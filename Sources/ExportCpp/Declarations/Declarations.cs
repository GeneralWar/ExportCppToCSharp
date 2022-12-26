using ClangSharp.Interop;
using ExportCpp.Cpps;
using General;
using System.Text.RegularExpressions;
using System.Xml;
using Type = System.Type;

namespace ExportCpp
{
    internal class DeclarationType
    {
        public CXType CXType { get; }
        /// <summary>
        /// declared cpp type
        /// </summary>
        public Type DeclaredType { get; }
        public string CSharpTypeString { get; }
        public string CSharpUnmanagedTypeString { get; }

        public bool IsUnsafe => this.CSharpTypeString.Contains('*') || this.CSharpTypeString.Contains("unmanaged") || (this.DeclaredType as CppType)?.Declaration is FunctionPointer;

        public DeclarationType(CXType type, Type declaredType, string csharpTypeString, string csharpUnmanagedTypeString)
        {
            this.CXType = type;
            this.DeclaredType = declaredType;
            this.CSharpTypeString = csharpTypeString;
            this.CSharpUnmanagedTypeString = csharpUnmanagedTypeString;
        }

        public override string ToString()
        {
            return $"{this.DeclaredType.FullName} -> {this.CSharpTypeString}";
        }
    }

    interface ITypeDeclaration
    {
        string Name { get; }
        string FullName { get; }

        DeclarationType Type { get; }

        bool MatchTypeName(string name);
    }

    internal abstract class Declaration
    {
        protected const string KEY_DECORATIONS = "Decorations";

        public CXCursor Cursor { get; protected set; }

        public Namespace? Root { get; protected set; }
        public string Filename { get; init; }

        public string Name { get; init; }
        public string FullName { get; private set; }
        public string DisplayString { get; private set; }

        public string Content { get; protected set; }

        public int Index { get; private set; }
        public int CloseIndex { get; protected set; }

        public abstract string? ExportMacro { get; }
        public bool ShouldExport { get; private set; }
        public string? ExportContent { get; private set; }
        public string[] ExportContents { get; private set; } = new string[0];

        public DeclarationCollection? Parent { get; private set; }

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
            this.Index = context.FileContent.Locate((int)line, (int)column); // offset is not accurate

            cursor.Extent.End.GetFileLocation(out file, out line, out column, out offset);
            this.CloseIndex = context.FileContent.Locate((int)line, (int)column); // offset is not accurate
            this.Content = this.Index > 0 && this.CloseIndex > this.Index ? context.FileContent.Substring(this.Index, this.CloseIndex - this.Index) : "";

            this.checkExport(context);
        }

        internal Declaration(string name)
        {
            this.FullName = this.DisplayString = this.Name = name;
            this.Filename = this.Content = "";
        }

        protected virtual string checkFullName() => this.Cursor.GetFullTypeName();

