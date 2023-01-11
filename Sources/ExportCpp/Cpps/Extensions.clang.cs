using ClangSharp.Interop;
using System.Diagnostics;
using Type = System.Type;

namespace ExportCpp
{
    static public partial class Extensions
    {
        static public string GetName(this CXCursor instance)
        {
            return instance.Spelling.CString;
        }

        static public string GetFullTypeName(this CXCursor instance)
        {
            if (CXCursorKind.CXCursor_ClassTemplate == instance.kind)
            {
                return $"{instance.GetFullNamespace()}{Namespace.SEPARATOR}{instance.Spelling.CString}";
            }
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
            return string.Join(Namespace.SEPARATOR, names);
        }

        static public string GetFullName(this CXCursor instance)
        {
            if (instance.IsInvalid)
            {
                return "";
            }

            switch (instance.kind)
            {
                case CXCursorKind.CXCursor_Namespace:
                    return instance.GetFullNamespace();
                case CXCursorKind.CXCursor_ClassDecl:
                case CXCursorKind.CXCursor_StructDecl:
                case CXCursorKind.CXCursor_EnumDecl:
                case CXCursorKind.CXCursor_ClassTemplate:
                case CXCursorKind.CXCursor_ParmDecl:
                    return instance.GetFullTypeName();
                case CXCursorKind.CXCursor_CXXMethod:
                case CXCursorKind.CXCursor_FieldDecl:
                case CXCursorKind.CXCursor_UnexposedDecl:
                    Stack<string> names = new Stack<string>();
                    CXCursor cursor = instance;
                    while (!cursor.IsInvalid && !cursor.IsTranslationUnit)
                    {
                        names.Push(cursor.GetName());
                        cursor = cursor.SemanticParent;
                    }
                    return string.Join(Namespace.SEPARATOR, names);
                case CXCursorKind.CXCursor_Constructor:
                    return $"{instance.ThisObjectType.Spelling.CString}{Namespace.SEPARATOR}{instance.Name.CString}";
                case CXCursorKind.CXCursor_TypedefDecl:
                    if (CX_TypeClass.CX_TypeClass_FunctionProto == instance.Type.PointeeType.TypeClass)
                    {
                        return instance.Spelling.CString;
                    }
                    Debugger.Break();
                    throw new NotImplementedException();
                default:
                    Debugger.Break();
                    throw new NotImplementedException();
            }
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
            if (CXTypeKind.CXType_Typedef == instance.kind)
            {
                return instance.Spelling.CString;
            }
            return instance.Desugar.Spelling.CString;
        }

        static public bool IsConst(this CXType instance)
        {
            if (CXTypeKind.CXType_Pointer == instance.kind || CXTypeKind.CXType_LValueReference == instance.kind || CXTypeKind.CXType_RValueReference == instance.kind)
            {
                return instance.PointeeType.IsConst();
            }
            return instance.IsConstQualified;
        }

        static public bool IsArray(this CXType instance)
        {
            return CXTypeKind.CXType_ConstantArray == instance.kind;
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

        static public string GetTemplateName(this CXType instance)
        {
            switch (instance.TypeClass)
            {
                case CX_TypeClass.CX_TypeClass_TemplateSpecialization:
                    return instance.TemplateName.AsTemplateDecl.GetFullTypeName();
                case CX_TypeClass.CX_TypeClass_Record:
                case CX_TypeClass.CX_TypeClass_Elaborated:
                case CX_TypeClass.CX_TypeClass_SubstTemplateTypeParm:
                    return instance.CanonicalType.Declaration.SpecializedCursorTemplate.GetFullTypeName();
                default:
                    Debugger.Break();
                    throw new NotImplementedException();
            }
        }

        static public Type ToBuiltinType(this CXType instance)
        {
            switch (instance.kind)
            {
                case CXTypeKind.CXType_Void: return typeof(void);
                case CXTypeKind.CXType_Bool: return typeof(bool);
                //case CXTypeKind.CXType_Char_U = 4,
                case CXTypeKind.CXType_UChar: return typeof(byte);
                //case CXTypeKind.CXType_Char16 = 6,
                //case CXTypeKind.CXType_Char32 = 7,
                case CXTypeKind.CXType_UShort: return typeof(ushort);
                case CXTypeKind.CXType_UInt: return typeof(uint);
                case CXTypeKind.CXType_ULong: return typeof(uint);
                case CXTypeKind.CXType_ULongLong: return typeof(ulong);
                //case CXTypeKind.CXType_UInt128: return typeof(ulong);
                case CXTypeKind.CXType_Char_S: return typeof(sbyte);
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
                default:
                    Debugger.Break();
                    throw new InvalidOperationException();
            }
        }

        static public CXFile GetFile(this CXSourceLocation instance)
        {
            CXFile file;
            uint line, column, offset;
            instance.GetFileLocation(out file, out line, out column, out offset);
            return file;
        }

        static public int GetOffset(this CXSourceLocation instance)
        {
            CXFile file;
            uint line, column, offset;
            instance.GetFileLocation(out file, out line, out column, out offset);
            return (int)offset;
        }
    }
}
