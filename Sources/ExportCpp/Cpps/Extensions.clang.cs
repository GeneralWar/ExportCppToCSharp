using ClangSharp.Interop;
using ExportCpp.Cpps;

namespace ExportCpp
{
    static internal partial class Extensions
    {
        static public string GetName(this CXCursor instance)
        {
            return instance.DisplayName.CString;
        }

        static public string GetFullTypeName(this CXCursor instance)
        {
            return instance.Type.Desugar.Spelling.CString;
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

        static public bool HasSign(this CXType instance)
        {
            return CXTypeKind.CXType_SChar == instance.kind || CXTypeKind.CXType_Short == instance.kind || CXTypeKind.CXType_Int == instance.kind || CXTypeKind.CXType_Long == instance.kind || CXTypeKind.CXType_Float == instance.kind || CXTypeKind.CXType_Double == instance.kind;
        }
    }
}
