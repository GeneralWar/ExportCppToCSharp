using ClangSharp.Interop;
using General;
using System.Text.RegularExpressions;
using System.Xml;

namespace ExportCpp
{
    public partial class CppAnalyzer
    {
        static internal string ExportClassMacro { get; private set; } = "EXPORT_CLASS";
        static internal string ExportConstructorMacro { get; private set; } = "EXPORT_CONSTRUCTOR";
        static internal string ExportDestructorMacro { get; private set; } = "EXPORT_DESTRUCTOR";
        static internal string ExportFunctionMacro { get; private set; } = "EXPORT_FUNCTION";
        static internal string ExportFunctionPointerMacro { get; private set; } = "EXPORT_FUNCTION_POINTER";

        static internal string ExportStructMacro { get; private set; } = "EXPORT_STRUCT";
        static internal string ExportFieldMacro { get; private set; } = "EXPORT_FIELD";

        static internal string ExportEnumMacro { get; private set; } = "EXPORT_ENUM";
        static internal string ExportEnumValueMacro { get; private set; } = "EXPORT_ENUM_VALUE";

        static internal string[] ExportMacros => new[] { ExportClassMacro, ExportConstructorMacro, ExportDestructorMacro, ExportFunctionMacro, ExportFunctionPointerMacro, ExportStructMacro, ExportFieldMacro, ExportEnumMacro, ExportEnumValueMacro };


        private const string PLACE_HOLDER_INCLUDES = "{PLACE_HOLDER_INCLUDES}";
        private const string PLACE_HOLDER_USINGS = "{PLACE_HOLDER_USINGS}";

        public string ProjectFilename { get; init; }
        public string ProjectDirectory { get; init; }

        public string ExportFilename { get; init; }
        public string BindingFilename { get; init; }

        public string SolutionFilename { get; init; }
        public string SolutionDirectory { get; init; }

        public string LibraryName { get; init; }

        public string BindingClassname { get; init; }
        public string? BindingNamespace { get; private set; }

        public string? PchFilename { get; private set; } = null;
        public string? CompiledPchFilename { get; private set; } = null;

        public HashSet<string> IncludeDirectories { get; init; } = new HashSet<string>();
        public HashSet<string> IncludeHeaderFiles { get; init; } = new HashSet<string>();
        public HashSet<string> DefineMacros { get; init; } = new HashSet<string>();

        public HashSet<string> ProcessedFiles { get; init; } = new HashSet<string>();

        public Global Global { get; init; }

        private Dictionary<string, Declaration> mDeclarations = new Dictionary<string, Declaration>();
        public IEnumerable<Declaration> Declarations => mDeclarations.Values;

        public CppAnalyzer(string solutionFilename, string projectFilename, string exportFilename, string bindingFilename, string libraryName, string bindingClassname)
        {
            this.SolutionFilename = Path.GetFullPath(solutionFilename).MakeStandardPath();
            this.SolutionDirectory = Path.GetDirectoryName(this.SolutionFilename)?.MakeStandardPath() ?? "";

            this.ProjectFilename = Path.GetFullPath(projectFilename).MakeStandardPath();
            this.ExportFilename = Path.GetFullPath(exportFilename).MakeStandardPath();
            this.BindingFilename = Path.GetFullPath(bindingFilename).MakeStandardPath();
            this.ProjectDirectory = Path.GetDirectoryName(this.ProjectFilename)?.MakeStandardPath() ?? "";
            this.IncludeDirectories.Add(PathUtility.MakeDirectoryStandard(this.ProjectDirectory));

            this.LibraryName = libraryName;
            this.BindingClassname = bindingClassname;

            this.Global = new Global(this);
        }

        public void SetBindingNamespace(string? bindingNamespace)
        {
            this.BindingNamespace = bindingNamespace;
        }

        public void AppendDeclaration(Declaration declaration)
        {
            mDeclarations.Add(declaration.FullName, declaration);
        }

        public Declaration? GetDeclaration(string fullname)
        {
            Declaration? declaration;
            mDeclarations.TryGetValue(fullname, out declaration);
            return declaration;
        }

        private void printVersion()
        {
            CXTranslationUnit unit;
            CXErrorCode errorCode = CXTranslationUnit.TryParse(CXIndex.Create(), "", new string[] { "--version", "-v" }, new CXUnsavedFile[0], CXTranslationUnit_Flags.CXTranslationUnit_None, out unit);
        }