        private bool checkExport(CppContext context)
        {
            this.ShouldExport = false;
            if (string.IsNullOrWhiteSpace(this.ExportMacro))
            {
                return false;
            }

            string previousContent = CppAnalyzer.CheckContentFromPreviousCursor(context, this.Cursor);
            int exportIndex = previousContent.LastIndexOf(this.ExportMacro);
            if (exportIndex < 0)
            {
                return false;
            }

            int openIndex = previousContent.IndexOf('(', exportIndex);
            if (-1 == openIndex)
            {
                return false;
            }

            int closeIndex = previousContent.LastIndexOf(')');
            if (-1 == closeIndex)
            {
                return false;
            }

            string exportContent = this.ExportContent = previousContent.Substring(openIndex + 1, closeIndex - openIndex - 1);
            string[] exportParts = this.ExportContents = CppAnalyzer.SplitArguments(exportContent);
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

                if (this.Cursor.IsInvalid)
                {
                    this.updateFullNameAndDisplayString();
                }
            }
        }

        protected internal void updateFullNameAndDisplayString()
        {
            this.FullName = this.DisplayString = this.Parent is null ? this.Name : $"{this.Parent.FullName}::{this.Name}";
        }

        public void Merge(Declaration other)
        {
            if (this.GetType() != other.GetType())
            {
                throw new InvalidOperationException();
            }

            this.Content = other.Content;

            this.Index = other.Index;
            this.CloseIndex = other.CloseIndex;

            this.ShouldExport = other.ShouldExport;
            this.ExportContent = other.ExportContent;
            this.ExportContents = other.ExportContents;
            this.Parent = other.Parent;

            this.internalMerge(other);
        }

        protected virtual void internalMerge(Declaration other) { }

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
                        if (child.kind == kind && child.MatchTypeName(name))
                        {
                            return child;
                        }
                    }
                }
                cursor = cursor.SemanticParent;
            }
            return cursor.DisplayName.CString == name && kind == cursor.kind ? cursor : CXCursor.Null;
        }

        static internal Declaration? FindDeclarationUpwards(Declaration? start, string name)
        {
            for (Declaration? declaration = start; declaration is not null; declaration = declaration.Parent)
            {
                if ((declaration as ITypeDeclaration)?.MatchTypeName(name) ?? false)
                {
                    return declaration;
                }

                DeclarationCollection? collection = declaration as DeclarationCollection;
                if (collection is not null)
                {
                    foreach (Declaration child in collection.Declarations)
                    {
                        if ((child as ITypeDeclaration)?.MatchTypeName(name) ?? false)
                        {
                            return child;
                        }
                    }
                }
            }
            return null;
        }

        private Declaration? findTypeUpward(CppContext context, string name)
        {
            if (this.Name == name && (this is Class || this is Struct || this is Enum))
            {
                return this;
            }

            Declaration? declaration = FindDeclarationUpwards(this.Parent, name);
            declaration ??= FindDeclarationUpwards(context.CurrentScope, name);
            if (declaration is null)
            {
                CXCursor cursor = this.findCursorUpward(this.Cursor, CXCursorKind.CXCursor_ClassDecl, name);
                if (cursor.MatchTypeName(name) && CXCursorKind.CXCursor_ClassDecl == cursor.kind)
                {
                    declaration = context.GetDeclaration(cursor.GetFullTypeName());
                }
            }
            return declaration;
        }

        internal Type? FindType(CppContext context, CXType type)
        {
            if (CXTypeKind.CXType_Invalid == type.kind)
            {
                return null;
            }

            if (CXTypeKind.CXType_Pointer == type.kind)
            {
                /*if (CXTypeKind.CXType_Record == type.PointeeType.kind)
                {
                    return typeof(void*);
                }
                else */
                if (CXTypeKind.CXType_FirstBuiltin <= type.PointeeType.kind && type.PointeeType.kind <= CXTypeKind.CXType_LastBuiltin)
                {
                    return type.PointeeType.ToBuiltinType().MakePointerType();
                }
            }

            if (CXTypeKind.CXType_FirstBuiltin <= type.kind && type.kind <= CXTypeKind.CXType_LastBuiltin)
            {
                return type.ToBuiltinType();
            }

            return this.FindType(context, type.GetOriginalTypeName());
        }

        internal Type? FindType(CppContext context, string name)
        {
            Type? type = name.TypeFromCppString();
            if (type is not null)
            {
                return type;
            }

            string safename = name.Trim();
            Declaration? declaration = this.findTypeUpward(context, safename);
            declaration ??= this.Root?.GetDeclaration(safename);
            declaration ??= this.Root == context.Global ? null : context.Global.GetDeclaration(safename);
            declaration ??= context.GetDeclaration(safename);
            return (declaration as ITypeDeclaration)?.Type.DeclaredType;
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
                case CXCursorKind.CXCursor_TypedefDecl:
                    if (CX_TypeClass.CX_TypeClass_FunctionProto == cursor.Type.PointeeType.TypeClass)
                    {
                        return new FunctionPointer(context, cursor);
                    }
                    break;
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
        public List<Declaration> TypeDefines { get; init; } = new List<Declaration>();

        public DeclarationCollection(CppContext context, CXCursor cursor) : base(context, cursor) { }

        public DeclarationCollection(string name) : base(name) { }

        public void AddDeclaration(Declaration declaration)
        {
            declaration.setParent(this);
            this.Declarations.Add(declaration);

            if (CXCursorKind.CXCursor_TypedefDecl == declaration.Cursor.kind)
            {
                this.TypeDefines.Add(declaration);
            }

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

        protected internal override void setParent(DeclarationCollection? parent)
        {
            base.setParent(parent);

            foreach (Declaration child in this.Declarations)
            {
                child.updateFullNameAndDisplayString();
            }
        }

        protected override void internalMerge(Declaration other)
        {
            base.internalMerge(other);

            DeclarationCollection? source = other as DeclarationCollection ?? throw new InvalidOperationException();
            foreach (Declaration child in source.Declarations)
            {
                this.AddDeclaration(child);
            }
            source.Declarations.Clear();
        }
    }

    internal class Namespace : DeclarationCollection
    {
        public override string? ExportMacro => null;

        public Namespace(CppContext context, CXCursor cursor) : base(context, cursor) { }

        internal Namespace(string name) : base(name) { }

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

        public DeclarationType Type { get; init; }

        public bool IsPointer => CXTypeKind.CXType_Pointer == this.Type.CXType.kind || CXTypeKind.CXType_ObjCObjectPointer == this.Type.CXType.kind;
        public bool IsLeftValueReference => CXTypeKind.CXType_LValueReference == this.Type.CXType.kind;
        public bool IsRightValueReference => CXTypeKind.CXType_RValueReference == this.Type.CXType.kind;
        public bool IsReference => this.IsLeftValueReference || this.IsRightValueReference;

        public Argument(CppContext context, CXCursor cursor) : this(context, cursor, cursor.Type) { }

        public Argument(CppContext context, CXType type, string name) : this(context, CXCursor.Null, type)
        {
            this.Name = name;
        }

        public Argument(CppContext context, CXCursor cursor, CXType type) : base(context, cursor)
        {
            Type declaredType = this.FindType(context, type) ?? throw new InvalidOperationException($"There is no type {type.GetOriginalTypeName()}");
            this.Type = new DeclarationType(type, declaredType, declaredType.ToCSharpTypeString(), declaredType.ToCSharpUnmanagedTypeString());
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(CppType), this.Type.DeclaredType.FullName);

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
            if (decorations.Count > 0)
            {
                element.SetAttribute(KEY_DECORATIONS, string.Join('|', decorations));
            }
        }

        public override string ToCppCode()
        {
            return $"{this.Type.DeclaredType.ToCppTypeString()} {this.Name}";
        }

        public override string ToCSharpCode()
        {
            return $"{this.Type.DeclaredType.ToCSharpTypeString()} {this.Name}";
        }

        public override string ToString()
        {
            return $"{nameof(Argument)} {this.Type.CXType.Spelling} {this.Name}";
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
            this.ReturnType = this.checkReturnType(context);

            List<Argument> arguments = new List<Argument>();
            for (uint i = 0; i < cursor.NumArguments; ++i)
            {
                arguments.Add(new Argument(context, cursor.GetArgument(i)));
            }
            this.Arguments = arguments.ToArray();
        }

        protected virtual Type checkReturnType(CppContext context) => this.FindType(context, this.Cursor.ReturnType) ?? throw new InvalidOperationException($"There is no type {this.Cursor.ReturnType.GetOriginalTypeName()}");
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
            arguments.Insert(0, $"{(this.Parent as Class ?? throw new InvalidOperationException()).ExportAsType?.MakePointerType().ToCppTypeString()} {VARIABLE_NAME_INSTANCE}");
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
                if (parent.Type.DeclaredType != parent.ExportAsType)
                {
                    string derivedName = VARIABLE_NAME_DERIVED_INSTANCE;
                    contents.Add("");
                    contents.Add($"{parent.Type.DeclaredType.MakePointerType().ToCppTypeString()} {derivedName} = dynamic_cast<{parent.Type.DeclaredType.MakePointerType().ToCppTypeString()}>({instanceName});");
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

        public string MakeCSharpBindingDeclaration()
        {
            if (string.IsNullOrWhiteSpace(this.BindingName))
            {
                throw new InvalidOperationException("Function has no export or binding flag");
            }

            bool hasUnsafeArgument = false;
            List<string> arguments = new List<string>();
            foreach (Argument argument in this.Arguments)
            {
                arguments.Add(argument.ToCSharpCode());
                hasUnsafeArgument = hasUnsafeArgument || argument.Type.IsUnsafe;
            }

            string bindingName;
            Class? parent = this.Parent as Class;
            if (this is Constructor)
            {
                bindingName = this.BindingName;
            }
            else
            {
                arguments.Insert(0, $"{typeof(IntPtr).ToCSharpTypeString()} instance");
                bindingName = parent is null ? this.BindingName : $"{parent.BindingPrefix}_{this.BindingName}";
            }

            return $"static internal extern {(hasUnsafeArgument ? "unsafe " : "")}{this.ReturnType.ToCSharpTypeString()} {bindingName}({string.Join(", ", arguments)});";
        }

        public override string ToString()
        {
            return $"{base.ToString()}, return: {this.ReturnType.FullName}";
        }
    }

    internal class FunctionPointer : Function, ITypeDeclaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportFunctionPointerMacro;

        public DeclarationType Type { get; init; } = new DeclarationType(new CXType(), null, null, null);

        public string FunctionTypeString => this.Type.CXType.PointeeType.Spelling.CString;

        public FunctionPointer(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            CXType pointeeType = cursor.Type.PointeeType;
            // fix end index and content, it will be wrong if call convention is set
            int closeIndex = context.FileContent.IndexOf(';', this.Index + 1);
            if (this.CloseIndex != closeIndex)
            {
                this.CloseIndex = closeIndex;
                this.Content = this.Index > 0 && this.CloseIndex > this.Index ? context.FileContent.Substring(this.Index, this.CloseIndex - this.Index + 1) : "";
            }

            int argumentCount = pointeeType.NumArgTypes;
            string csharpTypeString = $"delegate* unmanaged[Cdecl]<{this.ReturnType.ToUnmanagedString()}>";
            if (argumentCount > 0)
            {
                string[] argumentContents = CppAnalyzer.SplitArguments(this.Content.Substring(this.Content.IndexOf(')') + 1).Trim());
                Tracer.Assert(argumentContents.Length == argumentCount);

                List<Argument> arguments = new List<Argument>();
                List<string> unmanagedArguments = new List<string>();
                for (uint i = 0; i < pointeeType.NumArgTypes; ++i)
                {
                    Argument argument = new Argument(context, pointeeType.GetArgType(i), CppAnalyzer.CheckArgumentName(argumentContents[i]));
                    unmanagedArguments.Add(argument.Type.CSharpUnmanagedTypeString);
                    arguments.Add(argument);
                }
                this.Arguments = arguments.ToArray();

                csharpTypeString = $"delegate* unmanaged[Cdecl]<{string.Join(", ", unmanagedArguments)}, {this.ReturnType.ToUnmanagedString()}>";
            }

            this.Type = new DeclarationType(cursor.Type, new CppType(this), csharpTypeString, csharpTypeString);
        }
        protected override Type checkReturnType(CppContext context) => this.FindType(context, this.Cursor.Type.PointeeType.ResultType) ?? throw new InvalidOperationException($"There is no type {this.Cursor.Type.PointeeType.ResultType.GetOriginalTypeName()}");

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith(name) || this.FunctionTypeString == name;
        }
    }

    internal class Constructor : Function
    {
        static public readonly Regex ExportConstructorExpression = new Regex($@"{CppAnalyzer.ExportConstructorMacro}\((.*)\)\s*;");

        public override string? ExportMacro => CppAnalyzer.ExportConstructorMacro;

        public Constructor(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.ReturnType = this.FindType(context, cursor.SemanticParent.GetFullTypeName()) ?? throw new InvalidOperationException($"There is no type {cursor.SemanticParent.GetFullTypeName()}"); ;
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

    internal class Class : DeclarationCollection, ITypeDeclaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportClassMacro;
        private const int INDEX_EXPORT_AS = 0;
        private const int INDEX_EXPORT_PREFIX = 1;

        public DeclarationType Type { get; init; } = new DeclarationType(new CXType(), null, null, null);
        public Type? ExportAsType { get; private set; }

        public string? BindingPrefix => this.ExportContents.Length > INDEX_EXPORT_PREFIX ? this.ExportContents[INDEX_EXPORT_PREFIX] : "";

        public Class(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.Type = new DeclarationType(cursor.Type, new CppType(this), typeof(IntPtr).ToCSharpTypeString(), typeof(IntPtr).ToCSharpUnmanagedTypeString());

            if (this.ExportContents.Length > INDEX_EXPORT_AS)
            {
                string exportString = this.ExportContents[INDEX_EXPORT_AS];
                this.ExportAsType = this.Name == exportString ? this.Type.DeclaredType : this.FindType(context, exportString) ?? throw new InvalidOperationException($"There is no type {exportString}, mark it with {CppAnalyzer.ExportClassMacro}|{CppAnalyzer.ExportStructMacro}|{CppAnalyzer.ExportEnumMacro} and make sure it is processed in front of this type");
            }
        }

        internal Class(string name, Type csharpType, Type exportAsType) : base(name)
        {
            this.Type = new DeclarationType(new CXType(), new CppType(this), csharpType.ToCSharpTypeString(), csharpType.ToCSharpUnmanagedTypeString());
            this.ExportAsType = exportAsType;
        }

        protected override void internalMerge(Declaration other)
        {
            base.internalMerge(other);

            if (this.Cursor.IsCanonical)
            {
                this.Cursor = other.Cursor;
            }
        }

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith(name);
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
            element.SetAttribute(nameof(BindingPrefix), this.BindingPrefix);
        }
    }

    internal class Struct : DeclarationCollection, ITypeDeclaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportStructMacro;

        public DeclarationType Type { get; init; } = new DeclarationType(new CXType(), null, null, null);

        public Struct(CppContext context, CXCursor cursor) : base(context, cursor) { }

        protected override Type? getDeclaredType() => throw new NotImplementedException();

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith(name);
        }

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
            // check if cursor should be replaced by type
            this.Type = this.FindType(context, cursor.GetFullTypeName()) ?? throw new InvalidOperationException($"There is no type {cursor.GetFullTypeName()}"); ;
        }

        protected override string checkFullName() => $"{this.Cursor.GetFullTypeName()}::{this.Name}";

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(Type), this.Type.FullName);
        }
    }

    internal class Enum : DeclarationCollection, ITypeDeclaration
    {
        private const int INDEX_NAME = 0;

        public override string? ExportMacro => CppAnalyzer.ExportEnumMacro;

        public string[] Values { get; private set; } = new string[0];

        public DeclarationType Type { get; init; }

        public Enum(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            string csharpTypeString = this.ExportContents.Length > INDEX_NAME ? this.ExportContents[INDEX_NAME] : "";
            this.Type = new DeclarationType(cursor.Type, new CppType(this), csharpTypeString, csharpTypeString);
        }

        protected override Type? getDeclaredType() => throw new NotImplementedException();

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith(name);
        }

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

        public override string ToCSharpCode()
        {
            return $"{this.Name} = {this.Value}";
        }
    }
}
