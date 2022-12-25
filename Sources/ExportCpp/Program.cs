using General;
using General.Tracers;

namespace ExportCpp
{
    internal class Program
    {
        private const string CPP_PROJECT_EXTENSION = ".vcxproj";
        private const string SOLUTION_EXTENSION = ".sln";

        private const string ARGUMENT_INCLUDE_DIRECTORY = "include-directory";
        private const string ARGUMENT_INCLUDE_HEADER_FILE = "include";
        private const string ARGUMENT_SOLUTION_FILENAME = "solution";
        private const string ARGUMENT_DEFINE = "define";

        private const string ARGUMENT_EXPORT_FILENAME = "export-filename";
        private const string ARGUMENT_BINDING_FILENAME = "binding-filename";
        private const string ARGUMENT_LIBRARY_NAME = "library-name";
        private const string ARGUMENT_BINDING_NAMESPACE = "binding-namespace";
        private const string ARGUMENT_BINDING_CLASSNAME = "binding-classname";

        static void Main(string[] args)
        {
            Tracer.onLog += onTracerLog;

            CommandLine commandLine = new CommandLine();
            commandLine.SetHelpKey("-h|--help");
            commandLine.SetUsages($"ExportCpp [--command <--argument>] ... project.{CPP_PROJECT_EXTENSION}");
            commandLine.Require($"--{ARGUMENT_EXPORT_FILENAME}", "Set filename where stores exported C/C++ functions", true);
            commandLine.Require($"--{ARGUMENT_BINDING_FILENAME}", "Set filename where stores C# binding functions", true);
            commandLine.Require($"--{ARGUMENT_BINDING_CLASSNAME}", "Set class name where stores C# binding functions", true);
            commandLine.Require($"--{ARGUMENT_LIBRARY_NAME}", "Set library name used in C# DllImport, it should always equals to C++ dll's name", true);
            commandLine.Require($"--{ARGUMENT_SOLUTION_FILENAME}", $"Set solution (*{SOLUTION_EXTENSION}) filename", true);
            commandLine.Option($"--{ARGUMENT_BINDING_NAMESPACE}", $"Set namespace where stores C# binding functions", true);
            commandLine.Option($"--{ARGUMENT_INCLUDE_DIRECTORY}", $"Pass --include-directory to clang", true);
            commandLine.Option($"--{ARGUMENT_INCLUDE_HEADER_FILE}", $"Pass --include to clang", true);
            commandLine.Option($"--{ARGUMENT_DEFINE}", $"Pass -D <macro>=<value> to clang", true);
            commandLine.Parse(args);

#if !RELEASE
            try
            {
                Analyze(commandLine);
            }
            catch (Exception e)
            {
                ConsoleLogger.LogException(e);
            }

            Console.ReadKey(true);
#else
            Analyze(commandLine);
#endif
        }

        static private void Analyze(CommandLine commandLine)
        {
            string exportFilename = commandLine.GetString(ARGUMENT_EXPORT_FILENAME) ?? throw new InvalidOperationException("Missing export filename");
            string bindingFilename = commandLine.GetString(ARGUMENT_BINDING_FILENAME) ?? throw new InvalidOperationException("Missing binding filename");
            string libraryName = commandLine.GetString(ARGUMENT_LIBRARY_NAME) ?? throw new InvalidOperationException("Missing library filename");
            string bindingClassname = commandLine.GetString(ARGUMENT_BINDING_CLASSNAME) ?? throw new InvalidOperationException("Missing binding classname");
            string solutionFilename = commandLine.GetString(ARGUMENT_SOLUTION_FILENAME) ?? throw new InvalidOperationException($"Missing solution filename (*{SOLUTION_EXTENSION})");

            string? projectFilename = commandLine.ExtraArguments.Where(e => e.EndsWith(CPP_PROJECT_EXTENSION)).FirstOrDefault();
            if (string.IsNullOrEmpty(projectFilename))
            {
                ConsoleLogger.LogWarning($"Missing C++ project filename *{CPP_PROJECT_EXTENSION}");
                commandLine.PrintHelp();
                return;
            }

            if (!File.Exists(projectFilename))
            {
                ConsoleLogger.LogWarning($"Invalid project file path {projectFilename}");
                return;
            }

            CppAnalyzer analyzer = new CppAnalyzer(Path.GetFullPath(solutionFilename), Path.GetFullPath(projectFilename), Path.GetFullPath(exportFilename), Path.GetFullPath(bindingFilename), libraryName, bindingClassname);
            analyzer.SetNamespace(commandLine.GetString(ARGUMENT_BINDING_NAMESPACE));
            analyzer.IncludeDirectories.AddRange(commandLine.GetStringArray(ARGUMENT_INCLUDE_DIRECTORY).Select(d => PathUtility.MakeDirectoryStandard(d)));
            analyzer.IncludeHeaderFiles.AddRange(commandLine.GetStringArray(ARGUMENT_INCLUDE_HEADER_FILE));
            analyzer.DefineMacros.AddRange(commandLine.GetStringArray(ARGUMENT_DEFINE));
            analyzer.Analyze();
            analyzer.ExportXml();
            analyzer.Export();
            analyzer.Bind();
        }

        private static void onTracerLog(Tracer.LogMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.message))
            {
                return;
            }

            switch (message.level)
            {
                case Tracer.LogLevel.Warning:
                    ConsoleLogger.LogWarning(message.message);
                    break;
                case Tracer.LogLevel.Error:
                case Tracer.LogLevel.Exception:
                    ConsoleLogger.LogError(message.message);
                    break;
            }
        }
    }
}