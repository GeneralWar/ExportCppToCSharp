using ClangSharp.Interop;

namespace ExportCpp
{
    internal class CppContext
    {
        public string Filename { get; init; }
        public string FileContent { get; init; }

        public Declaration? RootScope { get; private set; }
        private Stack<Declaration> mScopes = new Stack<Declaration>();
        public Declaration? CurrentScope => mScopes.Count > 0 ? mScopes.Peek() : null;

        private Dictionary<string, Declaration> mDeclarations = new Dictionary<string, Declaration>();
        public IEnumerable<Declaration> Declarations => mDeclarations.Values;

        private List<Declaration> mExports = new List<Declaration>();
        public IEnumerable<Declaration> Exports => mExports;

        public Namespace Global { get; init; }

        public CppContext(string filename, Namespace global) : this(filename, File.ReadAllText(filename), global)
        {
            foreach (Declaration declaration in global.Declarations)
            {
                mDeclarations.Add(declaration.FullName, declaration);
            }
        }

        internal CppContext(string filename, string fileContent, Namespace global)
        {
            this.Filename = filename;
            this.FileContent = fileContent;

            this.Global = global;
        }

        public void PushScope(Declaration scope)
        {
            this.RootScope ??= scope;
            mScopes.Push(scope);
        }

        public void AppendDeclaration(Declaration declaration)
        {
            mDeclarations.Add(declaration.FullName, declaration);

            if (!string.IsNullOrWhiteSpace(declaration.ExportContent))
            {
                mExports.Add(declaration);
            }

            DeclarationCollection? parent = null;
            CXCursor cursor = declaration.Cursor.SemanticParent;
            while (!cursor.IsInvalid)
            {
                parent = (CXCursorKind.CXCursor_Namespace == cursor.kind ? this.GetDeclaration(cursor.GetFullNamespace()) : this.GetDeclaration(cursor.GetFullTypeName()) as DeclarationCollection) as DeclarationCollection;
                if (parent is not null)
                {
                    break;
                }

                cursor = cursor.SemanticParent;
            }
            parent ??= this.Global as DeclarationCollection;
            (parent ?? throw new InvalidOperationException()).AddDeclaration(declaration);
        }

        public Declaration? GetDeclaration(string fullname)
        {
            Declaration? declaration;
            if (!mDeclarations.TryGetValue(fullname, out declaration))
            {
                string[] parts = fullname.Split("::");
                if (parts.Length > 1)
                {
                    mDeclarations.TryGetValue(parts[0], out declaration);
                    for (int i = 1; i < parts.Length && declaration is not null; ++i)
                    {
                        declaration = (declaration as DeclarationCollection)?.GetDeclaration(parts[i]);
                    }
                }
            }
            return declaration;
        }

        public void PopScope(Declaration scope)
        {
            if (mScopes.Peek() == scope)
            {
                mScopes.Pop();
            }
        }

        public override string ToString()
        {
            return Path.GetFileName(this.Filename);
        }
    }

    internal class CppExportContext
    {
        public StreamWriter Writer { get; init; }

        private List<Declaration> mDeclarations = new List<Declaration>();
        private HashSet<string> mFilenames = new HashSet<string>();
        public IEnumerable<string> Filenames => mFilenames;

        public int TabCount { get; set; } = 0;

        public CppExportContext(StreamWriter writer)
        {
            this.Writer = writer;
        }

        public void AppendDeclaration(Declaration declaration)
        {
            mDeclarations.Add(declaration);
            mFilenames.Add(declaration.Filename);
        }
    }

    internal class CSharpBindingContext
    {
        public StreamWriter Writer { get; init; }

        public int TabCount { get; set; } = 0;

        public CSharpBindingContext(StreamWriter writer)
        {
            this.Writer = writer;
        }
    }
}
