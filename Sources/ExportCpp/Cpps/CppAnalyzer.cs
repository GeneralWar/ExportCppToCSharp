using ClangSharp.Interop;
using General;
using General.Tracers;
using System.Text.RegularExpressions;
using System.Xml;

namespace ExportCpp
{
    public partial class CppAnalyzer
    {
        static internal string ExportClassMacro { get; private set; } = "EXPORT_CLASS";
        static internal string ExportConstructorMacro { get; private set; } = "EXPORT_CONSTRUCTOR";
        static internal string ExportFunctionMacro { get; private set; } = "EXPORT_FUNCTION";
        static internal string ExportFunctionPointerMacro { get; private set; } = "EXPORT_FUNCTION_POINTER";

        static internal string ExportStructMacro { get; private set; } = "EXPORT_STRUCT";
        static internal string ExportFieldMacro { get; private set; } = "EXPORT_FIELD";

        static internal string ExportEnumMacro { get; private set; } = "EXPORT_ENUM";
        static internal string ExportEnumValueMacro { get; private set; } = "EXPORT_ENUM_VALUE";

        static internal string[] ExportMacros => new[] { ExportClassMacro, ExportConstructorMacro, ExportFunctionMacro, ExportFunctionPointerMacro, ExportStructMacro, ExportFieldMacro, ExportEnumMacro, ExportEnumValueMacro };


        private const string PLACE_HOLDER_INCLUDES = "{PLACE_HOLDER_INCLUDES}";

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

        private Namespace mGlobal;

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

            mGlobal = this.initializeGlobal();
        }

        public void SetBindingNamespace(string? bindingNamespace)
        {
            this.BindingNamespace = bindingNamespace;
        }

