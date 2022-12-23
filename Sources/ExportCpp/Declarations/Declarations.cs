using ClangSharp.Interop;
using ExportCpp.Cpps;
using System.Text.RegularExpressions;
using System.Xml;
using Type = System.Type;

namespace ExportCpp
{
    internal abstract class Declaration
    {
        protected const string KEY_DECORATIONS = "Decorations";

        public CXCursor Cursor { get; init; }

        public Namespace? Root { get; protected set; }
        public string Filename { get; init; }

        public string Name { get; init; }
        public string FullName { get; private set; }
        public string DisplayString { get; private set; }

        public string Content { get; init; }

        public int Index { get; init; }
        public int CloseIndex { get; init; }

        public abstract string? ExportMacro { get; }
        public bool ShouldExport { get; private set; }
        public string? ExportContent { get; private set; }
        public string[] ExportContents { get; private set; } = new string[0];

        public DeclarationCollection? Parent { get; private set; }
        public bool IsLastChildInParent => this.Parent is not null && this.Parent.Declarations.Last() == this;

        public Declaration(CppContext context, CXCursor cursor)
        {
            this.Cursor = cursor;
            this.Filename = context.Filename;

            this.Name = cursor.Spelling.CString;
            this.FullName = this.checkFullName();
            this.DisplayString = cursor.DisplayName.CString;

            CXFile file;
            uint line, column, offset;
            cursor.Extent.Start.GetFileLocation(out file, out line, out column, out offset);
            this.Index = (int)offset; // context.FileContent.Locate((int)line, (int)column);

            cursor.Extent.End.GetFileLocation(out file, out line, out column, out offset);
            this.CloseIndex = (int)offset; // context.FileContent.Locate((int)line, (int)column);
            this.Content = this.Index > 0 && this.CloseIndex > this.Index ? context.FileContent.Substring(this.Index, this.CloseIndex - this.Index) : "";

            this.checkExport(context);
        }

        protected virtual string checkFullName() => this.Cursor.GetFullTypeName();

        private bool checkExport(CppContext context)
        {
            this.ShouldExport = false;
            if (string.IsNullOrWhiteSpace(this.ExportMacro))
            {
                return false;
            }

            int previousCloseIndex = Math.Max(context.FileContent.LastIndexOfAny("{});".ToCharArray(), this.Index), 0);
            int exportIndex = context.FileContent.LastIndexOf(this.ExportMacro, previousCloseIndex);
            if (exportIndex < 0)
            {
                return false;
            }

            int openIndex = context.FileContent.IndexOf('(', exportIndex);
            if (-1 == openIndex)
            {
                return false;
            }

            int closeIndex = context.FileContent.LastIndexOf(')', this.Index, this.Index - openIndex);
            if (-1 == closeIndex)
            {
                return false;
            }

            string exportContent = this.ExportContent = context.FileContent.Substring(openIndex + 1, closeIndex - openIndex - 1);
            string[] exportParts = this.ExportContents = exportContent.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
            this.checkExportContents(exportParts);
            this.ShouldExport = true;
            return true;
        }

        protected virtual void checkExportContents(string[] contents) { }

        protected internal void setShouldExport(bool recursive)
        {
            this.ShouldExport = true;

            if (recursive)
            {
                DeclarationCollection? collection = this as DeclarationCollection;
                if (collection is not null)
                {
                    foreach (Declaration declaration in collection.Declarations)
                    {
                        declaration.setShouldExport(recursive);
                    }
                }
            }
        }

        protected internal virtual void setParent(DeclarationCollection? parent)
        {
            this.Parent = parent;
            if (parent is not null)
            {
                this.Root = parent.Root;
            }
        }

        public void Analyze(CppContext context, CXCursor cursor)
        {
            internalAnalyze(context, cursor);
        }

        protected virtual void internalAnalyze(CppContext context, CXCursor cursor) { }

        public virtual string ToCppCode()
        {
            return $"{this.Name}";
        }

        public virtual string ToCSharpCode()
        {
            return $"{this.Name}";
        }

        public virtual XmlElement ToXml(XmlDocument document)
        {
            XmlElement element = document.CreateElement(this.GetType().Name);
            element.SetAttribute(nameof(Name), this.Name);
            this.makeXml(element);
            return element;
        }

        protected abstract void makeXml(XmlElement element);

        protected virtual Type? getDeclaredType() => null;

