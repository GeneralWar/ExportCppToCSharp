using ClangSharp.Interop;

namespace ExportCpp
{
    internal class FailedDeclaration
    {
        public Declaration declaration { get; init; }
        public Exception exception { get; init; }

        internal FailedDeclaration(Declaration declaration, Exception exception)
        {
            this.declaration = declaration;
            this.exception = exception;
        }
    }

    internal class CppContext
    {
        public string Filename { get; init; }
        public string FileContent { get; init; }

        public CppAnalyzer Analyzer { get; protected set; }
        public Global Global => this.Analyzer.Global;

        public Declaration? RootScope { get; private set; }
        private Stack<DeclarationCollection> mScopes = new Stack<DeclarationCollection>();
        public DeclarationCollection CurrentScope => mScopes.Count > 0 ? mScopes.Peek() : this.Global;

        private Dictionary<string, Declaration> mDeclarations = new Dictionary<string, Declaration>();
        public IEnumerable<Declaration> Declarations => mDeclarations.Values;

        private List<Declaration> mExports = new List<Declaration>();
        public IEnumerable<Declaration> Exports => mExports;

        private List<FailedDeclaration> mFailedDeclarations = new List<FailedDeclaration>();
        public IEnumerable<FailedDeclaration> FailedDeclarations => mFailedDeclarations;

        public CppContext(string filename, CppAnalyzer analyzer) : this(filename, File.Exists(filename) ? File.ReadAllText(filename) : "", analyzer)
        {
            foreach (Declaration declaration in analyzer.Global.Declarations)
            {
                mDeclarations.Add(declaration.FullName, declaration);
            }
        }

        internal CppContext(string filename, string fileContent, CppAnalyzer analyzer)
        {
            this.Filename = filename;
            this.FileContent = fileContent;

            this.Analyzer = analyzer;
        }

        public void PushScope(DeclarationCollection scope)
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
                string[] parts = fullname.Split(Namespace.SEPARATOR);
                if (parts.Length > 1)
                {
                    mDeclarations.TryGetValue(parts[0], out declaration);
                    for (int i = 1; i < parts.Length && declaration is not null; ++i)
                    {
                        declaration = (declaration as DeclarationCollection)?.GetDeclaration(parts[i]);
                    }
                }

                declaration ??= this.Global.GetDeclaration(fullname);
            }
            return declaration;
        }

        public void PopScope(DeclarationCollection scope)
        {
            if (mScopes.Peek() == scope)
            {
                mScopes.Pop();
            }
        }

        public void AppendFailedDeclaration(Declaration declaration, Exception exception)
        {
            mFailedDeclarations.Add(new FailedDeclaration(declaration, exception));
        }

        public void ClearFailedDeclarations()
        {
            mFailedDeclarations.Clear();
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