        private string checkConfigurationString(string inputString)
        {
            MatchCollection matches = Regex.Matches(inputString, @"\$\((\w+)\)");
            if (matches.Count > 0)
            {
                string content = inputString;
                foreach (Match match in matches)
                {
                    string environmentName = match.Groups[1].Value;
                    switch (environmentName)
                    {
                        case "ProjectDir":
                            content = content.Replace("$(ProjectDir)", PathUtility.MakeDirectoryStandard(this.ProjectDirectory));
                            break;
                        case "SolutionDir":
                            content = content.Replace("$(SolutionDir)", PathUtility.MakeDirectoryStandard(this.SolutionDirectory));
                            break;
                        default:
                            content = content.Replace($"$({environmentName})", Environment.GetEnvironmentVariable(environmentName) ?? throw new InvalidOperationException());
                            break;
                    }
                }
                return content;
            }
            return inputString;
        }

        private void checkIncludeDirectories(XmlDocument document)
        {
            XmlNodeList list = document.GetElementsByTagName("AdditionalIncludeDirectories");
            foreach (XmlNode item in list)
            {
                foreach (string value in item.InnerText.Split(';'))
                {
                    string directory = this.checkConfigurationString(value);
                    this.IncludeDirectories.Add(PathUtility.MakeDirectoryStandard(this.checkFullPathForProjectFile(directory)));
                }
            }
        }

        private string checkFullPathForProjectFile(string filename)
        {
            return (Path.IsPathRooted(filename) ? Path.GetFullPath(filename) : Path.GetFullPath(filename, this.ProjectDirectory)).MakeStandardPath();
        }

        private bool execute(IEnumerable<string> arguments, string filename, out CXTranslationUnit translationUnit)
        {
            return this.execute(arguments, filename, new CXUnsavedFile[0], out translationUnit);
        }

        private bool execute(IEnumerable<string> arguments, string filename, IEnumerable<CXUnsavedFile> unsavedFiles, out CXTranslationUnit translationUnit)
        {
            List<string> argumentList = new List<string>(arguments);
            foreach (string directory in this.IncludeDirectories)
            {
                argumentList.Add("--include-directory");
                argumentList.Add(directory);
            }
            foreach (string macro in this.DefineMacros)
            {
                argumentList.Add("-D");
                argumentList.Add(macro);
            }
            //#if DEVELOP
            //            argumentList.Add("-v");
            //#endif
            argumentList.Add("-fparse-all-comments");

            Tracer.Log($"Try to execute clang with arguments : {string.Join(" ", argumentList)} {filename}");
            //Program.ConsoleLogger.Log($"clang {string.Join(" ", argumentList)} \"{filename}\"");
            Program.ConsoleLogger.Log($"Try to analyze {filename}");
            CXErrorCode errorCode = CXTranslationUnit.TryParse(CXIndex.Create(), filename, new ReadOnlySpan<string>(argumentList.ToArray()), new ReadOnlySpan<CXUnsavedFile>(unsavedFiles.ToArray()), CXTranslationUnit_Flags.CXTranslationUnit_None, out translationUnit);
            bool failed = CXErrorCode.CXError_Success != errorCode;
            if (failed || translationUnit.DiagnosticSet.Count > 0)
            {
                if (failed)
                {
                    Program.ConsoleLogger.LogError($"{errorCode} clang {string.Join(" ", argumentList)} \"{filename}\"");
                }

                for (uint i = 0; i < translationUnit.DiagnosticSet.Count; ++i)
                {
                    CXDiagnostic diagnostic = translationUnit.GetDiagnostic(i);

                    CXFile file;
                    uint line, column, offset;
                    diagnostic.Location.GetFileLocation(out file, out line, out column, out offset);
                    switch (diagnostic.Severity)
                    {
                        case CXDiagnosticSeverity.CXDiagnostic_Ignored:
                            Tracer.Log($"{file}:{line}:{column} ignored: {diagnostic.Spelling.CString}");
                            break;
                        case CXDiagnosticSeverity.CXDiagnostic_Note:
                            Tracer.Log($"{file}:{line}:{column} note: {diagnostic.Spelling.CString}");
                            break;
                        case CXDiagnosticSeverity.CXDiagnostic_Warning:
                            Tracer.Warn($"{file}:{line}:{column} warning: {diagnostic.Spelling.CString}");
                            break;
                        case CXDiagnosticSeverity.CXDiagnostic_Error:
                            Tracer.Error($"{file}:{line}:{column} error: {diagnostic.Spelling.CString}");
                            failed = true;
                            break;
                        case CXDiagnosticSeverity.CXDiagnostic_Fatal:
                            Tracer.Error($"{file}:{line}:{column} fatal: {diagnostic.Spelling.CString}");
                            failed = true;
                            break;
                    }
                }
            }
            return !failed;
        }

