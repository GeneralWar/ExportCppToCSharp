using ClangSharp;
using Type = System.Type;

namespace ExportCpp
{
    static public partial class Extensions
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
                return cppType.Declaration.CSharpUnmanagedTypeString;
            }

            return type.ToUnmanagedString();
        }

        static public string ToCppTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.ToCppTypeString();
            }

            return type.ToCppString() ?? throw new InvalidOperationException();
        }

        static public string MakeCppExportArgumentTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.MakeCppExportArgumentTypeString();
            }

            return type.ToCppString() ?? throw new InvalidOperationException();
        }

        static public bool CheckCppShouldCastExportArgumentTypeToInvocationType(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.CheckCppShouldCastExportArgumentTypeToInvocationType();
            }

            return false;
        }

        static public string? MakeCppExportArgumentCastString(this Type type, string argumentName, string targetName)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.MakeCppExportArgumentCastString(argumentName, targetName);
            }

            return null;
        }

        static public string? MakeCppExportInvocationCastString(this Type type, string content)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.MakeCppExportInvocationCastString(content);
            }

            return null;
        }

        static public string MakeCppExportReturnTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.MakeCppExportReturnTypeString();
            }

            return type.ToCppString() ?? throw new InvalidOperationException();
        }

        static public string MakeCppExportReturnValueString(this Type type, string content)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.MakeCppExportReturnValueString(content);
            }

            return content;
        }

        static public string MakeCppExportTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.MakeCppExportTypeString();
            }

            return type.ToCppString() ?? throw new InvalidOperationException();
        }
    }
}
