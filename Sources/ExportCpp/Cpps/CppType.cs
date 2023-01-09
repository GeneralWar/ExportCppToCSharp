using ClangSharp.Interop;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace ExportCpp
{
    public class CppType : Type
    {
        public ITypeDeclaration Declaration { get; init; }

        public override Assembly Assembly => throw new NotImplementedException();

        public override string? AssemblyQualifiedName => throw new NotImplementedException();

        public override Type? BaseType => this.checkBaseType();

        public override string? FullName => this.IsPointer ? $"{this.Declaration.FullName}*" : this.Declaration.FullName;

        public override Guid GUID => throw new NotImplementedException();

        public override Module Module => throw new NotImplementedException();

        public override string? Namespace => throw new NotImplementedException();

        public override Type UnderlyingSystemType => this;

        public override string Name => this.IsPointer ? $"{this.Declaration.Name}*" : this.Declaration.Name;

        private bool mIsPointer = false;
        protected override bool IsPointerImpl() => mIsPointer;
        protected override bool IsValueTypeImpl() => this.Declaration is Struct || this.Declaration is Enum;
        public override bool IsEnum => this.Declaration is Enum;

        public CppType(ITypeDeclaration declaration) : this(declaration, false) { }

        protected CppType(ITypeDeclaration declaration, bool isPointer)
        {
            mIsPointer = isPointer;
            this.Declaration = declaration;
        }

        private Type? checkBaseType()
        {
            if (this.IsPointer)
            {
                return null;
            }

            Class? @class = this.Declaration as Class;
            if (@class is not null)
            {
                return @class.ExportAsType == this ? typeof(object) : @class.ExportAsType;
            }

            Struct? @struct = this.Declaration as Struct;
            if (@struct is not null)
            {
                return typeof(ValueType);
            }

            Enum? @enum = this.Declaration as Enum;
            if (@enum is not null)
            {
                return typeof(System.Enum);
            }

            return null;
        }

        public override Type MakePointerType()
        {
            return this.IsPointer ? this : new CppType(this.Declaration, true);
        }

        public string ToCSharpTypeString()
        {
            return this.Declaration.CSharpTypeString;
        }

        public virtual string ToCppTypeString()
        {
            return this.FullName ?? throw new InvalidOperationException();
        }

        public virtual string MakeCppExportArgumentTypeString()
        {
            string value = this.Declaration.ToCppExportArgumentTypeString();
            if (this.Declaration is Class)
            {
                return value;
            }
            return this.IsPointer ? (value + "*") : value;
        }

        public virtual bool CheckCppShouldCastExportArgumentTypeToInvocationType()
        {
            return this.Declaration.CheckCppShouldCastExportArgumentTypeToInvocationType();
        }

        public virtual string? MakeCppExportArgumentCastString(string argumentName, string targetName)
        {
            return this.Declaration.ToCppExportArgumentCastString(argumentName, targetName);
        }

        public virtual string? MakeCppExportInvocationCastString(string content)
        {
            return this.Declaration.ToCppExportInvocationCastString(content);
        }

        public virtual string MakeCppExportReturnTypeString()
        {
            string value = this.Declaration.ToCppExportReturnTypeString();
            if (this.Declaration is Class)
            {
                return value;
            }
            return this.IsPointer ? (value + "*") : value;
        }

        public virtual string MakeCppExportReturnValueString(string content)
        {
            return this.Declaration.ToCppExportReturnValueString(content);
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override Type? GetElementType()
        {
            if (!this.IsPointer)
            {
                throw new InvalidOperationException();
            }
            return new CppType(this.Declaration);
        }

        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        public override Type? GetInterface(string name, bool ignoreCase)
        {
            throw new NotImplementedException();
        }

        public override Type[] GetInterfaces()
        {
            throw new NotImplementedException();
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override Type? GetNestedType(string name, BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters)
        {
            throw new NotImplementedException();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            throw new NotImplementedException();
        }

        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers)
        {
            throw new NotImplementedException();
        }

        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            throw new NotImplementedException();
        }

        protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers)
        {
            throw new NotImplementedException();
        }

        protected override bool HasElementTypeImpl()
        {
            throw new NotImplementedException();
        }

        protected override bool IsArrayImpl()
        {
            throw new NotImplementedException();
        }

        protected override bool IsByRefImpl()
        {
            throw new NotImplementedException();
        }

        protected override bool IsCOMObjectImpl()
        {
            throw new NotImplementedException();
        }

        protected override bool IsPrimitiveImpl()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"{this.ToCppTypeString()} -> {this.ToCSharpTypeString()}";
        }
    }

    public class CppTemplateType : CppType
    {
        private ITemplateTypeDeclaration mTemplateDeclaration;

        private CXType mTemplateType;

        private List<Type> mTemplateArguments;
        public override Type[] GenericTypeArguments => mTemplateArguments.ToArray();

        public override bool IsGenericType => true;

        public CppTemplateType(ITemplateTypeDeclaration declaration, CXType templateType, IEnumerable<Type> templateArguments) : this(declaration, templateType, templateArguments, false) { }

        private CppTemplateType(ITemplateTypeDeclaration declaration, CXType templateType, IEnumerable<Type> templateArguments, bool isPointer) : base(declaration, isPointer)
        {
            mTemplateDeclaration = declaration;

            mTemplateType = templateType;
            mTemplateArguments = new List<Type>(templateArguments);
        }

        public override Type MakePointerType()
        {
            return this.IsPointer ? this : new CppTemplateType(mTemplateDeclaration, mTemplateType, mTemplateArguments, true);
        }

        public string[] CheckTemplateInvocationArgumentTypes()
        {
            return mTemplateArguments.Select(t => t.ToCppTypeString()).ToArray();
        }

        public override string MakeCppExportArgumentTypeString()
        {
            return mTemplateDeclaration.ToCppExportArgumentTypeString(mTemplateArguments.ToArray());
        }

        public override bool CheckCppShouldCastExportArgumentTypeToInvocationType()
        {
            return mTemplateDeclaration.CheckCppShouldCastExportArgumentTypeToInvocationType(mTemplateArguments.ToArray());
        }

        public override string? MakeCppExportArgumentCastString(string argumentName, string targetName)
        {
            return mTemplateDeclaration.ToCppExportArgumentCastString(mTemplateArguments.ToArray(), argumentName, targetName);
        }

        public override string? MakeCppExportInvocationCastString(string content)
        {
            return mTemplateDeclaration.ToCppExportInvocationCastString(mTemplateArguments.ToArray(), content);
        }

        public override string MakeCppExportReturnTypeString()
        {
            return mTemplateDeclaration.ToCppExportReturnTypeString(mTemplateArguments.ToArray());
        }

        public override string MakeCppExportReturnValueString(string content)
        {
            return mTemplateDeclaration.ToCppExportReturnValueString(mTemplateArguments.ToArray(), content);
        }

        public override string ToCppTypeString()
        {
            //string template = mTemplateType.GetFullTypeName();            
            //return $"{template}<{string.Join(", ", mTemplateArguments.Select(a => a.Type.DeclaredType.ToCppTypeString()))}>";
            return mTemplateType.GetFullTypeName();
        }

        public override Type? GetElementType()
        {
            if (!this.IsPointer)
            {
                throw new InvalidOperationException();
            }
            return new CppTemplateType(mTemplateDeclaration, mTemplateType, mTemplateArguments);
        }
    }
}