        private bool checkPrecompiledHeader(XmlDocument document)
        {
            // <PrecompiledHeader>Use</PrecompiledHeader>
            XmlNodeList pchNodeList = document.GetElementsByTagName("PrecompiledHeader");
            if (pchNodeList.Count > 0)
            {
                // TODO: check condition
                XmlNode? pchUseNode = pchNodeList.Find<XmlNode>(n => "Use" == n.InnerText);
                if (pchUseNode is not null)
                {
                    // <PrecompiledHeaderFile>pch.h</PrecompiledHeaderFile>
                    XmlNode? pchNode = pchUseNode.ParentNode?.ChildNodes.Find<XmlNode>(n => "PrecompiledHeaderFile" == n.Name);
                    if (pchNode is null)
                    {
                        throw new InvalidOperationException();
                    }

                    this.PchFilename = pchNode.InnerText;
                    string pchFullPath = this.checkFullPathForProjectFile(this.PchFilename);
                    this.CompiledPchFilename = Path.ChangeExtension(pchFullPath, ".pch");
                    // clang -x c++-header test.h -o test.h.pch
                    List<string> commandArguments = new List<string>();
                    commandArguments.AddRange("-std=c++17 -x c++-header".Split());
                    commandArguments.Add("-o");
                    commandArguments.Add(this.CompiledPchFilename);

                    CXTranslationUnit translationUnit;
                    if (!this.execute(commandArguments, pchFullPath, out translationUnit))
                    {
                        Tracer.Error($"Failed to compile pch");
                        return false;
                    }

                    translationUnit.Save(this.CompiledPchFilename, CXSaveTranslationUnit_Flags.CXSaveTranslationUnit_None);

                    uint declarationCount = (uint)translationUnit.Cursor.NumDecls;
                    for (uint i = 0; i < declarationCount; ++i)
                    {
                        CXCursor cursor = translationUnit.Cursor.GetDecl(i);
                        CXSourceLocation location = cursor.Location;
                        if (location.IsInSystemHeader)
                        {
                            continue;
                        }

                        CXFile file;
                        uint line, column, offset;
                        location.GetFileLocation(out file, out line, out column, out offset);
                        string filename = this.checkFullPathForProjectFile(file.TryGetRealPathName().CString);
                        if (string.IsNullOrWhiteSpace(filename) || !PathUtility.IsPathUnderDirectory(filename, this.SolutionDirectory))
                        {
                            continue;
                        }

                        System.Diagnostics.Trace.WriteLine($"{filename}: {cursor}");
                        CppContext context = this.createCppContext(filename);
                        //if (ExportMacros.All(e => context.FileContent.IndexOf(e) < 0))
                        //{
                        //    continue;
                        //}
                        this.analyzeCursor(context, cursor);
                    }
                }
            }
            return true;
        }

        private string[] checkAllFiles(XmlDocument document, string tag, string? attribute)
        {
            HashSet<string> filenames = new HashSet<string>();
            XmlNodeList nodeList = document.GetElementsByTagName(tag);
            foreach (XmlNode node in nodeList)
            {
                string? filename = string.IsNullOrWhiteSpace(attribute) ? node.InnerText : node.Attributes?[attribute]?.InnerText;
                if (string.IsNullOrWhiteSpace(filename))
                {
                    continue;
                }

                filenames.Add(this.checkFullPathForProjectFile(filename));
            }
            return filenames.ToArray();
        }

        private string[] checkAllHeaderFiles(XmlDocument document)
        {
            return this.checkAllFiles(document, "ClInclude", "Include");
        }

        private string[] checkAllSourceFiles(XmlDocument document)
        {
            return this.checkAllFiles(document, "ClCompile", "Include");
        }