        private CXCursor findCursorUpward(CXCursor startCursor, CXCursorKind kind, string name)
        {
            CXCursor cursor = startCursor;
            while (!string.IsNullOrWhiteSpace(cursor.DisplayName.CString) && (cursor.DisplayName.CString != name || kind != cursor.kind))
            {
                if (cursor.NumDecls > 0)
                {
                    for (uint i = 0; i < cursor.NumDecls; ++i)
                    {
                        CXCursor child = cursor.GetDecl(i);
                        if (child.DisplayName.CString == name && kind == child.kind)
                        {
                            return child;
                        }
                    }
                }
                cursor = cursor.SemanticParent;
            }
            return cursor.DisplayName.CString == name && kind == cursor.kind ? cursor : new CXCursor();
        }

        private Type? findTypeUpward(CppContext context, string name)
        {
            if (this.Name == name && (this is Class || this is Struct || this is Enum))
            {
                return this.getDeclaredType() ?? throw new InvalidOperationException();
            }

            CXCursor cursor = this.findCursorUpward(this.Cursor, CXCursorKind.CXCursor_ClassDecl, name);
            if (cursor.DisplayName.CString == name && CXCursorKind.CXCursor_ClassDecl == cursor.kind)
            {
                return context.GetDeclaration(cursor.GetFullTypeName())?.getDeclaredType();
            }

            return null;
        }

        internal Type FindType(CppContext context, string name)
        {
            string safename = name.TrimEnd('&', '*');
            switch (safename)
            {
                case "void": return typeof(void);
                case "int": return typeof(int);
                case "float": return typeof(float);
                case "double": return typeof(double);
            }

            return this.findTypeUpward(context, safename) ?? throw new InvalidOperationException("Should handle this type if it is valid");
        }

        public override string ToString()
        {
            return $"{this.GetType().Name} {this.Name}";
        }

        static internal Declaration? Create(CppContext context, CXCursor cursor)
        {
            switch (cursor.kind)
            {
                case CXCursorKind.CXCursor_Namespace: return new Namespace(context, cursor);
                case CXCursorKind.CXCursor_ClassDecl: return new Class(context, cursor);
                case CXCursorKind.CXCursor_Constructor: return new Constructor(context, cursor);
                case CXCursorKind.CXCursor_FunctionDecl:
                case CXCursorKind.CXCursor_CXXMethod:
                    return new Function(context, cursor);
                case CXCursorKind.CXCursor_StructDecl: return new Struct(context, cursor);
                case CXCursorKind.CXCursor_EnumDecl: return new Enum(context, cursor);
                case CXCursorKind.CXCursor_EnumConstantDecl: return new EnumConstant(context, cursor);
                case CXCursorKind.CXCursor_FieldDecl: return new Field(context, cursor);
            }
            return null;
        }
    }

    internal abstract class DeclarationCollection : Declaration
    {
        public List<Declaration> Declarations { get; init; } = new List<Declaration>();

        public DeclarationCollection(CppContext context, CXCursor cursor) : base(context, cursor) { }

        public void AddDeclaration(Declaration declaration)
        {
            declaration.setParent(this);
            this.Declarations.Add(declaration);

            if (!this.ShouldExport && declaration.ShouldExport)
            {
                Declaration? current = this;
                do
                {
                    current.setShouldExport(false);
                    current = current.Parent;

                } while (current is not null);
            }
            if (this.ShouldExport && !declaration.ShouldExport)
            {
                declaration.setShouldExport(true);
            }
        }

        public Declaration? GetDeclaration(string name)
        {
            return this.Declarations.Find(d => d.Name == name);
        }
    }

    internal class Namespace : DeclarationCollection
    {
        public override string? ExportMacro => null;

        public Namespace(CppContext context, CXCursor cursor) : base(context, cursor) { }

        protected override string checkFullName() => this.Cursor.GetFullNamespace();

        public void SetAsRoot()
        {
            this.Root = this;
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
        }
    }

    internal class Argument : Declaration
    {
        public override string? ExportMacro => null;

        public CXType CXType { get; init; }
        public string TypeString { get; init; }
        public Type Type { get; init; }

        public bool IsPointer => CXTypeKind.CXType_Pointer == this.CXType.kind || CXTypeKind.CXType_ObjCObjectPointer == this.CXType.kind;
        public bool IsLeftValueReference => CXTypeKind.CXType_LValueReference == this.CXType.kind;
        public bool IsRightValueReference => CXTypeKind.CXType_RValueReference == this.CXType.kind;
        public bool IsReference => this.IsLeftValueReference || this.IsRightValueReference;

