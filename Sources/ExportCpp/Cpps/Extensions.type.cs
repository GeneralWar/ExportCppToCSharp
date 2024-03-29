﻿using ClangSharp;
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

            string content;
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                content = cppType.ToCSharpTypeString().Trim();
            }
            else
            {
                content = type.ToShortString()?.Trim() ?? type.FullName ?? throw new InvalidOperationException();
            }
            if (type.IsPointer && (type.IsValueType/* || type.IsEnum*/))
            {
                if (!content.EndsWith("*"))
                {
                    content += "*";
                }
            }
            return content.Replace("::", ".");
        }

        static public string ToCSharpBindingArgumentTypeString(this Type type)
        {
            if (typeof(void) == type)
            {
                return "void";
            }

            string content;
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                content = cppType.MakeCSharpBindingArgumentTypeString().Trim();
            }
            else
            {
                content = type.ToShortString()?.Trim() ?? type.FullName ?? throw new InvalidOperationException();
            }
            if (type.IsPointer && (type.IsValueType/* || type.IsEnum*/))
            {
                if (!content.EndsWith("*"))
                {
                    content += "*";
                }
            }
            return content.Replace("::", ".");
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
    }
}
