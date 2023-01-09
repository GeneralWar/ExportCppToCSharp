﻿using ClangSharp.Interop;
using General;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Type = System.Type;

namespace ExportCpp
{
    public class DeclarationType
    {
        public CXType CXType { get; }
        /// <summary>
        /// declared cpp type
        /// </summary>
        public Type DeclaredType { get; }

        public DeclarationType(CXType type, Type declaredType)
        {
            this.CXType = type;
            this.DeclaredType = declaredType;
        }

        public override string ToString()
        {
            return $"{this.DeclaredType.FullName} -> {this.DeclaredType.ToCSharpTypeString()}";
        }
    }

    public interface ITypeDeclaration
    {
        string Name { get; }
        string FullName { get; }

        DeclarationType Type { get; }
        public string CSharpTypeString { get; }
        public string CSharpUnmanagedTypeString { get; }

        bool MatchTypeName(string name);

        string ToCppExportArgumentTypeString();
        bool CheckCppShouldCastExportArgumentTypeToInvocationType();
        string? ToCppExportArgumentCastString(string argumentName, string targetName);
        string? ToCppExportInvocationCastString(string content);
        string ToCppExportReturnTypeString();
        string ToCppExportReturnValueString(string content);
    }

    public interface ITemplateTypeDeclaration : ITypeDeclaration
    {
        string ToCppExportArgumentTypeString(Type[] arguments);
        bool CheckCppShouldCastExportArgumentTypeToInvocationType(Type[] arguments);
        string? ToCppExportArgumentCastString(Type[] arguments, string argumentName, string targetName);
        string? ToCppExportInvocationCastString(Type[] arguments, string content);
        string ToCppExportReturnTypeString(Type[] arguments);
        string ToCppExportReturnValueString(Type[] arguments, string content);
    }

    public abstract class Declaration
    {
        protected const string KEY_DECORATIONS = "Decorations";

        public CppAnalyzer Analyzer { get; protected set; }
        public Global Global => this.Analyzer.Global;

        public CXCursor Cursor { get; protected set; }
        public string Filename { get; init; }

        public string Name { get; init; }
        public string FullName { get; private set; }
        public string DisplayString { get; private set; }

        public string Content { get; protected set; }
        public string? CommentText { get; }

        public int Index { get; private set; }
        public int CloseIndex { get; protected set; }

        public abstract string? ExportMacro { get; }
        public virtual bool ShouldExport => true;
        public string? ExportContent { get; private set; }
        public string[] ExportContents { get; private set; } = new string[0];

        private bool mIsAnalyzed = false;

        public DeclarationCollection? Parent { get; private set; }

        internal unsafe Declaration(CppContext context, CXCursor cursor)
        {
            this.Cursor = cursor;
            this.Analyzer = context.Analyzer;
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

            if (cursor.RawCommentText.data is not null)
            {
                CXCursor previousCursor = CppAnalyzer.CheckPreviousCursor(cursor, cursor.Location.GetFile());
                if (cursor.CommentRange.Start.GetOffset() > previousCursor.Location.GetOffset())
                {
                    this.CommentText = cursor.RawCommentText.CString;
                }
            }

            this.checkExport(context);
        }

        internal Declaration(CppAnalyzer analyzer, string name)
        {
            this.Analyzer = analyzer;

            this.FullName = this.DisplayString = this.Name = name;
            this.Filename = this.Content = "";
        }

        protected virtual string checkFullName() => this.Cursor.GetFullName();

        private bool checkExport(CppContext context)
        {
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
            return true;
        }

        protected virtual void checkExportContents(string[] contents) { }

        protected internal virtual void setParent(DeclarationCollection? parent)
        {
            this.Parent = parent;
            if (parent is not null)
            {
                if (!this.Cursor.IsInvalid)
                {
                    this.updateFullNameAndDisplayString();
                }
            }
        }

        protected internal void updateFullNameAndDisplayString()
        {
            this.FullName = this.DisplayString = (this.Parent is null || this.Parent == this.Global) ? this.Name : $"{this.Parent.FullName}::{this.Name}";
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

            this.ExportContent = other.ExportContent;
            this.ExportContents = other.ExportContents;

            this.internalMerge(other);
        }

        protected virtual void internalMerge(Declaration other) { }

        internal void Analyze(CppContext context)
        {
            if (mIsAnalyzed)
            {
                return;
            }

            try
            {
                this.internalAnalyze(context);
                mIsAnalyzed = true;
            }
            catch (Exception e)
            {
                context.AppendFailedDeclaration(this, e);
            }
        }

        internal void ForceAnalyze(CppContext context)
        {
            this.internalAnalyze(context);
        }

        internal virtual void internalAnalyze(CppContext context) { }

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

        internal virtual void MakeCSharpDefinition(CSharpBindingContext context)
        {
            if (!string.IsNullOrWhiteSpace(this.CommentText))
            {
                foreach (string line in this.CommentText.Split('\n'))
                {
                    context.WriteLine(line.Trim());
                }
            }

            this.internalMakeCSharpDefinition(context);
        }

        internal virtual void internalMakeCSharpDefinition(CSharpBindingContext context) { }

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

        static internal Declaration? FindDeclarationUpwards(CppContext context, CXCursor cursor, string name)
        {
            if (cursor.GetName() == name)
            {
                return context.Global.GetDeclaration(cursor.GetFullTypeName());
            }

            // TODO: should check using namespace expressions

            for (CXCursor current = cursor.SemanticParent; !current.IsInvalid; current = current.SemanticParent)
            {
                DeclarationCollection? collection = current.IsTranslationUnit ? context.Global : context.Global.GetDeclaration(current.GetFullName()) as DeclarationCollection;
                if (collection is null)
                {
                    continue;
                }

                Declaration? declaration = collection.GetDeclaration(name);
                if (declaration is not null)
                {
                    return declaration;
                }
            }
            return null;
        }

