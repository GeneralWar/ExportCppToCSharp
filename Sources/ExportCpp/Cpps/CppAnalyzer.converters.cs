using General.Tracers;
using System.Diagnostics.Metrics;
using System.Xml.Linq;

namespace ExportCpp
{
    public partial class CppAnalyzer
    {
        public interface ITypeConverter
        {
            string MakeCppExportArgumentTypeString(Type type);
            bool CheckCppShouldCastExportArgumentTypeToInvocationType(Type type);
            string? MakeCppExportArgumentCastString(Type type, string argumentName, string targetName);
            string? MakeCppExportInvocationCastString(Type type, string content);
            string MakeCppExportReturnTypeString(Type type);
            string MakeCppExportReturnValueString(Type type, string content);
            string MakeCppExportTypeString(Type type);
        }

        public interface ITemplateTypeConverter
        {
            string MakeCppExportArgumentTypeString(ClassTemplate declaration, Type[] arguments);
            bool CheckCppShouldCastExportArgumentTypeToInvocationType(ClassTemplate declaration, Type[] arguments);
            string? MakeCppExportArgumentCastString(ClassTemplate declaration, Type[] arguments, string argumentName, string targetName);
            string? MakeCppExportInvocationCastString(ClassTemplate declaration, Type[] arguments, string content);
            string MakeCppExportReturnTypeString(ClassTemplate declaration, Type[] arguments);
            string MakeCppExportReturnValueString(ClassTemplate declaration, Type[] arguments, string content);
            string MakeCppExportTypeString(ClassTemplate declaration, Type[] arguments);
        }

        private Dictionary<string, ITypeConverter> mTypeConverters = new Dictionary<string, ITypeConverter>();
        private Dictionary<string, ITemplateTypeConverter> mTemplateTypeConverters = new Dictionary<string, ITemplateTypeConverter>();

        public void RegisterTypeConverter(string cppFuleTypeName, ITypeConverter converter)
        {
            if (!mTypeConverters.TryAdd(cppFuleTypeName, converter))
            {
                mTypeConverters[cppFuleTypeName] = converter;
            }
        }

        public string? MakeCppExportArgumentTypeString(Type type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.MakeCppExportArgumentTypeString(type) : null;
        }

        public bool? CheckCppShouldCastExportArgumentTypeToInvocationType(Type type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.CheckCppShouldCastExportArgumentTypeToInvocationType(type) : null;
        }

        public string? MakeCppExportArgumentCastString(Type type, string argumentName, string targetName)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.MakeCppExportArgumentCastString(type, argumentName, targetName) : null;
        }

        public string? MakeCppExportInvocationCastString(Type type, string content)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.MakeCppExportInvocationCastString(type, content) : null;
        }

        public string? MakeCppExportReturnTypeString(Type type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.MakeCppExportReturnTypeString(type) : null;
        }

        public string? MakeCppExportReturnValueString(Type type, string content)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.MakeCppExportReturnValueString(type, content) : null;
        }

        public string? MakeCppExportTypeString(Type type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName ?? type.Name, out converter) ? converter.MakeCppExportTypeString(type) : null;
        }

        public void RegisterTemplateTypeConverter(string cppFuleTypeName, ITemplateTypeConverter converter)
        {
            if (!mTemplateTypeConverters.TryAdd(cppFuleTypeName, converter))
            {
                mTemplateTypeConverters[cppFuleTypeName] = converter;
            }
        }

        public string? MakeCppExportArgumentTypeString(ClassTemplate declaration, Type[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportArgumentTypeString(declaration, arguments);
        }

        public bool? CheckCppShouldCastExportArgumentTypeToInvocationType(ClassTemplate declaration, Type[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.CheckCppShouldCastExportArgumentTypeToInvocationType(declaration, arguments);
        }

        public string? MakeCppExportArgumentCastString(ClassTemplate declaration, Type[] arguments, string argumentName, string targetName)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportArgumentCastString(declaration, arguments, argumentName, targetName);
        }

        public string? MakeCppExportInvocationCastString(ClassTemplate declaration, Type[] arguments, string content)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportInvocationCastString(declaration, arguments, content);
        }

        public string? MakeCppExportReturnTypeString(ClassTemplate declaration, Type[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportReturnTypeString(declaration, arguments);
        }

        public string? MakeCppExportReturnValueString(ClassTemplate declaration, Type[] arguments, string content)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportReturnValueString(declaration, arguments, content);
        }

        public string? MakeCppExportTypeString(ClassTemplate declaration, Type[] arguments)
        {
            ITemplateTypeConverter? converter;
            return mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter) ? converter.MakeCppExportTypeString(declaration, arguments) : null;
        }
    }
}
