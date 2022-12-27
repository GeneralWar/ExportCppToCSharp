﻿using ClangSharp.Interop;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace ExportCpp
{
    public class CppType : Type
    {
        public ITypeDeclaration Declaration { get; init; }

        public override Assembly Assembly => throw new NotImplementedException();

        public override string? AssemblyQualifiedName => throw new NotImplementedException();

        public override Type? BaseType => throw new NotImplementedException();

        public override string? FullName => this.Declaration.FullName;

        public override Guid GUID => throw new NotImplementedException();

        public override Module Module => throw new NotImplementedException();

        public override string? Namespace => throw new NotImplementedException();

        public override Type UnderlyingSystemType => this;

        public override string Name => this.Declaration.Name;

        private bool mIsPointer = false;
        protected override bool IsPointerImpl() => mIsPointer;

        public CppType(ITypeDeclaration declaration) : this(declaration, false) { }

        private CppType(ITypeDeclaration declaration, bool isPointer)
        {
            this.Declaration = declaration;
            mIsPointer = isPointer;
        }

        public override Type MakePointerType()
        {
            return this.IsPointer ? this : new CppType(this.Declaration, true);
        }

        public string ToCSharpTypeString()
        {
            return this.Declaration.Type.CSharpTypeString;
        }

        public virtual string ToCppTypeString()
        {
            return this.IsPointer ? (this.FullName + "*") : (this.FullName ?? throw new InvalidOperationException());
        }

        /// <summary>
        /// convert to cpp function result after 'return'
        /// </summary>
        /// <param name="content">original content of execution, such as 'instance->function()' after 'return'</param>
        /// <returns></returns>
        public virtual string ConvertToCppResult(string content) => content;

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
            throw new NotImplementedException();
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
    }
}