        internal Type? FindType(CppContext context, CXType type)
        {
            if (CXTypeKind.CXType_Invalid == type.kind)
            {
                return null;
            }

            ITypeDeclaration? declaration = context.Global.GetDeclaration(type.GetFullTypeName()) as ITypeDeclaration;
            if (declaration is not null)
            {
                return declaration.Type.DeclaredType;
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
                    return this.FindType(context, type.PointeeType)?.MakePointerType();
                }

                return this.FindType(context, type.PointeeType)?.MakePointerType();
            }

            if (CXTypeKind.CXType_Typedef == type.kind)
            {
                return this.FindType(context, type.Declaration.TypedefDeclUnderlyingType);
            }

            if (CXTypeKind.CXType_LValueReference == type.kind || CXTypeKind.CXType_RValueReference == type.kind)
            {
                return this.FindType(context, type.PointeeType);
            }

            if (CXTypeKind.CXType_FirstBuiltin <= type.kind && type.kind <= CXTypeKind.CXType_LastBuiltin)
            {
                return type.ToBuiltinType();
            }

            if (type.NumTemplateArguments > 0)
            {
                string templateName = type.GetTemplateName();
                Tracer.Assert(!string.IsNullOrWhiteSpace(templateName));

                ClassTemplate template = Declaration.FindDeclarationUpwards(context, this.Cursor, templateName) as ClassTemplate ?? throw new InvalidOperationException($"There is no template class {templateName}");
                return template.MakeGenericType(context, type);
            }

            return this.FindType(context, type.GetOriginalTypeName());
        }

