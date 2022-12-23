using ClangSharp.Interop;
using General;
using General.Tracers;
using System.Xml;

namespace ExportCpp
{
    internal class CppAnalyzer
    {
        static internal string ExportClassMacro { get; private set; } = "EXPORT_CLASS";
        static internal string ExportConstructorMacro { get; private set; } = "EXPORT_CONSTRUCTOR";
        static internal string ExportFunctionMacro { get; private set; } = "EXPORT_FUNCTION";

        static internal string ExportStructMacro { get; private set; } = "EXPORT_STRUCT";
        static internal string ExportFieldMacro { get; private set; } = "EXPORT_FIELD";

        static internal string ExportEnumMacro { get; private set; } = "EXPORT_ENUM";
        static internal string ExportEnumValueMacro { get; private set; } = "EXPORT_ENUM_VALUE";

        private const string PLACE_HOLDER_INCLUDES = "{PLACE_HOLDER_INCLUDES}";

        public string ProjectFilename { get; init; }
        public string ExportFilename { get; init; }
        public string? ExportPchFilename { get; private set; } = null;
        public string BindingFilename { get; init; }
        public string ProjectDirectory { get; init; }

        public string LibraryName { get; init; }

        public string BindingClassname { get; init; }
        public string? BindingNamespace { get; private set; }

        private Namespace mGlobal;

        public CppAnalyzer(string projectFilename, string exportFilename, string bindingFilename, string libraryName, string bindingClassname)
        {
            this.ProjectFilename = projectFilename;
            this.ExportFilename = exportFilename;
            this.BindingFilename = bindingFilename;
            this.ProjectDirectory = Path.GetDirectoryName(projectFilename) ?? "";

            this.LibraryName = libraryName;
            this.BindingClassname = bindingClassname;

            mGlobal = new Namespace(new CppContext("", "", null), new CXCursor());
            mGlobal.SetAsRoot();
        }

        public void SetNamespace(string? bindingNamespace)
        {
            this.BindingNamespace = bindingNamespace;
        }

        public void SetExportPchFilename(string? filename)
        {
            this.ExportPchFilename = filename;
        }

        public void Analyze()
        {
            XmlDocument document = new XmlDocument();
            document.Load(this.ProjectFilename);

            XmlNodeList includeList = document.GetElementsByTagName("ClInclude");
            foreach (XmlNode includeNode in includeList)
            {
                string? headerFilename = includeNode.Attributes?["Include"]?.InnerText;
                if (string.IsNullOrWhiteSpace(headerFilename))
                {
                    continue;
                }

                this.analyzeHeader(Path.Combine(this.ProjectDirectory ?? "", headerFilename));
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
        }

        private void analyzeHeader(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }

            CppContext context = new CppContext(filename, mGlobal);

            //PInvokeGeneratorConfiguration configuration = new PInvokeGeneratorConfiguration(this.BindingNamespace ?? "TestBindings", Path.ChangeExtension(this.BindingFilename, ".xml"), filename, PInvokeGeneratorOutputMode.Xml, PInvokeGeneratorConfigurationOptions.None);

            CXTranslationUnit translationUnit;
            CXErrorCode errorCode = CXTranslationUnit.TryParse(CXIndex.Create(), filename, "-Xclang -ast-dump -fsyntax-only --include-directory E:\\Projects\\Tools\\ExportCppToCSharp\\Tests\\TestCpp --include framework.h --include pch.h".Split(), new CXUnsavedFile[0], CXTranslationUnit_Flags.CXTranslationUnit_None, out translationUnit);
            if (CXErrorCode.CXError_Success != errorCode)
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
                if (string.IsNullOrWhiteSpace(realPath) || Path.GetFullPath(realPath) != Path.GetFullPath(filename))
                {
                    continue;
                }

                this.analyzeCursor(context, cursor);
            }

            foreach (CXCursor cursor in context.Cursors)
            {
                Declaration? declaration = Declaration.Create(context, cursor);
                if (declaration is null)
                {
                    continue;
                }

                context.AppendDeclaration(declaration);
            }
        }

        private void analyzeCursor(CppContext context, CXCursor cursor)
        {
            //bool hashEquality = cursor.Hash == cursor.Definition.Hash;
            //bool isFunction = CXCursorKind.CXCursor_FunctionDecl == cursor.kind || CXCursorKind.CXCursor_Constructor == cursor.kind;
            //if (isFunction == hashEquality)
            //{
            //    return;
            //}
            if (((CXCursorKind.CXCursor_ClassDecl == cursor.kind || CXCursorKind.CXCursor_StructDecl == cursor.kind || CXCursorKind.CXCursor_EnumDecl == cursor.kind) && cursor.Definition.IsInvalid)
                || ((CXCursorKind.CXCursor_Constructor == cursor.kind || CXCursorKind.CXCursor_CXXMethod == cursor.kind) && !cursor.IsUserProvided))
            {
                return;
            }

            context.AppendCursor(cursor);

            for (uint i = 0; i < cursor.NumDecls; ++i)
            {
                this.analyzeCursor(context, cursor.GetDecl(i));
            }
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

                    if (!string.IsNullOrWhiteSpace(this.ExportPchFilename))
                    {
                        writer.WriteLine($"#include \"{this.ExportPchFilename}\"");
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
                    writer.Write(context.TabCount, "{");

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
                this.bindStruct(context, @struct);
                return;
            }

            Enum? @enum = declaration as Enum;
            if (@enum is not null)
            {
                this.bindEnum(context, @enum);
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

            string bindingName;
            Class? parent = declaration.Parent as Class;
            List<string> arguments = declaration.Arguments.Select(a => a.ToCSharpCode()).ToList();
            if (declaration is Constructor)
            {
                bindingName = declaration.BindingName;
            }
            else
            {
                arguments.Insert(0, $"{nameof(IntPtr)} instance");
                bindingName = parent is null ? declaration.BindingName : $"{parent.BindingPrefix}_{declaration.BindingName}";
            }

            context.Writer.WriteLine(context.TabCount, $"[DllImport(\"{this.LibraryName}\", CallingConvention = CallingConvention.Cdecl)]");
            context.Writer.WriteLine(context.TabCount, $"static internal extern {declaration.ReturnType.ToCSharpTypeString()} {bindingName}({string.Join(", ", arguments)});");

            ConsoleLogger.Log($"Bind {declaration}");
        }

        private void bindStruct(CSharpBindingContext context, Struct declaration)
        {
            this.bindCollection(context, declaration);
        }

        private void bindEnum(CSharpBindingContext context, Enum declaration)
        {

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
    }
}