        /// <summary>
        /// Set custom declaration, will throw exception if fullname is invalid
        /// </summary>
        /// <exception cref="InvalidOperationException">If fullname is invalid, such as invalid ancestor</exception>
        public void SetCustomDeclaration(string fullname, Declaration declaration)
        {
            DeclarationCollection? parent = mGlobal;
            string[] parts = fullname.Split(new[] { "::", "." }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts.Take(parts.Length - 1))
            {
                parent = parent?.GetDeclaration(part)as DeclarationCollection;
            }
            if (parent is null)
            {
                throw new InvalidOperationException($"Invalid path {fullname}");
            }
            parent.SetDeclaration(declaration);
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

            Tracer.Log($"Try to execute clang with arguments : {string.Join(" ", argumentList)} {filename}");
            //ConsoleLogger.Log($"clang {string.Join(" ", argumentList)} \"{filename}\"");
            ConsoleLogger.Log($"Try to analyze {filename}");
            CXErrorCode errorCode = CXTranslationUnit.TryParse(CXIndex.Create(), filename, new ReadOnlySpan<string>(argumentList.ToArray()), new ReadOnlySpan<CXUnsavedFile>(unsavedFiles.ToArray()), CXTranslationUnit_Flags.CXTranslationUnit_None, out translationUnit);
            bool failed = CXErrorCode.CXError_Success != errorCode;
            if (failed || translationUnit.DiagnosticSet.Count > 0)
            {
                if (failed)
                {
                    ConsoleLogger.LogError($"{errorCode} clang {string.Join(" ", argumentList)} \"{filename}\"");
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

                    for (uint i = 0; i < translationUnit.Cursor.NumDecls; ++i)
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
                        string realPath = file.TryGetRealPathName().CString;
                        if (string.IsNullOrWhiteSpace(realPath) || !PathUtility.IsPathUnderDirectory(realPath, this.ProjectDirectory))
                        {
                            continue;
                        }

                        CppContext context = this.createCppContext(this.checkFullPathForProjectFile(realPath));
                        if (ExportMacros.All(e => context.FileContent.IndexOf(e) < 0))
                        {
                            continue;
                        }
                        this.analyzeCursor(context, cursor);
                        this.ProcessedFiles.Add(context.Filename);
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

            ConsoleLogger.Log($"Try to analyze project {this.ProjectFilename}");
            ConsoleLogger.Log($"Export to {this.ExportFilename}");
            ConsoleLogger.Log($"Bind to {this.BindingFilename}");

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

            /*XmlNodeList sourceList = document.GetElementsByTagName("ClCompile");
            foreach (XmlNode sourceNode in sourceList)
            {
                string? sourceFilename = sourceNode.Attributes?["Include"]?.InnerText;
                if (string.IsNullOrWhiteSpace(sourceFilename))
                {
                    continue;
                }

                this.analyzeSource(Path.Combine(this.Directory ?? "", sourceFilename));
            }*/

            //if (File.Exists(this.CompiledPchFilename))
            //{
            //    File.Delete(this.CompiledPchFilename);
            //}
        }

        private CppContext createCppContext(string filename)
        {
            return new CppContext(filename, mGlobal);
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
        }

        private void analyzeCursor(CppContext context, CXCursor cursor)
        {
            string cursorName = cursor.GetName();
            if (ExportMacros.Contains(cursorName))
            {
                return;
            }

            if (CXCursorKind.CXCursor_UnexposedDecl == cursor.kind)
            {
                return;
            }

            if (cursor.IsTypeDeclaration() && cursor.Definition.IsInvalid)
            {
                return;
            }

            if ((CXCursorKind.CXCursor_Constructor == cursor.kind || CXCursorKind.CXCursor_CXXMethod == cursor.kind) && !cursor.IsUserProvided) // only user provided methods and constructors can be exported
            {
                return;
            }

            if (CXCursorKind.CXCursor_FieldDecl == cursor.kind && CXCursorKind.CXCursor_ClassDecl == cursor.SemanticParent.kind) // do not export class fields, only struct fields and enum constants can be exported
            {
                return;
            }

            if (!this.checkExportMark(context, cursor))
            {
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

            context.PushScope(declaration);
            for (uint i = 0; i < cursor.NumDecls; ++i)
            {
                this.analyzeCursor(context, cursor.GetDecl(i));
            }
            //for (uint i = 0; i < cursor.NumChildren; ++i)
            //{
            //    this.analyzeCursor(context, cursor.GetChild(i));
            //}
            context.PopScope(declaration);
        }

        private bool checkExportMark(CppContext context, CXCursor cursor)
        {
            if (CXCursorKind.CXCursor_Namespace == cursor.kind || CXCursorKind.CXCursor_EnumConstantDecl == cursor.kind)
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
                List<string> includes = new List<string>();
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    CppExportContext context = new CppExportContext(writer);

                    if (!string.IsNullOrWhiteSpace(this.PchFilename))
                    {
                        writer.WriteLine($"#include \"{this.PchFilename}\"");
                    }
                    writer.WriteLine(PLACE_HOLDER_INCLUDES);

                    writer.WriteLine(context.TabCount, "extern \"C\"");
                    writer.Write(context.TabCount, "{");

                    this.export(context, mGlobal);

                    while (context.TabCount >= 0)
                    {
                        writer.WriteLine(context.TabCount--, "}");
                    }

                    if (File.Exists(this.ExportFilename))
                    {
                        File.Delete(this.ExportFilename);
                    }

                    includes.AddRange(context.Filenames.Select(p => $"#include \"{Path.GetRelativePath(directory, p)}\""));
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
                File.WriteAllText(this.ExportFilename, content);
            }
        }

        private void export(CppExportContext context, Declaration declaration)
        {
            Namespace? @namespace = declaration as Namespace;
            if (@namespace is not null)
            {
                this.exportNamespace(context, @namespace);
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
            if (string.IsNullOrWhiteSpace(declaration.BindingName))
            {
                return;
            }

            context.Writer.WriteLine();
            context.AppendDeclaration(declaration);

            context.Writer.WriteLine(context.TabCount, declaration.MakeCppExportDeclaration());
            context.Writer.WriteLine(context.TabCount, "{");
            foreach (string content in declaration.MakeCppExportDefinition())
            {
                context.Writer.WriteLine(context.TabCount + 1, content);
            }
            context.Writer.WriteLine(context.TabCount, "}");

            ConsoleLogger.Log($"Export {declaration}");
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
                    writer.WriteLine();

                    if (!string.IsNullOrWhiteSpace(this.BindingNamespace))
                    {
                        writer.WriteLine(context.TabCount, $"namespace {this.BindingNamespace}");
                        writer.WriteLine(context.TabCount, "{");
                        ++context.TabCount;
                    }

                    writer.WriteLine(context.TabCount, $"static internal class {this.BindingClassname}");
                    writer.WriteLine(context.TabCount, "{");

                    this.bind(context, mGlobal);

                    while (context.TabCount >= 0)
                    {
                        writer.WriteLine(context.TabCount--, "}");
                    }
                }
            }
        }

        private void bind(CSharpBindingContext context, Declaration declaration)
        {
            Namespace? @namespace = declaration as Namespace;
            if (@namespace is not null)
            {
                this.bindNamespace(context, @namespace);
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
                ++context.TabCount;
                this.bindFunction(context, function);
                --context.TabCount;
                return;
            }

            Struct? @struct = declaration as Struct;
            if (@struct is not null)
            {
                ++context.TabCount;
                this.bindStruct(context, @struct);
                --context.TabCount;
                return;
            }

            Enum? @enum = declaration as Enum;
            if (@enum is not null)
            {
                ++context.TabCount;
                this.bindEnum(context, @enum);
                --context.TabCount;
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
            this.bindCollection(context, declaration);
        }

        private void bindClass(CSharpBindingContext context, Class declaration)
        {
            this.bindCollection(context, declaration);
        }

        private void bindFunction(CSharpBindingContext context, Function declaration)
        {
            if (string.IsNullOrWhiteSpace(declaration.BindingName))
            {
                return;
            }

            context.Writer.WriteLine();
            context.Writer.WriteLine(context.TabCount, $"[DllImport(\"{this.LibraryName}\", CallingConvention = CallingConvention.Cdecl)]");
            context.Writer.WriteLine(context.TabCount, declaration.MakeCSharpBindingDeclaration());

            ConsoleLogger.Log($"Bind {declaration}");
        }

        private void bindStruct(CSharpBindingContext context, Struct declaration)
        {
            this.bindCollection(context, declaration);
        }

        private void bindEnum(CSharpBindingContext context, Enum declaration)
        {
            if (string.IsNullOrWhiteSpace(declaration.BindingName))
            {
                return;
            }

            context.Writer.WriteLine(context.TabCount, $"internal enum {declaration.BindingName}");
            context.Writer.WriteLine(context.TabCount, "{");
            foreach (Declaration child in declaration.Declarations)
            {
                EnumConstant? constant = child as EnumConstant;
                if (constant is not null)
                {
                    context.Writer.WriteLine(context.TabCount + 1, constant.ToCSharpCode());
                }
            }
            context.Writer.WriteLine(context.TabCount, "}");

            ConsoleLogger.Log($"Bind {declaration}");
        }

        public void ExportXml()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml("<Export></Export>");
            this.exportXml(document.DocumentElement ?? throw new InvalidOperationException(), mGlobal as DeclarationCollection);
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

        static internal CXCursor CheckPreviousCursor(CppContext context, CXCursor cursor, CXFile expectedFile)
        {
            CXCursor previousCursor = cursor.PreviousDecl;
            if (previousCursor.IsExposedDeclaration() && previousCursor.Location.GetFile() == expectedFile)
            {
                return previousCursor;
            }

            CXCursor parent = cursor.SemanticParent;
            int siblingIndex = cursor.GetSiblingIndex();
            previousCursor = 0 == siblingIndex ? parent : parent.GetDecl((uint)siblingIndex - 1);
            if (!previousCursor.IsExposedDeclaration())
            {
                return CheckPreviousCursor(context, previousCursor, expectedFile);
            }
            if (previousCursor.Location.GetFile() != expectedFile)
            {
                return CheckPreviousCursor(context, previousCursor, expectedFile);
            }
            return previousCursor;
        }

        static internal string CheckContentFromPreviousCursor(CppContext context, CXCursor cursor)
        {
            CXCursor previousCursor = CheckPreviousCursor(context, cursor, cursor.Location.GetFile());
            Tracer.Assert(previousCursor.Location.GetFile() == cursor.Location.GetFile());

            CXFile file;
            uint line, column, offset;
            cursor.Extent.Start.GetFileLocation(out file, out line, out column, out offset);
            int startIndex = context.FileContent.Locate((int)line, (int)column);

            int previousEndIndex = 0;
            if (!previousCursor.IsInvalid)
            {
                previousCursor.Extent.End.GetFileLocation(out file, out line, out column, out offset);
                previousEndIndex = context.FileContent.Locate((int)line, (int)column); // offset is not accurate
            }
            if (previousEndIndex > startIndex)
            {
                previousCursor.Extent.Start.GetFileLocation(out file, out line, out column, out offset);
                previousEndIndex = context.FileContent.Locate((int)line, (int)column); // offset is not accurate
            }

            return context.FileContent.Substring(previousEndIndex, startIndex - previousEndIndex);
        }

        static internal string[] SplitArguments(string argumentsContent)
        {
            string contents = argumentsContent.TrimStart(' ', '(').TrimEnd(' ', ')', ';');
            Stack<char> expectedSymbols = new Stack<char>();
            List<string> arguments = new List<string>();
            char c;
            int s = 0, e = 0;
            for (; e < contents.Length; ++e)
            {
                c = contents[e];
                if ('(' == c)
                {
                    expectedSymbols.Push(')');
                    continue;
                }
                if ('{' == c)
                {
                    expectedSymbols.Push('}');
                    continue;
                }

                if (expectedSymbols.Count > 0 && expectedSymbols.Peek() == c)
                {
                    expectedSymbols.Pop();
                    continue;
                }

                if (',' == c)
                {
                    arguments.Add(contents.Substring(s, e - s).Trim());
                    s = e + 1;
                }
            }
            arguments.Add(contents.Substring(s, e - s).Trim());

            Tracer.Assert(0 == expectedSymbols.Count);
            return arguments.ToArray();
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