        internal Type? FindType(CppContext context, string name)
        {
            if (this.Name == name && (this is ITypeDeclaration))
            {
                return (this as ITypeDeclaration)?.Type.DeclaredType;
            }

            Type? type = name.TypeFromCppString();
            if (type is not null)
            {
                return type;
            }

            Declaration? declaration = context.GetDeclaration(name);
            type = (declaration as ITypeDeclaration)?.Type.DeclaredType;
            if (type is null)
            {
                type = ((context.CurrentScope as DeclarationCollection)?.GetDeclaration(name) as ITypeDeclaration)?.Type.DeclaredType;
            }
            if (type is null)
            {
                declaration = Declaration.FindDeclarationUpwards(context, this.Cursor, name);
                type = (declaration as ITypeDeclaration)?.Type.DeclaredType;
                if (type is not null)
                {
                    return type;
                }
            }
            return type;
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
                case CXCursorKind.CXCursor_ClassTemplate: return new ClassTemplate(context, cursor);
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
                case CXCursorKind.CXCursor_UnexposedDecl:
                    if (CXCursorKind.CXCursor_StructDecl == cursor.SemanticParent.kind)
                    {
                        return new Field(context, cursor);
                    }
                    Debugger.Break();
                    break;
                case CXCursorKind.CXCursor_UsingDeclaration:
                    if (CXCursorKind.CXCursor_OverloadedDeclRef == cursor.Definition.kind)
                    {
                        return new Function(context, cursor);
                    }
                    Debugger.Break();
                    break;
                case CXCursorKind.CXCursor_UsingDirective:
                case CXCursorKind.CXCursor_InclusionDirective:
                    Debugger.Break();
                    break;
            }
            return null;
        }

        static internal string CheckValidCSharpVariableName(string name)
        {
            switch (name)
            {
                case "namespace":
                case "class":
                case "struct":
                case "enum":
                case "params":
                    return "@" + name;
                default: return name;
            }
        }
    }

    public abstract class DeclarationCollection : Declaration
    {
        private List<Declaration> mDeclarations = new List<Declaration>();
        public IEnumerable<Declaration> Declarations => mDeclarations;

        private List<Declaration> mTypeDefines = new List<Declaration>();
        public IEnumerable<Declaration> TypeDefines => mTypeDefines;

        private HashSet<string> mUsingNamespaces = new HashSet<string>();
        public IEnumerable<string> UsingNamespaces => mUsingNamespaces;

        internal DeclarationCollection(CppContext context, CXCursor cursor) : base(context, cursor) { }

        public DeclarationCollection(CppAnalyzer analyzer, string name) : base(analyzer, name) { }

        internal override void internalAnalyze(CppContext context)
        {
            base.internalAnalyze(context);

            foreach (Declaration declaration in mDeclarations)
            {
                declaration.Analyze(context);
            }
        }

        public void AddDeclaration(Declaration declaration)
        {
            declaration.setParent(this);
            mDeclarations.Add(declaration);

            if (CXCursorKind.CXCursor_TypedefDecl == declaration.Cursor.kind)
            {
                mTypeDefines.Add(declaration);
            }

            this.Global.RegisterDeclaration(declaration);
        }

        private void removeDeclaration(Declaration declaration)
        {
            if (CXCursorKind.CXCursor_TypedefDecl == declaration.Cursor.kind)
            {
                mTypeDefines.Remove(declaration);
            }

            mDeclarations.Remove(declaration);
            declaration.setParent(null);

            this.Global.UnregisterDeclaration(declaration);
        }

        /// <summary>
        /// Set declaration, will replace existed declarations which have the same name
        /// </summary>
        /// <param name="declaration"></param>
        public void SetDeclaration(Declaration declaration)
        {
            foreach (Declaration record in this.Declarations.Where(d => d.Name == declaration.Name).ToArray())
            {
                this.removeDeclaration(record);
            }
            this.AddDeclaration(declaration);
        }

        public Declaration? GetDeclaration(string name)
        {
            string selfPrefix = this.Name + Namespace.SEPARATOR;
            if (name.StartsWith(selfPrefix))
            {
                return this.GetDeclaration(name.Substring(selfPrefix.Length));
            }

            List<string> potentialNames = this.UsingNamespaces.Select(u => $"{u}::{name}").ToList();
            potentialNames.Insert(0, name);

            Declaration? declaration = null;
            foreach (string potentialName in potentialNames)
            {
                if (potentialName.Contains("::"))
                {
                    string[] parts = CppAnalyzer.SplitFullName(potentialName);
                    if (parts.Length > 1)
                    {
                        DeclarationCollection? item = mDeclarations.Find(d => d.Name == parts[0]) as DeclarationCollection;
                        declaration = item?.GetDeclaration(string.Join(Namespace.SEPARATOR, parts.Skip(1)));
                    }
                }
                declaration ??= mDeclarations.Find(d => d.Name == name || ((d as ITypeDeclaration)?.MatchTypeName(potentialName) ?? false));
                if (declaration is not null)
                {
                    break;
                }
            }
            return declaration;
        }

        public void AppendUsingNamespace(string @namespace)
        {
            mUsingNamespaces.Add(@namespace);
        }

        public void AppendUsingNamespace(CXCursor cursor)
        {
            if (CXCursorKind.CXCursor_UsingDirective != cursor.kind)
            {
                return;
            }

            Tracer.Assert(CXCursorKind.CXCursor_Namespace == cursor.Definition.kind);

            string fullname = cursor.Definition.GetFullName();
            this.AppendUsingNamespace(fullname);
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
            //source.Declarations.Clear();
        }
    }

    public class Namespace : DeclarationCollection
    {
        public const string SEPARATOR = "::";

        public override string? ExportMacro => null;

        internal Namespace(CppContext context, CXCursor cursor) : base(context, cursor) { }

        public Namespace(CppAnalyzer analyzer, string name) : base(analyzer, name) { }

        protected override string checkFullName() => this.Cursor.GetFullNamespace();

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
        }
    }

    public class Global : Namespace
    {
        private Dictionary<string, Declaration> mFullDeclarations = new Dictionary<string, Declaration>();

        internal Global(CppAnalyzer analyzer) : base(analyzer, "global") { }

        public void UnregisterDeclaration(Declaration declaration)
        {
            Declaration? record;
            if (mFullDeclarations.Remove(declaration.FullName, out record))
            {
                Tracer.Assert(declaration == record);
            }
        }

        public void RegisterDeclaration(Declaration declaration)
        {
            mFullDeclarations.Add(declaration.FullName, declaration);
        }
    }

    internal class Argument : Declaration
    {
        public override string? ExportMacro => null;

        public CXType CXType { get; init; }

        private DeclarationType? mType = null;
        public DeclarationType Type => mType ?? throw new InvalidOperationException($"Make sure there is a valid {nameof(DeclarationType)}, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpTypeString = null;
        public string CSharpTypeString => mCSharpTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# type string, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpUnmanagedTypeString = null;
        public string CSharpUnmanagedTypeString => mCSharpUnmanagedTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# unmanaged type string, and {nameof(Declaration.Analyze)} has been invoked");

        public bool IsPointer => CXTypeKind.CXType_Pointer == this.Type.CXType.kind || CXTypeKind.CXType_ObjCObjectPointer == this.Type.CXType.kind;
        public bool IsLeftValueReference => CXTypeKind.CXType_LValueReference == this.Type.CXType.kind;
        public bool IsRightValueReference => CXTypeKind.CXType_RValueReference == this.Type.CXType.kind;
        public bool IsReference => this.IsLeftValueReference || this.IsRightValueReference;

        public virtual string DerivedName => "derived" + char.ToUpper(this.Name[0]) + this.Name.Substring(1);

        public Argument(CppContext context, CXCursor cursor) : this(context, cursor, cursor.Type) { }

        public Argument(CppContext context, CXType type, string name) : this(context, CXCursor.Null, type)
        {
            this.Name = name;
        }

        public Argument(CppContext context, CXCursor cursor, CXType type) : base(context, cursor)
        {
            this.CXType = type;
        }

        internal override void internalAnalyze(CppContext context)
        {
            Type declaredType = this.FindType(context, this.CXType) ?? throw new InvalidOperationException($"There is no type {this.CXType.GetOriginalTypeName()}");
            mCSharpTypeString = declaredType.ToCSharpTypeString();
            mCSharpUnmanagedTypeString = declaredType.ToCSharpUnmanagedTypeString();
            mType = new DeclarationType(this.CXType, declaredType);
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

        private string checkCppExportArgumentTypeString()
        {
            Type type = this.Type.DeclaredType;
            return type.MakeCppExportArgumentTypeString();
        }

        public override string ToCppCode()
        {
            return $"{this.checkCppExportArgumentTypeString()} {this.Name}";
        }

        public string ToCppExportArgumentCode()
        {
            return $"{this.checkCppExportArgumentTypeString()} {this.Name}";
        }

        private bool checkShouldCastType()
        {
            return this.Type.DeclaredType.CheckCppShouldCastExportArgumentTypeToInvocationType();
        }

        public string? MakeDynamicCastCode()
        {
            if (!this.checkShouldCastType())
            {
                return null;
            }

            Type type = this.Type.DeclaredType;
            string? content = type.MakeCppExportArgumentCastString(this.Name, this.DerivedName);
            return string.IsNullOrWhiteSpace(content) ? null : (content + ';');
        }

        public string ToCppInvocationCode()
        {
            Type type = this.Type.DeclaredType;
            string argumentName = this.checkShouldCastType() ? this.DerivedName : this.Name;
            return type.MakeCppExportInvocationCastString(argumentName) ?? argumentName;
        }

        public override string ToCSharpCode()
        {
            return $"{this.Type.DeclaredType.ToCSharpTypeString()} {Declaration.CheckValidCSharpVariableName(this.Name)}";
        }

        public override string ToString()
        {
            return $"{nameof(Argument)} {this.CXType.Spelling} {this.Name}";
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

        public CXCursor MethodCursor { get; init; }
        public bool IsStatic => this.MethodCursor.IsStatic;

        private Type? mReturnType = null;
        public Type ReturnType => mReturnType ?? throw new InvalidOperationException($"Make sure there is a valid return type, and {nameof(Declaration.Analyze)} has been invoked");

        public string? BindingName => this.ExportContents.Length > INDEX_EXPORT_NAME ? this.ExportContents[INDEX_EXPORT_NAME] : null;
        public string? DefaultReturnValue => this.ExportContents.Length > INDEX_EXPORT_DEFAULT_VALUE ? this.ExportContents[INDEX_EXPORT_DEFAULT_VALUE] : null;

        public Argument[] Arguments { get; protected set; } = new Argument[0];

        public Function(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            this.MethodCursor = this.checkMethodCursor();
        }

        private CXCursor checkMethodCursor()
        {
            if (CXCursorKind.CXCursor_CXXMethod == this.Cursor.kind || CXCursorKind.CXCursor_Constructor == this.Cursor.kind || CXCursorKind.CXCursor_TypedefDecl == this.Cursor.kind)
            {
                return this.Cursor;
            }

            if (CXCursorKind.CXCursor_OverloadedDeclRef == this.Cursor.Definition.kind)
            {
                Tracer.Assert(1 == this.Cursor.Definition.NumOverloadedDecls);
                return this.Cursor.Definition.GetOverloadedDecl(0);
            }

            throw new NotImplementedException();
        }

        protected virtual Type checkReturnType(CppContext context)
        {
            CXType returnType = this.MethodCursor.ReturnType;
            return this.FindType(context, returnType) ?? throw new InvalidOperationException($"There is no type {this.Cursor.ReturnType.GetOriginalTypeName()}");
        }

        protected override string checkFullName() => $"{this.Cursor.SemanticParent.GetFullTypeName()}::{this.Name}";

        internal override void internalAnalyze(CppContext context)
        {
            base.internalAnalyze(context);

            mReturnType = this.checkReturnType(context);

            List<Argument> arguments = new List<Argument>();
            for (uint i = 0; i < this.MethodCursor.NumArguments; ++i)
            {
                Argument argument = new Argument(context, this.MethodCursor.GetArgument(i));
                argument.Analyze(context);
                arguments.Add(argument);
            }
            this.Arguments = arguments.ToArray();
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(ReturnType), this.ReturnType?.FullName);
            element.SetAttribute(nameof(BindingName), this.BindingName);

            foreach (Argument argument in this.Arguments)
            {
                element.AppendChild(argument.ToXml(element.OwnerDocument));
            }
        }

        protected virtual string makeCppExportDeclaration()
        {
            Type returnType = this.checkCppExportReturnType();
            string exportName = this.checkCppExportFunctionName();
            List<string> arguments = this.checkCppExportArguments();
            string prefix = "__declspec (dllexport)";
            string returnTypeString = returnType.MakeCppExportReturnTypeString().Trim();
            if (this.MethodCursor.ReturnType.IsConst() && !returnTypeString.StartsWith("const"))
            {
                prefix += " const";
            }
            return $"{prefix} {returnTypeString} {exportName}({string.Join(", ", arguments)})";
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
            List<string> arguments = this.Arguments.Select(a => a.ToCppExportArgumentCode()).ToList();
            if (!this.IsStatic)
            {
                arguments.Insert(0, $"{(this.Parent as Class ?? throw new InvalidOperationException()).ExportAsType?.MakePointerType().ToCppTypeString()} {VARIABLE_NAME_INSTANCE}");
            }
            return arguments;
        }

        public void MakeCppExportDefinition(CppExportContext context)
        {
            context.WriteLine(this.makeCppExportDeclaration());
            context.WriteLine("{");
            ++context.TabCount;

            this.makeCppExportDefinition(context);

            --context.TabCount;
            context.WriteLine("}");
        }

        protected virtual void makeCppExportDefinition(CppExportContext context)
        {
            Class? parent = this.Parent as Class;
            Type? type = parent?.Type.DeclaredType;
            Type returnType = this.ReturnType;
            string instanceName = VARIABLE_NAME_INSTANCE;

            string returnContent = typeof(void) == returnType ? "return" : $"return {this.DefaultReturnValue ?? throw new InvalidOperationException()}";
            if (!this.IsStatic)
            {
                context.WriteLine($"if (!{instanceName}) {returnContent};");

                if (parent is not null)
                {
                    if (type is null)
                    {
                        throw new InvalidOperationException("If parent is not null, there must be a declared type");
                    }

                    if (type != parent.ExportAsType)
                    {
                        string derivedName = VARIABLE_NAME_DERIVED_INSTANCE;
                        string targetType = type.MakePointerType().ToCppTypeString();
                        context.WriteLine($"{targetType} {derivedName} = dynamic_cast<{targetType}>({instanceName});");
                        context.WriteLine($"if (!{derivedName}) {returnContent};");
                        instanceName = derivedName;
                    }
                }
            }

            List<string> arguments = new List<string>();
            foreach (Argument argument in this.Arguments)
            {
                string? castCode = argument.MakeDynamicCastCode();
                if (!string.IsNullOrWhiteSpace(castCode))
                {
                    context.WriteLine(castCode);
                    //contents.Add($"if (!{argument.DerivedName}) {returnContent};");
                }

                arguments.Add(argument.ToCppInvocationCode());
            }

            string argumentContents = string.Join(", ", arguments);
            string execution = this.IsStatic ? $"{type?.ToCppTypeString()}::{this.Name}({argumentContents})" : $"{instanceName}->{this.Name}({argumentContents})";
            string content = returnType.MakeCppExportReturnValueString(execution);
            context.WriteLine(typeof(void) == returnType ? content + ";" : $"return {content};");
        }

        public string MakeCSharpBindingDeclaration()
        {
            if (string.IsNullOrWhiteSpace(this.BindingName))
            {
                throw new InvalidOperationException("Function has no export or binding flag");
            }

            List<string> arguments = new List<string>();
            foreach (Argument argument in this.Arguments)
            {
                arguments.Add(argument.ToCSharpCode());
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

            string returnTypeString = this.ReturnType.ToCSharpTypeString();
            return $"static internal extern {returnTypeString} {bindingName}({string.Join(", ", arguments)});";
        }

        public override string ToString()
        {
            return $"{base.ToString()}, return: {mReturnType?.FullName}";
        }
    }

    internal class FunctionPointer : Function, ITypeDeclaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportFunctionPointerMacro;


        protected DeclarationType? mType = null;
        public DeclarationType Type => mType ?? throw new InvalidOperationException($"Make sure there is a valid {nameof(DeclarationType)}, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpTypeString = null;
        public string CSharpTypeString => mCSharpTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# type string, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpUnmanagedTypeString = null;
        public string CSharpUnmanagedTypeString => mCSharpUnmanagedTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# unmanaged type string, and {nameof(Declaration.Analyze)} has been invoked");

        private CXType mCXType;
        public string FunctionTypeString => mCXType.PointeeType.Spelling.CString;

        public FunctionPointer(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            mCXType = cursor.Type;

            // fix end index and content, it will be wrong if call convention is set
            int closeIndex = context.FileContent.IndexOf(';', this.Index + 1);
            if (this.CloseIndex != closeIndex)
            {
                this.CloseIndex = closeIndex;
                this.Content = this.Index > 0 && this.CloseIndex > this.Index ? context.FileContent.Substring(this.Index, this.CloseIndex - this.Index + 1) : "";
            }
        }

        protected override Type checkReturnType(CppContext context) => this.FindType(context, this.Cursor.Type.PointeeType.ResultType) ?? throw new InvalidOperationException($"There is no type {this.Cursor.Type.PointeeType.ResultType.GetOriginalTypeName()}");

        internal override void internalAnalyze(CppContext context)
        {
            base.internalAnalyze(context);

            CXType pointeeType = this.Cursor.Type.PointeeType;
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
                    argument.Analyze(context);
                    unmanagedArguments.Add(argument.CSharpUnmanagedTypeString);
                    arguments.Add(argument);
                }
                this.Arguments = arguments.ToArray();

                csharpTypeString = $"delegate* unmanaged[Cdecl]<{string.Join(", ", unmanagedArguments)}, {this.ReturnType.ToUnmanagedString()}>";
            }

            mCSharpTypeString = mCSharpUnmanagedTypeString = csharpTypeString;
            mType = new DeclarationType(mCXType, new CppType(this));
        }

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith($"::{name}") || this.FunctionTypeString == name;
        }

        public string ToCppExportArgumentTypeString()
        {
            return this.FullName;
        }

        public bool CheckCppShouldCastExportArgumentTypeToInvocationType()
        {
            return false; // Assuming no cast for function pointer
        }

        public string? ToCppExportArgumentCastString(string argumentName, string targetName)
        {
            return null; // Assuming no cast for function pointer
        }

        public string? ToCppExportInvocationCastString(string content)
        {
            return null; // Assuming no cast for function pointer
        }

        public string ToCppExportReturnTypeString()
        {
            Debugger.Break();
            throw new InvalidOperationException("Do not support export function pointer as return type");
        }

        public string ToCppExportReturnValueString(string content)
        {
            return content;
        }
    }

    internal class Constructor : Function
    {
        static public readonly Regex ExportConstructorExpression = new Regex($@"{CppAnalyzer.ExportConstructorMacro}\((.*)\)\s*;");

        public override string? ExportMacro => CppAnalyzer.ExportConstructorMacro;

        public Constructor(CppContext context, CXCursor cursor) : base(context, cursor) { }

        protected override Type checkReturnType(CppContext context) => this.FindType(context, this.Cursor.SemanticParent.GetFullTypeName()) ?? throw new InvalidOperationException($"There is no type {this.Cursor.SemanticParent.GetFullTypeName()}");

        protected override Type checkCppExportReturnType() => this.ReturnType.MakePointerType();

        protected override string checkCppExportFunctionName() => this.BindingName ?? throw new InvalidOperationException("This function might not be marked as an exported function");

        protected override List<string> checkCppExportArguments() => this.Arguments.Select(a => a.ToCppCode()).ToList();

        protected override string makeCppExportDeclaration()
        {
            Type returnType = this.checkCppExportReturnType();
            string exportName = this.checkCppExportFunctionName();
            List<string> arguments = this.checkCppExportArguments();
            string returnTypeString = this.Analyzer.MakeConstructorCppExportReturnTypeString(returnType) ?? returnType.MakeCppExportReturnTypeString();
            return $"__declspec (dllexport) {returnTypeString} {exportName}({string.Join(", ", arguments)})";
        }

        protected override void makeCppExportDefinition(CppExportContext context)
        {
            List<string> arguments = new List<string>();
            foreach (Argument argument in this.Arguments)
            {
                string? castCode = argument.MakeDynamicCastCode();
                if (!string.IsNullOrWhiteSpace(castCode))
                {
                    context.WriteLine(castCode);
                    //contents.Add($"if (!{argument.DerivedName}) return nullptr;");
                }

                arguments.Add(argument.ToCppInvocationCode());
            }

            string content = this.Analyzer.MakeConstructorCppExportReturnValueString(this.ReturnType, arguments.ToArray()) ?? $"new {this.ReturnType.FullName}({string.Join(", ", arguments)})";
            context.WriteLine($"return {content};");
        }

        public override string ToString()
        {
            return $"{this.GetType().Name} {this.Name}";
        }
    }

    public class Class : DeclarationCollection, ITypeDeclaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportClassMacro;
        private const int INDEX_EXPORT_AS = 0;
        private const int INDEX_EXPORT_PREFIX = 1;

        private DeclarationType? mType = null;
        public DeclarationType Type => mType ?? throw new InvalidOperationException($"Make sure there is a valid {nameof(DeclarationType)}, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpTypeString = null;
        public string CSharpTypeString => mCSharpTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# type string, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpUnmanagedTypeString = null;
        public string CSharpUnmanagedTypeString => mCSharpUnmanagedTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# unmanaged type string, and {nameof(Declaration.Analyze)} has been invoked");

        private Type? mExportAsType = null;
        public Type ExportAsType => mExportAsType ?? throw new InvalidOperationException($"Make sure this type has been marked as export, and {nameof(Declaration.Analyze)} has been invoked");

        private Type mCSharpType;
        private CXType mCXType;

        public string? BindingPrefix => this.ExportContents.Length > INDEX_EXPORT_PREFIX ? this.ExportContents[INDEX_EXPORT_PREFIX] : "";

        internal Class(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            mCSharpType = typeof(IntPtr);
            mCXType = cursor.Type;
        }

        protected Class(CppAnalyzer analyzer, string name, Type csharpType, Type exportAsType) : base(analyzer, name)
        {
            mExportAsType = exportAsType;
            mCSharpType = csharpType;
            mCXType = new CXType();
        }

        protected virtual Type checkDeclaredType() => new CppType(this);

        internal override void internalAnalyze(CppContext context)
        {
            base.internalAnalyze(context);

            mCSharpTypeString = mCSharpType.ToCSharpTypeString();
            mCSharpUnmanagedTypeString = mCSharpType.ToCSharpUnmanagedTypeString();
            mType = new DeclarationType(mCXType, this.checkDeclaredType());

            if (mExportAsType is null && this.ExportContents.Length > INDEX_EXPORT_AS)
            {
                string exportString = this.ExportContents[INDEX_EXPORT_AS];
                mExportAsType = this.Name == exportString ? this.Type.DeclaredType : this.FindType(context, exportString) ?? throw new InvalidOperationException($"There is no type {exportString}, mark it with {CppAnalyzer.ExportClassMacro}|{CppAnalyzer.ExportStructMacro}|{CppAnalyzer.ExportEnumMacro} and make sure it is processed in front of this type");
            }
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
            return this.Name == name || this.FullName.EndsWith($"::{name}");
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
            element.SetAttribute(nameof(BindingPrefix), this.BindingPrefix);
        }

        public string ToCppExportArgumentTypeString()
        {
            string? content = this.Analyzer.MakeCppExportArgumentTypeString(this.Type.DeclaredType);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = this.Analyzer.MakeCppExportArgumentTypeString(this.ExportAsType);
                if (string.IsNullOrWhiteSpace(content))
                {
                    content = this.ExportAsType.ToCppTypeString() + "*";
                }
            }
            return content;
        }

        public bool CheckCppShouldCastExportArgumentTypeToInvocationType()
        {
            bool? result = this.Analyzer.CheckCppShouldCastExportArgumentTypeToInvocationType(this.Type.DeclaredType);
            if (!result.HasValue)
            {
                result = this.Analyzer.CheckCppShouldCastExportArgumentTypeToInvocationType(this.ExportAsType);
            }
            return result.HasValue ? result.Value : this.Type.DeclaredType != this.ExportAsType;
        }

        public string? ToCppExportArgumentCastString(string argumentName, string targetName)
        {
            string? content = this.Analyzer.MakeCppExportArgumentCastString(this.Type.DeclaredType, argumentName, targetName);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = this.Analyzer.MakeCppExportArgumentCastString(this.ExportAsType, argumentName, targetName);
                if (string.IsNullOrWhiteSpace(content))
                {
                    string declaredType = this.Type.DeclaredType.ToCppTypeString();
                    content = $"{declaredType}* {targetName} = dynamic_cast<{declaredType}*>({argumentName})";
                }
            }
            return content;
        }

        public string? ToCppExportInvocationCastString(string inputContent)
        {
            string? content = this.Analyzer.MakeCppExportInvocationCastString(this.Type.DeclaredType, inputContent);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = this.Analyzer.MakeCppExportInvocationCastString(this.ExportAsType, inputContent);
            }
            return content;
        }

        public string ToCppExportReturnTypeString()
        {
            string? content = this.Analyzer.MakeCppExportReturnTypeString(this.Type.DeclaredType);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = this.Analyzer.MakeCppExportReturnTypeString(this.ExportAsType);
                if (string.IsNullOrWhiteSpace(content))
                {
                    content = this.ExportAsType.ToCppString();
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        content = this.FullName + "*"; // Assuming it must be a pointer when return a class type
                    }
                }
            }
            return content;
        }

        public string ToCppExportReturnValueString(string inputContent)
        {
            string? content = this.Analyzer.MakeCppExportReturnValueString(this.Type.DeclaredType, inputContent);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = this.Analyzer.MakeCppExportReturnValueString(this.ExportAsType, inputContent);
                if (string.IsNullOrWhiteSpace(content))
                {
                    content = inputContent;
                }
            }
            return content;
        }
    }

    public class ClassTemplate : Class, ITemplateTypeDeclaration
    {
        internal ClassTemplate(CppContext context, CXCursor cursor) : base(context, cursor) { }

        protected ClassTemplate(CppAnalyzer analyzer, string name, Type csharpType, Type exportAsType) : base(analyzer, name, csharpType, exportAsType) { }

        internal Type MakeGenericType(CppContext context, CXType type)
        {
            List<Type> arguments = new List<Type>();
            int templateArgumentCount = type.NumTemplateArguments;
            for (uint i = 0; i < templateArgumentCount; ++i)
            {
                CXType argumentType;
                CX_TemplateArgument templateArgument = type.GetTemplateArgument(i);
                switch (templateArgument.kind)
                {
                    case CXTemplateArgumentKind.CXTemplateArgumentKind_Type:
                        argumentType = templateArgument.AsType;
                        arguments.Add(this.FindType(context, argumentType) ?? throw new InvalidOperationException($"There is no type {argumentType.GetOriginalTypeName()}"));
                        break;
                    default: throw new NotImplementedException();
                }
            }
            return new CppTemplateType(this, type, arguments);
        }

        public string ToCppExportArgumentTypeString(Type[] arguments)
        {
            string? content = this.Analyzer.MakeCppExportArgumentTypeString(this, arguments);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = $"{this.FullName}<{string.Join(", ", arguments.Select(a => a.MakeCppExportReturnTypeString()))}>";
            }
            return content;
        }

        public bool CheckCppShouldCastExportArgumentTypeToInvocationType(Type[] arguments)
        {
            bool? result = this.Analyzer.CheckCppShouldCastExportArgumentTypeToInvocationType(this, arguments);
            return result.HasValue ? result.Value : false;
        }

        public string? ToCppExportArgumentCastString(Type[] arguments, string argumentName, string targetName)
        {
            return this.Analyzer.MakeCppExportArgumentCastString(this, arguments, argumentName, targetName);
        }

        public string? ToCppExportInvocationCastString(Type[] arguments, string content)
        {
            return this.Analyzer.MakeCppExportInvocationCastString(this, arguments, content);
        }

        public string ToCppExportReturnTypeString(Type[] arguments)
        {
            string? content = this.Analyzer.MakeCppExportReturnTypeString(this, arguments);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = $"{this.FullName}<{string.Join(", ", arguments.Select(a => a.MakeCppExportReturnTypeString()))}>";
            }
            return content;
        }

        public string ToCppExportReturnValueString(Type[] arguments, string content)
        {
            return this.Analyzer.MakeCppExportReturnValueString(this, arguments, content) ?? content;
        }
    }

    internal class Struct : DeclarationCollection, ITypeDeclaration
    {
        private const int INDEX_NAME = 0;

        public override string? ExportMacro => CppAnalyzer.ExportStructMacro;

        private CXType mCXType;

        private DeclarationType? mType = null;
        public DeclarationType Type => mType ?? throw new InvalidOperationException($"Make sure there is a valid {nameof(DeclarationType)}, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpTypeString = null;
        public string CSharpTypeString => mCSharpTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# type string, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpUnmanagedTypeString = null;
        public string CSharpUnmanagedTypeString => mCSharpUnmanagedTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# unmanaged type string, and {nameof(Declaration.Analyze)} has been invoked");

        public string BindingName { get; init; }
        public override bool ShouldExport => !this.BindingName.Contains(".");

        public int AlignOf => (int)mCXType.AlignOf;
        public int SizeOf => (int)mCXType.SizeOf;

        public Struct(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            mCXType = cursor.Type;
            this.BindingName = this.ExportContents.Length > INDEX_NAME ? this.ExportContents[INDEX_NAME] : "";
        }

        internal override void internalAnalyze(CppContext context)
        {
            base.internalAnalyze(context);

            mCSharpTypeString = mCSharpUnmanagedTypeString = this.BindingName.Contains(".") ? this.BindingName : this.FullName;
            mType = new DeclarationType(mCXType, new CppType(this));
        }

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith($"::{name}");
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(FullName), this.FullName);
        }

        public string ToCppExportArgumentTypeString()
        {
            return this.Analyzer.MakeCppExportArgumentTypeString(this.Type.DeclaredType) ?? this.FullName;
        }

        public bool CheckCppShouldCastExportArgumentTypeToInvocationType()
        {
            return false; // Assuming no cast for struct
        }

        public string? ToCppExportArgumentCastString(string argumentName, string targetName)
        {
            return null; // Assuming no cast for struct
        }

        public string? ToCppExportInvocationCastString(string content)
        {
            return null; // Assuming no cast for struct
        }

        public string ToCppExportReturnTypeString()
        {
            return this.Analyzer.MakeCppExportReturnTypeString(this.Type.DeclaredType) ?? this.FullName;
        }

        public string ToCppExportReturnValueString(string content)
        {
            return this.Analyzer.MakeCppExportReturnValueString(this.Type.DeclaredType, content) ?? content;
        }

        /*private bool checkSequential(List<Field> fields)
        {
            if (fields.Count <= 1)
            {
                return true;
            }

            Field previousField = fields[0];
            for (int i = 1; i < fields.Count; ++i)
            {
                Field field = fields[i];
                if (previousField.OffsetInBits / 8 + previousField.SizeOf <= field.OffsetInBits / 8)
                {
                    return false;
                }
                previousField = field;
            }
            return previousField.OffsetInBits / 8 + previousField.SizeOf == this.SizeOf;
        }*/

        internal override void internalMakeCSharpDefinition(CSharpBindingContext context)
        {
            base.internalMakeCSharpDefinition(context);

            List<Field> fields = this.Declarations.Select(d => d as Field).Where(d => d is not null).Cast<Field>().ToList();
            //if (this.checkSequential(fields))
            //{
            //    this.makeCSharpSequentialDefinition(context, fields);
            //}
            //else 
            {
                this.makeCSharpExplicitlDefinition(context, fields);
            }
        }

        private void makeCSharpSequentialDefinition(CSharpBindingContext context, List<Field> fields)
        {
            context.WriteLine($"[StructLayout(LayoutKind.Sequential, Pack = {this.AlignOf}, Size = {this.SizeOf})]");
            context.WriteLine($"public struct {this.BindingName}");
            context.WriteLine("{");
            ++context.TabCount;
            foreach (Field field in fields)
            {
                field.MakeCSharpDefinition(context, false);
            }
            --context.TabCount;
            context.WriteLine("}");
        }

        private void makeCSharpExplicitlDefinition(CSharpBindingContext context, List<Field> fields)
        {
            context.WriteLine($"[StructLayout(LayoutKind.Explicit, Pack = {this.AlignOf}, Size = {this.SizeOf})]");
            context.WriteLine($"public struct {this.BindingName}");
            context.WriteLine("{");
            ++context.TabCount;
            foreach (Field field in fields)
            {
                field.MakeCSharpDefinition(context, true);
            }
            --context.TabCount;
            context.WriteLine("}");
        }
    }

    internal class Field : Declaration
    {
        public override string? ExportMacro => CppAnalyzer.ExportFieldMacro;


        private Type? mType = null;
        public Type Type => mType ?? throw new InvalidOperationException($"Make sure there is a valid {nameof(Type)}, and {nameof(Declaration.Analyze)} has been invoked");

        public int OffsetInBits => (int)this.Cursor.OffsetOfField;
        public int SizeOf => (int)this.Cursor.Type.SizeOf;

        public bool IsArray => this.Cursor.Type.IsArray();
        public int ArraySize => (int)this.Cursor.Type.ArraySize;

        public Field(CppContext context, CXCursor cursor) : base(context, cursor) { }

        internal override void internalAnalyze(CppContext context)
        {
            base.internalAnalyze(context);

            CXType type = this.Cursor.Type;
            if (type.IsArray())
            {
                type = type.ElementType;
            }
            mType = this.FindType(context, type) ?? throw new InvalidOperationException($"There is no type {type.GetFullTypeName()}");
        }

        protected override void makeXml(XmlElement element)
        {
            element.SetAttribute(nameof(Type), this.Type.FullName);
        }

        internal void MakeCSharpDefinition(CSharpBindingContext context, bool setFieldOffset)
        {
            if (!string.IsNullOrWhiteSpace(this.CommentText))
            {
                foreach (string line in this.CommentText.Split('\n'))
                {
                    context.WriteLine(line.Trim());
                }
            }

            context.WriteLine($"[FieldOffset({this.OffsetInBits / 8})]");
            this.internalMakeCSharpDefinition(context);
        }

        internal override void internalMakeCSharpDefinition(CSharpBindingContext context)
        {
            base.internalMakeCSharpDefinition(context);
            context.WriteLine(this.ToCSharpCode());
        }

        public override string ToCSharpCode()
        {
            return $"public {this.Type.ToCSharpTypeString()} {this.Name};";
        }

        public override string ToString()
        {
            return $"{this.GetType().Name} {this.Content}";
        }
    }

    internal class Enum : DeclarationCollection, ITypeDeclaration
    {
        private const int INDEX_NAME = 0;

        public override string? ExportMacro => CppAnalyzer.ExportEnumMacro;

        public string BindingName { get; init; }

        public string[] Values { get; private set; } = new string[0];


        private DeclarationType? mType = null;
        public DeclarationType Type => mType ?? throw new InvalidOperationException($"Make sure there is a valid {nameof(DeclarationType)}, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpTypeString = null;
        public string CSharpTypeString => mCSharpTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# type string, and {nameof(Declaration.Analyze)} has been invoked");

        private string? mCSharpUnmanagedTypeString = null;
        public string CSharpUnmanagedTypeString => mCSharpUnmanagedTypeString ?? throw new InvalidOperationException($"Make sure there is a valid C# unmanaged type string, and {nameof(Declaration.Analyze)} has been invoked");

        public Enum(CppContext context, CXCursor cursor) : base(context, cursor)
        {
            mCSharpTypeString = mCSharpUnmanagedTypeString = this.BindingName = this.ExportContents.Length > INDEX_NAME ? this.ExportContents[INDEX_NAME] : "";
            mType = new DeclarationType(cursor.Type, new CppType(this));
        }

        bool ITypeDeclaration.MatchTypeName(string name)
        {
            return this.Name == name || this.FullName.EndsWith($"::{name}");
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

        public string ToCppExportArgumentTypeString()
        {
            return this.Analyzer.MakeCppExportArgumentTypeString(this.Type.DeclaredType) ?? this.FullName;
        }

        public bool CheckCppShouldCastExportArgumentTypeToInvocationType()
        {
            return false; // Assuming no cast for enum
        }

        public string? ToCppExportArgumentCastString(string argumentName, string targetName)
        {
            return null; // Assuming no cast for enum
        }

        public string? ToCppExportInvocationCastString(string content)
        {
            return null; // Assuming no cast for enum
        }

        public string ToCppExportReturnTypeString()
        {
            return this.Analyzer.MakeCppExportReturnTypeString(this.Type.DeclaredType) ?? this.FullName;
        }

        public string ToCppExportReturnValueString(string content)
        {
            return this.Analyzer.MakeCppExportReturnValueString(this.Type.DeclaredType, content) ?? content;
        }

        internal override void internalMakeCSharpDefinition(CSharpBindingContext context)
        {
            base.internalMakeCSharpDefinition(context);

            context.WriteLine($"public enum {this.BindingName}");
            context.WriteLine("{");

            ++context.TabCount;
            foreach (Declaration child in this.Declarations)
            {
                EnumConstant? constant = child as EnumConstant;
                if (constant is not null)
                {
                    constant.MakeCSharpDefinition(context);
                }
            }
            --context.TabCount;

            context.WriteLine("}");
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

        internal override void internalMakeCSharpDefinition(CSharpBindingContext context)
        {
            base.internalMakeCSharpDefinition(context);
            context.WriteLine(this.ToCSharpCode());
        }

        public override string ToCSharpCode()
        {
            return $"{this.Name} = {this.Value},";
        }
    }
}
