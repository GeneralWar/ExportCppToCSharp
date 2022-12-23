using General;
using General.Tracers;
using System.Reflection;

namespace ExportCpp
{
    internal class Program
    {
        private const string CPP_PROJECT_EXTENSION = ".vcxproj";

        private const string ARGUMENT_EXPORT_FILENAME = "export-filename";
        private const string ARGUMENT_EXPORT_PCH_FILENAME = "export-pch-filename";
        private const string ARGUMENT_BINDING_FILENAME = "binding-filename";
        private const string ARGUMENT_LIBRARY_NAME = "library-name";
        private const string ARGUMENT_BINDING_NAMESPACE = "binding-namespace";
        private const string ARGUMENT_BINDING_CLASSNAME = "binding-classname";

        static void Main(string[] args)
        {
            Analyze();
#if DEVELOP
            Console.ReadKey(true);
#endif
        }

        static private void Analyze()
        {
            CommandLine commandLine = CommandLine.Create();
            if (commandLine.Contains("h") || commandLine.Contains("help"))
            {
                PrintHelp();
                return;
            }

            string? exportFilename = commandLine.GetString(ARGUMENT_EXPORT_FILENAME);
            if (string.IsNullOrEmpty(exportFilename))
            {
                ConsoleLogger.LogWarning("Missing export filename");
                PrintHelp();
                return;
            }

            string? bindingFilename = commandLine.GetString(ARGUMENT_BINDING_FILENAME);
            if (string.IsNullOrEmpty(bindingFilename))
            {
                ConsoleLogger.LogWarning("Missing binding filename");
                PrintHelp();
                return;
            }

            string? libraryName = commandLine.GetString(ARGUMENT_LIBRARY_NAME);
            if (string.IsNullOrEmpty(libraryName))
            {
                ConsoleLogger.LogWarning("Missing library filename");
                PrintHelp();
                return;
            }

            string? bindingClassname = commandLine.GetString(ARGUMENT_BINDING_CLASSNAME);
            if (string.IsNullOrWhiteSpace(bindingClassname))
            {
                ConsoleLogger.LogWarning("Missing binding classname");
                PrintHelp();
                return;
            }

            string? projectFilename = commandLine.ExtraArguments.Where(e => e.EndsWith(CPP_PROJECT_EXTENSION)).FirstOrDefault();
            if (string.IsNullOrEmpty(projectFilename))
            {
                PrintHelp();
                return;
            }

            if (!File.Exists(projectFilename))
            {
                ConsoleLogger.LogWarning($"Invalid project file path {projectFilename}");
                return;
            }

            ConsoleLogger.Log($"Try to analyze project {projectFilename}");
            CppAnalyzer analyzer = new CppAnalyzer(Path.GetFullPath(projectFilename), Path.GetFullPath(exportFilename), Path.GetFullPath(bindingFilename), libraryName, bindingClassname);
            analyzer.SetNamespace(commandLine.GetString(ARGUMENT_BINDING_NAMESPACE));
            analyzer.SetExportPchFilename(commandLine.GetString(ARGUMENT_EXPORT_PCH_FILENAME));
            analyzer.Analyze();
            analyzer.Export();
            analyzer.ExportXml();
            analyzer.Bind();
        }

        static private void PrintHelp()
        {
            Assembly current = typeof(Program).Assembly;
            ConsoleLogger.Log($"Usage: {current.GetName().Name} [-h|--help] <--{ARGUMENT_EXPORT_FILENAME}> filename> <--{ARGUMENT_EXPORT_PCH_FILENAME} filename> <--{ARGUMENT_BINDING_FILENAME}> filename> <--{ARGUMENT_LIBRARY_NAME} name> [--{ARGUMENT_BINDING_NAMESPACE} namespace] <--{ARGUMENT_BINDING_CLASSNAME}> classname> <*{CPP_PROJECT_EXTENSION}> ");
        }
    }
}