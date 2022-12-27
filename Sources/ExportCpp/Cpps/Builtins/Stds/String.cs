namespace ExportCpp.Cpps.Builtins.Stds
{
    internal class String : Class
    {
        internal String() : base("string", typeof(string), typeof(string)) { }

        protected override Type checkDeclaredType() => new CppStringType(this);

        public override string ToCppCode()
        {
            return this.Type.DeclaredType.ToCppTypeString();
        }
    }

    internal class CppStringType : CppType
    {
        public CppStringType(ITypeDeclaration declaration) : base(declaration) { }

        public override string ToCppTypeString()
        {
            return "char*";
        }

        public override string ConvertToCppResult(string content) => $"{content}.c_str()";
    }
}
