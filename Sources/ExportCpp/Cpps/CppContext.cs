using ClangSharp.Interop;
using General;
using System.Text;

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

        public override string ToString()
        {
            return this.declaration.ToString();
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

        private List<Declaration> mExports = new List<Declaration>();
        public IEnumerable<Declaration> Exports => mExports;

        private List<FailedDeclaration> mFailedDeclarations = new List<FailedDeclaration>();
        public IEnumerable<FailedDeclaration> FailedDeclarations => mFailedDeclarations;

        public CppContext(string filename, CppAnalyzer analyzer) : this(filename, File.Exists(filename) ? File.ReadAllText(filename) : "", analyzer) { }

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
            this.Analyzer.AppendDeclaration(declaration);

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
            Declaration? declaration = this.Analyzer.GetDeclaration(fullname);
            if (declaration is null)
            {
                string[] parts = fullname.Split(Namespace.SEPARATOR);
                if (parts.Length > 1)
                {
                    declaration = this.Analyzer.GetDeclaration(parts[0]);
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

    internal class WriterContext
    {
        protected StreamWriter OriginalWriter { get; set; }
        public StreamWriter Writer { get; protected set; }
        public int TabCount { get; set; } = 0;

        public WriterContext(StreamWriter writer)
        {
            this.OriginalWriter = this.Writer = writer;
        }

        public void Write(string content)
        {
            this.Writer.Write(this.TabCount, content);
        }

        public void WriteLine()
        {
            this.Writer.WriteLine();
        }

        public void WriteLine(string content)
        {
            this.Writer.WriteLine(this.TabCount, content);
        }
    }

    internal class CppExportContext : WriterContext
    {
        private List<Declaration> mDeclarations = new List<Declaration>();
        private HashSet<string> mFilenames = new HashSet<string>();
        public IEnumerable<string> Filenames => mFilenames;
        public IEnumerable<Declaration> Declarations => mDeclarations;

        public CppExportContext(StreamWriter writer) : base(writer) { }

        public void AppendDeclaration(Declaration declaration)
        {
            mDeclarations.Add(declaration);
            mFilenames.Add(declaration.Filename);
        }
    }

    internal class CSharpBindingContext : WriterContext
    {
        internal class BindingScope
        {
            public Namespace Scope { get; }
            public HashSet<Enum> Enums { get; }
            public HashSet<Struct> Structs { get; }
            internal int BoundDeclarations { get; set; }

            private MemoryStream mStream = new MemoryStream();
            public StreamWriter Writer { get; init; }
            public string Content { get; private set; } = "";

            public BindingScope(Namespace scope)
            {
                this.Scope = scope;
                this.Enums = new HashSet<Enum>();
                this.Structs = new HashSet<Struct>();
                this.Writer = new StreamWriter(mStream, Encoding.UTF8, leaveOpen: true);
            }

            public void AppendEnum(Enum declaration)
            {
                this.Enums.Add(declaration);
                this.updateBoundDeclarations();
            }

            public void AppendStruct(Struct declaration)
            {
                this.Structs.Add(declaration);
                this.updateBoundDeclarations();
            }

            private void updateBoundDeclarations()
            {
                this.BoundDeclarations = this.Enums.Count + this.Structs.Count;
            }

            public void Close()
            {
                this.Writer.Close();
                mStream.Close();
                this.Content = Encoding.UTF8.GetString(mStream.ToArray());
            }

            public override string ToString() => this.Scope.ToString();
        }


        private Stack<BindingScope> mScopes = new Stack<BindingScope>();
        public HashSet<Enum> ExportedEnums => mScopes.Peek().Enums;
        public HashSet<Struct> ExportedStructs => mScopes.Peek().Structs;
        public HashSet<Function> ExportedFunctions { get; }
        public int CurrentExportedDeclarations => mScopes.Peek().BoundDeclarations;

        public CSharpBindingContext(StreamWriter writer) : base(writer)
        {
            this.ExportedFunctions = new HashSet<Function>();
        }

        public void PushScope(Namespace scope)
        {
            BindingScope instance = new BindingScope(scope);
            mScopes.Push(instance);
            this.Writer = instance.Writer;
        }

        public void AppendEnum(Enum declaration)
        {
            mScopes.Peek().AppendEnum(declaration);
        }

        public void AppendStruct(Struct declaration)
        {
            mScopes.Peek().AppendStruct(declaration);
        }

        public void AppendFunction(Function declaration)
        {
            this.ExportedFunctions.Add(declaration);
        }

        public void PopScope(Namespace expectedScope)
        {
            BindingScope scope = mScopes.Pop();
            Tracer.Assert(scope.Scope == expectedScope);

            scope.Close();

            if (mScopes.Count > 0)
            {
                BindingScope top = mScopes.Peek();
                top.BoundDeclarations += scope.BoundDeclarations;
                this.Writer = top.Writer;
            }
            else
            {
                this.Writer = this.OriginalWriter;
            }

            if (scope.BoundDeclarations > 0)
            {
                this.Write(scope.Content);
            }
        }
    }
}
