using ClangSharp.Interop;

namespace ExportCpp
{
    static internal partial class Extensions
    {
        static public string GetName(this CXCursor instance)
        {
            return instance.Spelling.CString;
        }

        static public string GetFullTypeName(this CXCursor instance)
        {
            return instance.Type.GetFullTypeName();
        }

        static public string GetFullNamespace(this CXCursor instance)
        {
            Stack<string> names = new Stack<string>();
            CXCursor cursor = instance;
            while (!cursor.IsInvalid)
            {
                if (CXCursorKind.CXCursor_Namespace == cursor.kind)
                {
                    names.Push(cursor.GetName());
                }
                cursor = cursor.SemanticParent;
            }
            return string.Join("::", names);
        }

        static public bool IsTypeDeclaration(this CXCursor instance)
        {
            return CXCursorKind.CXCursor_ClassDecl == instance.kind || CXCursorKind.CXCursor_StructDecl == instance.kind || CXCursorKind.CXCursor_EnumDecl == instance.kind;
        }

        static public bool MatchTypeName(this CXCursor instance, string name)
        {
            if (instance.DisplayName.CString == name)
            {
                return true;
            }

            string fullname = instance.GetFullTypeName();
            if (fullname.EndsWith(name))
            {
                return true;
            }

            return false;
        }

        static public int GetSiblingIndex(this CXCursor instance)
        {
            CXCursor parent = instance.SemanticParent;
            if (parent.IsInvalid)
            {
                return -1;
            }

            for (uint i = 0; i < parent.NumDecls; ++i)
            {
                if (parent.GetDecl(i) == instance)
                {
                    return (int)i;
                }
            }
            return -1;
        }

        static public bool IsExposedDeclaration(this CXCursor instance)
        {
            return CXCursorKind.CXCursor_FirstDecl <= instance.kind && instance.kind <= CXCursorKind.CXCursor_LastDecl && CXCursorKind.CXCursor_UnexposedDecl != instance.kind;
        }

        static public bool HasSign(this CXType instance)
        {
            return CXTypeKind.CXType_SChar == instance.kind || CXTypeKind.CXType_Short == instance.kind || CXTypeKind.CXType_Int == instance.kind || CXTypeKind.CXType_Long == instance.kind || CXTypeKind.CXType_Float == instance.kind || CXTypeKind.CXType_Double == instance.kind;
        }

        static public string GetFullTypeName(this CXType instance)
        {
            return instance.Desugar.Spelling.CString;
        }

        static public string GetOriginalTypeName(this CXType instance)
        {
            if (CXTypeKind.CXType_Invalid != instance.PointeeType.kind)
            {
                string type = instance.PointeeType.GetOriginalTypeName();
                if ("void" == type)
                {
                    return "void*";
                }
                return type.Trim();
            }

            string name = instance.Desugar.Spelling.CString.Trim();
            if (CXTypeKind.CXType_LValueReference == instance.kind || CXTypeKind.CXType_RValueReference == instance.kind)
            {
                name = name.TrimEnd('&').TrimEnd();
            }
            if (name.StartsWith("const"))
            {
                name = name.Substring("const".Length + 1).TrimStart();
            }
            if (name.EndsWith("const"))
            {
                name = name.Substring(0, name.Length - "const".Length).TrimEnd();
            }
            return name;
        }

        static public Type ToBuiltinType(this CXType instance)
        {
            switch (instance.kind)
            {
                case CXTypeKind.CXType_Void: return typeof(void);
                case CXTypeKind.CXType_Bool: return typeof(bool);
                //case CXTypeKind.CXType_Char_U = 4,
                //case CXTypeKind.CXType_UChar = 5,
                //case CXTypeKind.CXType_Char16 = 6,
                //case CXTypeKind.CXType_Char32 = 7,
                case CXTypeKind.CXType_UShort: return typeof(ushort);
                case CXTypeKind.CXType_UInt: return typeof(uint);
                case CXTypeKind.CXType_ULong: return typeof(uint);
                case CXTypeKind.CXType_ULongLong: return typeof(ulong);
                //case CXTypeKind.CXType_UInt128: return typeof(ulong);
                //case CXTypeKind.CXType_Char_S: return typeof(uint);
                //case CXTypeKind.CXType_SChar: return typeof(uint);
                //case CXTypeKind.CXType_WChar: return typeof(uint);
                case CXTypeKind.CXType_Short: return typeof(short);
                case CXTypeKind.CXType_Int: return typeof(int);
                case CXTypeKind.CXType_Long: return typeof(int);
                case CXTypeKind.CXType_LongLong: return typeof(long);
                //case CXTypeKind.CXType_Int128: return typeof(long long);
                case CXTypeKind.CXType_Float: return typeof(float);
                case CXTypeKind.CXType_Double: return typeof(double);
                //case CXTypeKind.CXType_LongDouble: return typeof(long double);
                case CXTypeKind.CXType_NullPtr: return typeof(void*);
                //case CXTypeKind.CXType_Overload = 25,
                //case CXTypeKind.CXType_Dependent = 26,
                //case CXTypeKind.CXType_ObjCId = 27,
                //case CXTypeKind.CXType_ObjCClass = 28,
                //case CXTypeKind.CXType_ObjCSel = 29,
                //case CXTypeKind.CXType_Float128 = 30,
                //case CXTypeKind.CXType_Half = 0x1F,
                //case CXTypeKind.CXType_Float16 = 0x20,
                //case CXTypeKind.CXType_ShortAccum = 33,
                //case CXTypeKind.CXType_Accum = 34,
                //case CXTypeKind.CXType_LongAccum = 35,
                //case CXTypeKind.CXType_UShortAccum = 36,
                //case CXTypeKind.CXType_UAccum = 37,
                //case CXTypeKind.CXType_ULongAccum = 38,
                //case CXTypeKind.CXType_BFloat16 = 39,
                //case CXTypeKind.CXType_Ibm128 = 40,
                default: throw new InvalidOperationException();
            }
        }

        static public CXFile GetFile(this CXSourceLocation instance)
        {
            CXFile file;
            uint line, column, offset;
            instance.GetFileLocation(out file, out line, out column, out offset);
            return file;
        }
    }
}
