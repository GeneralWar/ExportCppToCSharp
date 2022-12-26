using ExportCpp.Cpps;

namespace ExportCpp
{
    static internal partial class Extensions
    {
        static public string ToCSharpTypeString(this Type type)
        {
            if (typeof(void) == type)
            {
                return "void";
            }

            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.ToCSharpTypeString();
            }

            return type.ToShortString() ?? type.FullName ?? throw new InvalidOperationException();
        }

        static public string ToCSharpUnmanagedTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.Declaration.Type.CSharpUnmanagedTypeString;
            }

            return type.ToUnmanagedString();
        }

        static public string ToCppTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.IsPointer ? (cppType.FullName + "*") : (cppType.FullName ?? throw new InvalidOperationException());
            }

            return type.ToCppString() ?? throw new InvalidOperationException();
        }
    }
}
