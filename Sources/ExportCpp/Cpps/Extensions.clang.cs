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
                return instance.PointeeType.GetOriginalTypeName();
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

        static public CXFile GetFile(this CXSourceLocation instance)
        {
            CXFile file;
            uint line, column, offset;
            instance.GetFileLocation(out file, out line, out column, out offset);
            return file;
        }
    }
}
