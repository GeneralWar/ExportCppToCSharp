using ClangSharp;
using ClangSharp.Interop;
using General;
using General.Tracers;
using System.Text.RegularExpressions;
using System.Xml;

namespace ExportCpp
{
    public partial class CppAnalyzer
    {
        private Namespace initializeGlobal()
        {
            Namespace global = new Namespace("global");
            global.SetAsRoot();

            global.AddDeclaration(this.initializeStds(global));

            return global;
        }

        private Namespace initializeStds(Namespace global)
        {
            Namespace std = new Namespace("std");

            std.AddDeclaration(new Cpps.Builtins.Stds.String());

            return std;
        }
    }
}