        public void Analyze()
        {
            this.printVersion();

            Program.ConsoleLogger.Log($"Try to analyze project {this.ProjectFilename}");
            Program.ConsoleLogger.Log($"Export to {this.ExportFilename}");
            Program.ConsoleLogger.Log($"Bind to {this.BindingFilename}");

            XmlDocument document = new XmlDocument();
            document.Load(this.ProjectFilename);

            this.checkIncludeDirectories(document);

            if (!this.checkPrecompiledHeader(document))
            {
                return;
            }

            string[] headers = this.checkAllHeaderFiles(document);
            string[] sources = this.checkAllSourceFiles(document);

            foreach (string filename in headers)
            {
                this.analyzeFile(filename);
            }

            CppContext context = this.createCppContext("");
            Program.ConsoleLogger.Log("Analyzing ...");
            this.Global.Analyze(context);

            while (context.FailedDeclarations.Count() > 0)
            {
                Declaration[] declarations = context.FailedDeclarations.Select(f => f.declaration).ToArray();
                context.ClearFailedDeclarations();

                Program.ConsoleLogger.Log("Analyzing ...");
                foreach (Declaration declaration in declarations)
                {
                    declaration.Analyze(context);
                }

                Declaration[] differences = context.FailedDeclarations.Select(f => f.declaration).Except(declarations).ToArray();
                if (context.FailedDeclarations.Count() == declarations.Length && 0 == differences.Length)
                {
                    foreach (FailedDeclaration fail in context.FailedDeclarations)
                    {
                        string message = $"Analyze error: {fail.declaration.GetType().Name} {fail.declaration.Name}"; // Declaration.ToString might throw exception
                        Program.ConsoleLogger.LogError(message);
                        Tracer.Error(message);

                        message = fail.exception.ToString();
                        Program.ConsoleLogger.LogError(message);
                        Tracer.Error(message);
                    }
#if !RELEASE
                    foreach (Declaration declaration in declarations)
                    {
                        // Should step into method to check exceptions
                        declaration.ForceAnalyze(context);
                    }
#endif
                    throw new InvalidOperationException($"There are still {context.FailedDeclarations.Count()} failed declarations");
                }
            }
        }

        private CppContext createCppContext(string filename)
        {
            CppContext context = new CppContext(filename, this);
            return context;
        }

        private void analyzeFile(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }

            filename = this.checkFullPathForProjectFile(filename);
            if (this.ProcessedFiles.Contains(filename))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(this.PchFilename) && Path.GetFullPath(this.PchFilename, this.ProjectDirectory) == filename)
            {
                return;
            }

            CppContext context = this.createCppContext(filename);
            if (ExportMacros.All(e => context.FileContent.IndexOf(e) < 0))
            {
                return;
            }

            // clang -Xclang -ast-dump -fsyntax-only --include-directory E:\\Projects\\Tools\\ExportCppToCSharp\\Tests\\TestCpp --include framework.h --include pch.h
            List<string> commandArguments = new List<string>();
            if (filename.EndsWith(".h"))
            {
                commandArguments.Add("-x");
                commandArguments.Add("c++");
            }
            commandArguments.AddRange("-std=c++17 -Xclang -ast-dump -fsyntax-only".Split());
            if (!string.IsNullOrWhiteSpace(this.CompiledPchFilename))
            {
                commandArguments.Add("-include-pch");
                commandArguments.Add(this.CompiledPchFilename);
            }
            //string.Join(" ", headers.Select(h => $"--include \"{h}\""))

            CXTranslationUnit translationUnit;
            if (!this.execute(commandArguments, filename, out translationUnit))
            {
                Tracer.Error($"Failed to parse translation unit for {filename}");
                return;
            }

            int declarationCount = clangsharp.Cursor_getNumDecls(translationUnit.Cursor);
            for (int declarationIndex = 0; declarationIndex < declarationCount; ++declarationIndex)
            {
                CXCursor cursor = clangsharp.Cursor_getDecl(translationUnit.Cursor, (uint)declarationIndex);
                CXSourceLocation location = cursor.Location;
                if (location.IsInSystemHeader)
                {
                    continue;
                }

                CXFile file;
                uint line, column, offset;
                location.GetFileLocation(out file, out line, out column, out offset);
                string realPath = file.TryGetRealPathName().CString;
                if (string.IsNullOrWhiteSpace(realPath) || Path.GetFullPath(realPath).MakeStandardPath() != Path.GetFullPath(filename).MakeStandardPath())
                {
                    continue;
                }

                this.analyzeCursor(context, cursor);
            }

