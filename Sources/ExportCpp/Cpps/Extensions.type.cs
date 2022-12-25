using ExportCpp.Cpps;

namespace ExportCpp
{
    static internal partial class Extensions
    {
        static public Type ToCSharpType(this Type type)
        {
            //CppType? cppType = type as CppType;
            //return cppType is null ? type : typeof(IntPtr);
            return (type as CppType)?.ToCSharpType() ?? type;
        }

        static public string ToCSharpTypeString(this Type type)
        {
            Type realType = type.ToCSharpType();
            if (typeof(void) == realType)
            {
                return "void";
            }

            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.ToCSharpTypeString();
            }

            return realType.FullName ?? throw new InvalidOperationException();
        }

        static public string ToCSharpUnmanagedTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.IsPointer ? (cppType.FullName + "*") : (cppType.FullName ?? throw new InvalidOperationException());
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

            return type.ToCppString();
        }
    }
}
