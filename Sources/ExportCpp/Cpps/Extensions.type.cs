using ExportCpp.Cpps;

namespace ExportCpp
{
    static internal partial class Extensions
    {
        static public Type ToCSharpType(this Type type)
        {
            //CppType? cppType = type as CppType;
            //return cppType is null ? type : typeof(IntPtr);
            return type is CppType ? typeof(IntPtr) : type;
        }

        static public string ToCSharpTypeString(this Type type)
        {
            Type realType = type.ToCSharpType();
            if (typeof(void) == realType)
            {
                return "void";
            }

            return realType.FullName;
        }

        static public string ToCppTypeString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.IsPointer ? (cppType.FullName + "*") : (cppType.FullName ?? throw new InvalidOperationException());
            }

            if (typeof(void) == type)
            {
                return "void";
            }
            if (type.IsPrimitive)
            {
                if (typeof(bool) == type)
                {
                    return "bool";
                }
                if (typeof(sbyte) == type)
                {
                    return "char";
                }
                else if (typeof(byte) == type)
                {
                    return "unsigned char";
                }
                else if (typeof(short) == type)
                {
                    return "short int";
                }
                else if (typeof(ushort) == type)
                {
                    return "unsigned short int";
                }
                else if (typeof(int) == type)
                {
                    return "int";
                }
                else if (typeof(uint) == type)
                {
                    return "unsigned int";
                }
                else if (typeof(long) == type)
                {
                    return "long long";
                }
                else if (typeof(ulong) == type)
                {
                    return "unsigned long long";
                }
                else if (typeof(float) == type)
                {
                    return "float";
                }
                else if (typeof(double) == type)
                {
                    return "double";
                }

                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        static public string ToCppDefaultValueString(this Type type)
        {
            CppType? cppType = type as CppType;
            if (cppType is not null)
            {
                return cppType.IsPointer ? "nullptr" : throw new InvalidOperationException("Never create instance without new");
            }

            if (typeof(void) == type)
            {
                return "";
            }
            if (type.IsPrimitive)
            {
                if (typeof(bool) == type)
                {
                    return "false";
                }
                if (typeof(sbyte) == type || typeof(byte) == type || typeof(short) == type || typeof(ushort) == type || typeof(int) == type)
                {
                    return "0";
                }
                else if (typeof(uint) == type)
                {
                    return "0u";
                }
                else if (typeof(long) == type)
                {
                    return "0ll";
                }
                else if (typeof(ulong) == type)
                {
                    return "0llu";
                }
                else if (typeof(float) == type)
                {
                    return ".0f";
                }
                else if (typeof(double) == type)
                {
                    return ".0";
                }

                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }
    }
}