            this.ProcessedFiles.Add(filename);
        }

        private void analyzeCursor(CppContext context, CXCursor cursor)
        {
#if DEBUG || DEVELOP
            string filename = cursor.Location.GetFile().Name.CString;
#endif
            string cursorName = cursor.GetName();
            if (ExportMacros.Contains(cursorName))
            {
                return;
            }

            if (cursor.Extent.IsNull || CXCursorKind.CXCursor_ClassTemplatePartialSpecialization == cursor.kind)
            {
                return;
            }

            if (CXCursorKind.CXCursor_UnexposedDecl == cursor.kind && CXTypeKind.CXType_Invalid == cursor.Type.kind)
            {
                return;
            }

            //if (cursor.IsTypeDeclaration() && cursor.Definition.IsInvalid) // might skip forward declaration, so comment this
            //{
            //    return;
            //}

            if ((CXCursorKind.CXCursor_Constructor == cursor.kind || CXCursorKind.CXCursor_Destructor == cursor.kind || CXCursorKind.CXCursor_CXXMethod == cursor.kind) && !cursor.IsUserProvided) // only user provided methods and constructors can be exported
            {
                return;
            }

            if (CXCursorKind.CXCursor_FieldDecl == cursor.kind && CXCursorKind.CXCursor_ClassDecl == cursor.SemanticParent.kind) // do not export class fields, only struct fields and enum constants can be exported
            {
                return;
            }

            if (CXCursorKind.CXCursor_CXXMethod == cursor.kind && (!cursor.IsUserProvided || cursor.IsOverloadedOperator))
            {
                return;
            }

            if (CXCursorKind.CXCursor_UsingDirective == cursor.kind)
            {
                context.CurrentScope.AppendUsingNamespace(cursor);
                this.analyzeChildCursors(context, cursor);
                return;
            }

            if (!this.checkExportMark(context, cursor))
            {
                return;
            }

            if (cursor.IsAnonymousStructOrUnion)
            {
                this.analyzeChildCursors(context, cursor);
                return;
            }

            Declaration? declaration = Declaration.Create(context, cursor);
            if (declaration is null)
            {
                return;
            }

            Declaration? record = context.GetDeclaration(declaration.FullName);
            if (record is null)
            {
                context.AppendDeclaration(declaration);
            }
            else
            {
                record.Merge(declaration);
            }

            if (CXCursorKind.CXCursor_StructDecl == cursor.kind)
            {
                int baseCount = cursor.NumBases;
                for (uint baseIndex = 0; baseIndex < baseCount; ++baseIndex)
                {
                    CXCursor baseCursor = cursor.GetBase(baseIndex).Definition;
                    int fieldCount = baseCursor.NumFields;
                    for (uint fieldIndex = 0; fieldIndex < fieldCount; ++fieldIndex)
                    {
                        this.analyzeCursor(context, baseCursor.GetField(fieldIndex));
                    }
                }
            }

            if (cursor.NumDecls > 0 && declaration is DeclarationCollection)
            {
                context.PushScope(declaration as DeclarationCollection ?? throw new InvalidOperationException("Only collection can be scope"));
                this.analyzeChildCursors(context, cursor);
                context.PopScope(declaration as DeclarationCollection ?? throw new InvalidOperationException("Only collection can be scope"));
            }
        }

        private void analyzeChildCursors(CppContext context, CXCursor cursor)
        {
            int declarationCount = cursor.NumDecls;
            for (uint i = 0; i < declarationCount; ++i)
            {
                this.analyzeCursor(context, cursor.GetDecl(i));
            }
        }

        private bool checkExportMark(CppContext context, CXCursor cursor)
        {
            if (CXCursorKind.CXCursor_Namespace == cursor.kind || CXCursorKind.CXCursor_EnumConstantDecl == cursor.kind)
            {
                return true;
            }

            if (/*CXCursorKind.CXCursor_FieldDecl == cursor.kind && */CXCursorKind.CXCursor_StructDecl == cursor.SemanticParent.kind)
            {
                return true;
            }

            string previousContent = CheckContentFromPreviousCursor(context, cursor);
            if (ExportMacros.All(e => previousContent.IndexOf(e) < 0))
            {
                return false;
            }
            return true;
        }

        public void Export()
        {
            string? directory = Path.GetDirectoryName(this.ExportFilename);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException($"Invalid export filename {this.ExportFilename}");
            }
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (MemoryStream stream = new MemoryStream())
            {
                HashSet<string> usings = new HashSet<string>();
                HashSet<string> includes = new HashSet<string>();
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    CppExportContext context = new CppExportContext(writer);

                    if (!string.IsNullOrWhiteSpace(this.PchFilename))
                    {
                        writer.WriteLine($"#include \"{this.PchFilename}\"");
                    }
                    writer.Write(PLACE_HOLDER_INCLUDES);
                    writer.WriteLine(PLACE_HOLDER_USINGS);

                    writer.WriteLine(context.TabCount, "extern \"C\"");
                    writer.Write(context.TabCount, "{");

                    this.export(context, this.Global);

                    while (context.TabCount >= 0)
                    {
                        writer.WriteLine(context.TabCount--, "}");
                    }

                    if (File.Exists(this.ExportFilename))
                    {
                        File.Delete(this.ExportFilename);
                    }

                    includes.AddRange(context.Filenames.Select(p => $"#include \"{Path.GetRelativePath(directory, p)}\""));
                    usings.AddRange(context.Declarations.Select(d => d.Namespace).OfType<Namespace>().Select(n => $"using namespace {n.FullName};"));
                }

                string content = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                if (includes.Count > 0)
                {
                    content = content.Replace(PLACE_HOLDER_INCLUDES, string.Join(Environment.NewLine, includes) + Environment.NewLine);
                }
                else
                {
                    content = content.Replace(PLACE_HOLDER_INCLUDES, "");
                }
                if (usings.Count > 0)
                {
                    content = content.Replace(PLACE_HOLDER_USINGS, string.Join(Environment.NewLine, usings) + Environment.NewLine);
                }
                else
                {
                    content = content.Replace(PLACE_HOLDER_USINGS, "");
                }
                File.WriteAllText(this.ExportFilename, content);
            }
        }

        private void export(CppExportContext context, Declaration declaration)
        {
            if (!declaration.ShouldExport)
            {
                return;
            }

            Namespace? @namespace = declaration as Namespace;
            if (@namespace is not null)
            {
                this.exportNamespace(context, @namespace);
                return;
            }

            if (!PathUtility.IsPathUnderDirectory(declaration.Filename, this.ProjectDirectory))
            {
                return;
            }

            Class? @class = declaration as Class;
            if (@class is not null)
            {
                this.exportClass(context, @class);
                return;
            }

            Function? function = declaration as Function;
            if (function is not null)
            {
                ++context.TabCount;
                this.exportFunction(context, function);
                --context.TabCount;
                return;
            }

            Struct? @struct = declaration as Struct;
            if (@struct is not null)
            {
                this.exportStruct(context, @struct);
                return;
            }

            Enum? @enum = declaration as Enum;
            if (@enum is not null)
            {
                this.exportEnum(context, @enum);
                return;
            }
        }

        private void exportCollection(CppExportContext context, DeclarationCollection declaration)
        {
            foreach (Declaration child in declaration.Declarations)
            {
                this.export(context, child);
            }
        }

        private void exportNamespace(CppExportContext context, Namespace declaration)
        {
            this.exportCollection(context, declaration);
        }

        private void exportClass(CppExportContext context, Class declaration)
        {
            this.exportCollection(context, declaration);
        }

        private void exportFunction(CppExportContext context, Function declaration)
        {
            if (!declaration.ShouldExport)
            {
                return;
            }

            context.AppendDeclaration(declaration);

            declaration.MakeCppExportDefinition(context);
            Program.ConsoleLogger.Log($"Export {declaration}");
        }

        private void exportStruct(CppExportContext context, Struct declaration)
        {
            this.exportCollection(context, declaration);
        }

        private void exportEnum(CppExportContext context, Enum declaration)
        {

        }

        public void Bind()
        {
            using (FileStream stream = File.Open(this.BindingFilename, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    CSharpBindingContext context = new CSharpBindingContext(writer);

                    writer.WriteLine("using System.Runtime.InteropServices;");

                    this.bind(context, this.Global);

                    if (!string.IsNullOrWhiteSpace(this.BindingNamespace))
                    {
                        writer.WriteLine();
                        writer.WriteLine(context.TabCount, $"namespace {this.BindingNamespace}");
                        writer.Write(context.TabCount, "{");
                        ++context.TabCount;
                    }

                    writer.WriteLine();
                    writer.WriteLine(context.TabCount, $"static internal unsafe partial class {this.BindingClassname}");
                    writer.WriteLine(context.TabCount, "{");

                    ++context.TabCount;
                    writer.WriteLine(context.TabCount, $"private const string LIBRARY_NAME = \"{this.LibraryName}\";");

                    foreach (Function function in context.ExportedFunctions)
                    {
                        this.bindFunction(context, function);
                    }
                    --context.TabCount;

                    while (context.TabCount >= 0)
                    {
                        writer.WriteLine(context.TabCount--, "}");
                    }
                }
            }
        }

        private void bind(CSharpBindingContext context, Declaration declaration)
        {
            if (!declaration.ShouldExport)
            {
                return;
            }

            Namespace? @namespace = declaration as Namespace;
            if (@namespace is not null)
            {
                this.bindNamespace(context, @namespace);
                return;
            }

            if (!PathUtility.IsPathUnderDirectory(declaration.Filename, this.ProjectDirectory))
            {
                return;
            }

            Class? @class = declaration as Class;
            if (@class is not null)
            {
                this.bindClass(context, @class);
                return;
            }

            Function? function = declaration as Function;
            if (function is not null)
            {
                context.AppendFunction(function);
                return;
            }

            Struct? @struct = declaration as Struct;
            if (@struct is not null)
            {
                context.AppendStruct(@struct);
                return;
            }

            Enum? @enum = declaration as Enum;
            if (@enum is not null)
            {
                context.AppendEnum(@enum);
                return;
            }
        }

        private void bindCollection(CSharpBindingContext context, DeclarationCollection declaration)
        {
            foreach (Declaration child in declaration.Declarations)
            {
                this.bind(context, child);
            }
        }

        private void bindNamespace(CSharpBindingContext context, Namespace declaration)
        {
            context.PushScope(declaration);

            if (declaration.Global != declaration)
            {
                context.Writer.WriteLine();
                context.WriteLine($"namespace {declaration.Name}");
                context.Write("{");
                ++context.TabCount;
            }

            this.bindCollection(context, declaration);

            foreach (Enum @enum in context.ExportedEnums)
            {
                this.bindEnum(context, @enum);
            }

            foreach (Struct @struct in context.ExportedStructs)
            {
                this.bindStruct(context, @struct);
            }

            if (declaration.Global != declaration)
            {
                --context.TabCount;
                context.WriteLine("}");
            }

            context.PopScope(declaration);
        }

        private void bindClass(CSharpBindingContext context, Class declaration)
        {
            this.bindCollection(context, declaration);
        }

        private void bindFunction(CSharpBindingContext context, Function declaration)
        {
            if (!declaration.ShouldExport)
            {
                return;
            }

            declaration.MakeCSharpBindingDeclaration(context);

            Program.ConsoleLogger.Log($"Bind {declaration}");
        }

        private void bindStruct(CSharpBindingContext context, Struct declaration)
        {
            if (string.IsNullOrWhiteSpace(declaration.BindingName))
            {
                return;
            }

            context.Writer.WriteLine();
            declaration.MakeCSharpDefinition(context);
            Program.ConsoleLogger.Log($"Bind {declaration}");
        }

        private void bindEnum(CSharpBindingContext context, Enum declaration)
        {
            if (string.IsNullOrWhiteSpace(declaration.BindingName))
            {
                return;
            }

            context.WriteLine();
            declaration.MakeCSharpDefinition(context);

            Program.ConsoleLogger.Log($"Bind {declaration}");
        }

        public void ExportXml()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.BindingFilename) ?? throw new InvalidOperationException());

            XmlDocument document = new XmlDocument();
            document.LoadXml("<Export></Export>");
            this.exportXml(document.DocumentElement ?? throw new InvalidOperationException(), this.Global as DeclarationCollection);
            document.Save(Path.ChangeExtension(this.BindingFilename, ".xml"));
        }

        private void exportXml(XmlElement parent, Declaration declaration)
        {
            XmlElement element = declaration.ToXml(parent.OwnerDocument);
            parent.AppendChild(element);

            DeclarationCollection? collection = declaration as DeclarationCollection;
            if (collection is not null)
            {
                this.exportXml(element, collection);
            }
        }

        private void exportXml(XmlElement parent, DeclarationCollection declaration)
        {
            foreach (Declaration child in declaration.Declarations)
            {
                if (child.ShouldExport)
                {
                    this.exportXml(parent, child);
                }
            }
        }

        static internal CXCursor CheckPreviousCursor(CXCursor cursor, CXFile expectedFile)
        {
            CXCursor parent = cursor.SemanticParent;
            int siblingIndex = cursor.GetSiblingIndex();
            if (0 == siblingIndex)
            {
                return parent;
            }

            CXCursor previousCursor = CXCursor.Null;
            for (siblingIndex = siblingIndex - 1; siblingIndex > -1 && (!previousCursor.IsExposedDeclaration() || previousCursor.Location.GetFile() != expectedFile); --siblingIndex)
            {
                previousCursor = parent.GetDecl((uint)siblingIndex);
            }
            if (previousCursor.IsUnexposed)
            {
                Tracer.Assert(-1 == siblingIndex);
                return parent;
            }
            return previousCursor.IsInvalid ? parent : previousCursor;
        }

        static internal bool CheckRangeFromPreviousCursor(CppContext context, CXCursor cursor, out int previousEndIndex, out int currentStartIndex)
        {
            uint line, column, offset;

            CXFile file = cursor.Location.GetFile();
            string content = context.GetFileContent(file) ?? throw new InvalidOperationException();

            CXCursor previousCursor = CheckPreviousCursor(cursor, cursor.Location.GetFile());
            file = previousCursor.Location.GetFile();
            if (string.IsNullOrWhiteSpace(file.Name.CString))
            {
                //cursor.Location.GetFileLocation(out file, out line, out column, out offset);
                //return content.Substring(0, content.Locate((int)line, (int)column));
                throw new InvalidOperationException();
            }

            Tracer.Assert(file == cursor.Location.GetFile() && !previousCursor.IsUnexposed);

            cursor.Extent.Start.GetFileLocation(out file, out line, out column, out offset);
            currentStartIndex = content.Locate((int)line, (int)column);

            previousEndIndex = 0;
            if (!previousCursor.IsInvalid)
            {
                previousCursor.Extent.End.GetFileLocation(out file, out line, out column, out offset);
                previousEndIndex = content.Locate((int)line, (int)column); // offset is not accurate
            }
            if (previousEndIndex > currentStartIndex)
            {
                previousCursor.Extent.Start.GetFileLocation(out file, out line, out column, out offset);
                previousEndIndex = content.Locate((int)line, (int)column); // offset is not accurate
            }
            if (previousEndIndex == currentStartIndex)// for nested anonymous struct or union
            {
                return CheckRangeFromPreviousCursor(context, previousCursor, out previousEndIndex, out currentStartIndex);
            }

            return true;
        }

        static internal string CheckContentFromPreviousCursor(CppContext context, CXCursor cursor)
        {
            uint line, column, offset;

            CXFile file = cursor.Location.GetFile();
            string filename = Path.GetFullPath(file.TryGetRealPathName().CString);
            string content = context.Filename == filename ? context.FileContent : File.ReadAllText(filename);

            CXCursor previousCursor = CheckPreviousCursor(cursor, cursor.Location.GetFile());
            file = previousCursor.Location.GetFile();
            if (string.IsNullOrWhiteSpace(file.Name.CString))
            {
                cursor.Location.GetFileLocation(out file, out line, out column, out offset);
                return content.Substring(0, content.Locate((int)line, (int)column));
            }

            Tracer.Assert(file == cursor.Location.GetFile() && !previousCursor.IsUnexposed);

            int previousEndIndex, currentStartIndex;
            if (!CheckRangeFromPreviousCursor(context, cursor, out previousEndIndex, out currentStartIndex))
            {
                throw new InvalidOperationException();
            }

            return content.Substring(previousEndIndex, currentStartIndex - previousEndIndex);
        }

        static internal string[] SplitContent(string content, string separator, params Tuple<char, char>[] skipScopes)
        {
            Stack<char> expectedSymbols = new Stack<char>();
            List<string> arguments = new List<string>();
            char c;
            int s = 0, e = 0;
            for (; e < content.Length; ++e)
            {
                c = content[e];
                int shouldSkip = Array.FindIndex(skipScopes, t => t.Item1 == c);
                if (shouldSkip > -1)
                {
                    char start = skipScopes[shouldSkip].Item1, end = skipScopes[shouldSkip].Item2;
                    int endIndex = content.IndexOf(end, e);
                    int count = content.Count(start, e, endIndex);
                    while (--count > 0)
                    {
                        endIndex = content.IndexOf(end, endIndex + 1);
                    }
                    e = endIndex;
                    continue;
                }

                if (expectedSymbols.Count > 0 && expectedSymbols.Peek() == c)
                {
                    expectedSymbols.Pop();
                    continue;
                }

                if (e + separator.Length < content.Length && 0 == string.Compare(content, e, separator, 0, separator.Length))
                {
                    arguments.Add(content.Substring(s, e - s).Trim());
                    s = e + separator.Length;
                }
            }
            arguments.Add(content.Substring(s, e - s).Trim());

            Tracer.Assert(0 == expectedSymbols.Count);
            return arguments.ToArray();
        }

        static internal string[] SplitArguments(string argumentsContent)
        {
            string contents = argumentsContent.TrimStart().TrimEnd(' ', ';');
            if (contents.StartsWith('('))
            {
                Tracer.Assert(contents.EndsWith(')'));
                contents = contents.Substring(1, contents.Length - 2);
            }
            return SplitContent(contents, ",", new Tuple<char, char>('{', '}'), new Tuple<char, char>('(', ')'), new Tuple<char, char>('<', '>'));
        }

        static internal string[] SplitFullName(string fullname)
        {
            return SplitContent(fullname, "::", new Tuple<char, char>('{', '}'), new Tuple<char, char>('(', ')'), new Tuple<char, char>('<', '>'));
        }

        static internal string CheckArgumentName(string argumentContent)
        {
            if (string.IsNullOrWhiteSpace(argumentContent))
            {
                return "";
            }

            string maybeName = argumentContent.Trim().Split().Last();
            return maybeName.TrimStart('*', '&');
        }
    }
}