        public Argument(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.CXType = cursor.Type;
            this.TypeString = this.CXType.PointeeType.Desugar.Spelling.CString;
            this.Type = this.FindType(context, this.TypeString);
        }

        protected override void internalAnalyze(CppContext context, CXCursor cursor)
        {
            base.internalAnalyze(context, cursor);
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(Type), this.Type.FullName);

            List<string> decorations = new List<string>();
            if (this.IsPointer)
            {
                decorations.Add("Pointer");
            }
            if (this.IsLeftValueReference)
            {
                decorations.Add("LValueReference");
            }
            if (this.IsRightValueReference)
            {
                decorations.Add("RValueReference");
            }
            element.SetAttribute(KEY_DECORATIONS, string.Join('|', decorations));
        }

        public override string ToCppCode()
        {
            return $"{this.Type.ToCppTypeString()} {this.Name}";
        }

        public override string ToCSharpCode()
        {
            return $"{this.Type.ToCSharpTypeString()} {this.Name}";
        }
    }

    internal class Function : Declaration
    {
        private const string VARIABLE_NAME_INSTANCE = "instance";
        private const string VARIABLE_NAME_DERIVED_INSTANCE = "derived";

        private const int INDEX_EXPORT_NAME = 0;
        private const int INDEX_EXPORT_DEFAULT_VALUE = 1;

        static public readonly Regex ExportExpression = new Regex($@"{CppAnalyzer.ExportFunctionMacro}\((.*)\)\s*;");

        public override string? ExportMacro => CppAnalyzer.ExportFunctionMacro;

        public Type ReturnType { get; protected set; }

        public string? BindingName => this.ExportContents.Length > INDEX_EXPORT_NAME ? this.ExportContents[INDEX_EXPORT_NAME] : null;
        public string? DefaultReturnValue => this.ExportContents.Length > INDEX_EXPORT_DEFAULT_VALUE ? this.ExportContents[INDEX_EXPORT_DEFAULT_VALUE] : null;

        public Argument[] Arguments { get; init; }

        public Function(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.ReturnType = this.FindType(context, cursor.ReturnType.Spelling.CString);

            List<Argument> arguments = new List<Argument>();
            for (uint i = 0; i < cursor.NumArguments; ++i)
            {
                arguments.Add(new Argument(context, cursor.GetArgument(i)));
            }
            this.Arguments = arguments.ToArray();
        }

        protected override string checkFullName() => $"{this.Cursor.SemanticParent.GetFullTypeName()}::{this.Name}";

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(ReturnType), this.ReturnType?.FullName);
            element.SetAttribute(nameof(BindingName), this.BindingName);

            foreach (Argument argument in this.Arguments)
            {
                element.AppendChild(argument.ToXml(element.OwnerDocument));
            }
        }

        public string MakeCppExportDeclaration()
        {
            Type returnType = this.checkCppExportReturnType();
            string exportName = this.checkCppExportFunctionName();
            List<string> arguments = this.checkCppExportArguments();
            return $"__declspec (dllexport) {returnType.ToCppTypeString()} {exportName}({string.Join(", ", arguments)})";
        }

        protected virtual Type checkCppExportReturnType() => this.ReturnType;

        protected virtual string checkCppExportFunctionName()
        {
            Class? parent = this.Parent as Class;
            return parent is null
                ? (this.BindingName ?? throw new InvalidOperationException("This function might not be marked as an exported function"))
                : $"{parent.BindingPrefix}_{this.BindingName}";
        }

        protected virtual List<string> checkCppExportArguments()
        {
            List<string> arguments = this.Arguments.Select(a => a.ToCppCode()).ToList(); ;
            arguments.Insert(0, $"{(this.Parent as Class ?? throw new InvalidOperationException()).ExportAsType.MakePointerType().ToCppTypeString()} {VARIABLE_NAME_INSTANCE}");
            return arguments;
        }

        public virtual List<string> MakeCppExportDefinition()
        {
            string instanceName = VARIABLE_NAME_INSTANCE;
            List<string> contents = new List<string>();
            string returnContent = typeof(void) == this.ReturnType ? "return" : $"return {this.DefaultReturnValue ?? throw new InvalidOperationException()}";
            contents.Add($"if (!{instanceName}) {returnContent};");

            Class? parent = this.Parent as Class;
            if (parent is not null)
            {
                if (parent.Type != parent.ExportAsType)
                {
                    string derivedName = VARIABLE_NAME_DERIVED_INSTANCE;
                    contents.Add("");
                    contents.Add($"{parent.Type.MakePointerType().ToCppTypeString()} {derivedName} = dynamic_cast<{parent.Type.MakePointerType().ToCppTypeString()}>({instanceName});");
                    contents.Add($"if (!{derivedName}) {returnContent};");
                    contents.Add("");
                    instanceName = derivedName;
                }
            }

            if (typeof(void) == this.ReturnType)
            {
                contents.Add($"{instanceName}->{this.Name}({string.Join(", ", this.Arguments.Select(a => a.Name))});");
            }
            else
            {
                contents.Add($"return {instanceName}->{this.Name}({string.Join(", ", this.Arguments.Select(a => a.Name))});");
            }
            return contents;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, return: {this.ReturnType.FullName}";
        }
    }

    internal class Constructor : Function
    {
        static public readonly Regex ExportConstructorExpression = new Regex($@"{CppAnalyzer.ExportConstructorMacro}\((.*)\)\s*;");

        public override string? ExportMacro => CppAnalyzer.ExportConstructorMacro;

        public Constructor(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.ReturnType = this.FindType(context, cursor.SemanticParent.Spelling.CString);
        }

        protected override Type checkCppExportReturnType() => this.ReturnType.MakePointerType();

        protected override string checkCppExportFunctionName() => this.BindingName ?? throw new InvalidOperationException("This function might not be marked as an exported function");

        protected override List<string> checkCppExportArguments() => this.Arguments.Select(a => a.ToCppCode()).ToList();

        public override List<string> MakeCppExportDefinition()
        {
            List<string> contents = new List<string>();
            contents.Add($"return new {this.ReturnType.FullName}({string.Join(", ", this.Arguments.Select(a => a.Name))});");
            return contents;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name} {this.Name}";
        }
    }

    internal class Class : DeclarationCollection
    {
        public override string? ExportMacro => CppAnalyzer.ExportClassMacro;
        private const int INDEX_EXPORT_AS = 0;
        private const int INDEX_EXPORT_PREFIX = 1;

        public Type Type { get; private set; }
        public Type ExportAsType { get; private set; }

        public string? BindingPrefix => this.ExportContents.Length > INDEX_EXPORT_PREFIX ? this.ExportContents[INDEX_EXPORT_PREFIX] : "";

        public Class(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.Type = new CppType(this);

            string exportString = this.ExportContents[INDEX_EXPORT_AS];
            this.ExportAsType = this.Name == exportString ? this.Type : this.FindType(context, exportString);
        }

        protected override Type? getDeclaredType() => this.Type;

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
            element.SetAttribute(nameof(BindingPrefix), this.BindingPrefix);
        }
    }

    internal class Struct : DeclarationCollection
    {
        public override string? ExportMacro => CppAnalyzer.ExportStructMacro;

        public Struct(CppContext context, CXCursor cursor) : base(context, cursor) { }

        protected override Type? getDeclaredType() => throw new NotImplementedException();

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
        }
    }

    internal class Field : Declaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportFieldMacro;

        public Type Type { get; init; }

        public Field(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.Type = this.FindType(context, cursor.GetFullTypeName());
        }

        protected override string checkFullName() => $"{this.Cursor.GetFullTypeName()}::{this.Name}";

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(Type), this.Type.FullName);
        }
    }

    internal class Enum : DeclarationCollection
    {
        public override string? ExportMacro => CppAnalyzer.ExportEnumMacro;

        public string[] Values { get; private set; } = new string[0];

        public Enum(CppContext context, CXCursor cursor) : base(context, cursor) { }

        protected override Type? getDeclaredType() => throw new NotImplementedException();

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);

            foreach (string value in this.Values)
            {
                XmlElement e = element.OwnerDocument.CreateElement("Field");
                e.InnerText = value;
                element.AppendChild(e);
            }
        }
    }

    internal class EnumConstant : Declaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportEnumValueMacro;

        public string Value { get; init; }

        public EnumConstant(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.Value = cursor.EnumDecl_IntegerType.HasSign() ? cursor.EnumConstantDeclValue.ToString() : cursor.EnumConstantDeclUnsignedValue.ToString();
        }

        protected override string checkFullName() => $"{this.Cursor.GetFullTypeName()}::{this.Name}";

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(this.Value), this.Value);
        }
    }
}
