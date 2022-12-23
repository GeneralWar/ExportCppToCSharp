using ClangSharp.Interop;
using General;

namespace ExportCpp
{
    internal class CppContext
    {
        public string Filename { get; init; }
        public string FileContent { get; init; }

        public Declaration? RootScope { get; private set; }
        public Stack<Declaration> mScopes = new Stack<Declaration>();
        public Stack<DeclarationCollection> mCollections = new Stack<DeclarationCollection>();
        public Stack<Namespace> mNamespaces = new Stack<Namespace>();

        private Dictionary<string, Declaration> mDeclarations = new Dictionary<string, Declaration>();
        public IEnumerable<Declaration> Declarations => mDeclarations.Values;

        private List<Declaration> mExports = new List<Declaration>();
        public IEnumerable<Declaration> Exports => mExports;

        private List<CXCursor> mCursors = new List<CXCursor>();
        public IEnumerable<CXCursor> Cursors => mCursors;

        public Namespace Global { get; init; }

        public CppContext(string filename, Namespace global) : this(filename, File.ReadAllText(filename), global) { }

        internal CppContext(string filename, string fileContent, Namespace global)
        {
            this.Filename = filename;
            this.FileContent = fileContent;

            this.Global = global;
        }

        public void PushScope(Declaration scope)
        {
            this.RootScope ??= scope;

            if (mCollections.Count > 0)
            {
                mCollections.Peek().AddDeclaration(scope);
            }

            DeclarationCollection? collection = scope as DeclarationCollection;
            if (collection is not null)
            {
                mCollections.Push(collection);
                mScopes.Push(scope);

                Namespace? @namespace = scope as Namespace;
                if (@namespace is not null)
                {
                    mNamespaces.Push(@namespace);
                }
            }
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
            return mDeclarations.TryGetValue(fullname, out declaration) ? declaration : null;
        }

        public void PopScope(Declaration scope)
        {
            if (mScopes.Peek() == scope)
            {
                mScopes.Pop();
            }

            DeclarationCollection? collection = scope as DeclarationCollection;
            if (collection is not null)
            {
                DeclarationCollection? topCollection = mCollections.Pop();
                Tracer.Assert(topCollection == collection);

                Namespace? @namespace = scope as Namespace;
                if (@namespace is not null)
                {
                    Namespace? topNamespace = mNamespaces.Pop();
                    Tracer.Assert(topNamespace == @namespace);
                }
            }
        }

        public void AppendCursor(CXCursor cursor)
        {
            mCursors.Add(cursor);
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
