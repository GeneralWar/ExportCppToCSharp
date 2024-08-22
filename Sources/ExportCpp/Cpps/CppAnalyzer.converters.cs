namespace ExportCpp
{
    public partial class CppAnalyzer
    {
        /// <summary>
        /// 将C++类型转换为C类型
        /// <para/>
        /// <example>
        /// 例如有C++函数
        /// <code>AssetType CheckAssetType(const std::filesystem::path&amp; filename);</code>
        /// 被导出为
        /// <code> 
        /// int asset_check_asset_type(const char* filename)
        /// {
        ///     return static_cast&lt;int&gt;(CheckAssetType(filename));
        /// }
        /// </code>
        /// </example>
        /// 那么我们称'std::filesystem::path'为执行参数类型，'char*'为导出参数类型，'AssetType'为执行返回类型，'int'为导出返回类型
        /// </summary>
        public interface ITypeConverter
        {
            /// <summary>
            /// 将执行参数类型转换为导出参数类型
            /// <para>例如，将'std::filesystem::path'声明为'char*'</para>
            /// </summary>
            string MakeCppExportArgumentTypeString(DeclarationType type);

            /// <summary>
            /// 判断导出参数类型是否需要被转换为执行参数类型
            /// <para>例如，'char*'是否需要显式转换为'std::filesystem::path'</para>
            /// </summary>
            bool CheckCppShouldCastExportArgumentTypeToInvocationType(DeclarationType type);

            /********************** 合并这两步 **********************/
            /// <summary>
            /// 创建将导出参数类型转换为执行参数类型的字符串，用以生成函数执行内容
            /// <para>例如，'char*'不需要显式转换为'std::filesystem::path'，则可以返回null</para>
            /// </summary>
            string? MakeCppExportArgumentCastString(DeclarationType type, string argumentName, string targetName);

            /// <summary>
            /// 创建将导出参数类型转换为执行参数类型的字符串，用以生成函数执行内容
            /// <para>例如，'char*'不需要显式转换为'std::filesystem::path'，则可以返回null</para>
            /// </summary>
            string? MakeCppExportInvocationCastString(DeclarationType type, string content);
            /********************** 合并这两步 **********************/

            /// <summary>
            /// 将执行返回类型转换为导出返回类型
            /// <para>例如，'AssetType'是包含C++特性的enum class类型，需要声明为C兼容的int类型</para>
            /// </summary>
            string MakeCppExportReturnTypeString(DeclarationType type);

            /// <summary>
            /// 将执行返回内容转换为导出返回内容
            /// <para>例如，'AssetType'是包含C++特性的enum class类型，需要通过'static_cast&lt;int&gt;(value)'来转换为C兼容的int类型</para>
            /// </summary>
            string MakeCppExportReturnValueString(DeclarationType type, string content);

            /// <summary>
            /// 将C++类型转换为C#类型
            /// <para>例如，将'unsigned int'转换为'uint'</para>
            /// </summary>
            string MakeCSharpBindingArgumentTypeString(DeclarationType type);
        }

        /// <summary>
        /// 将C++类型转换为C类型
        /// <para/>
        /// <example>
        /// 例如有C++函数
        /// <code>std::string CheckAssetName(const std::string&amp; filename);</code>
        /// 被导出为
        /// <code> 
        /// const char* asset_check_asset_type(const char* filename)
        /// {
        ///     return clone_string(CheckAssetName(filename).c_str());
        /// }
        /// </code>
        /// </example>
        /// 那么我们称'std::string'为执行参数类型，'char*'为导出参数类型，'std::string'为执行返回类型，'char*'为导出返回类型
        /// <para>注：std::string的完整声明为using string  = basic_string&lt;char, char_traits&lt;char&gt;, allocator&lt;char&gt;&gt;，因此它是模板类型</para>
        /// </summary>
        public interface ITemplateTypeConverter
        {
            /// <summary>
            /// 将执行参数类型转换为导出参数类型
            /// <para>例如，将'std::string'声明为'char*'</para>
            /// </summary>
            string MakeCppExportArgumentTypeString(ClassTemplate declaration, DeclarationType[] arguments);

            /// <summary>
            /// 判断导出参数类型是否需要被转换为执行参数类型
            /// <para>例如，'char*'是否需要显式转换为'std::string'</para>
            /// </summary>
            bool CheckCppShouldCastExportArgumentTypeToInvocationType(ClassTemplate declaration, DeclarationType[] arguments);

            /********************** 合并这两步 **********************/
            /// <summary>
            /// 创建将导出参数类型转换为执行参数类型的字符串，用以生成函数执行内容
            /// <para>例如，'char*'不需要显式转换为'std::string'，则可以返回null</para>
            /// </summary>
            string? MakeCppExportArgumentCastString(ClassTemplate declaration, DeclarationType[] arguments, string argumentName, string targetName);

            /// <summary>
            /// 创建将导出参数类型转换为执行参数类型的字符串，用以生成函数执行内容
            /// <para>例如，'char*'不需要显式转换为'std::string'，则可以返回null</para>
            /// </summary>
            string? MakeCppExportInvocationCastString(ClassTemplate declaration, DeclarationType[] arguments, string content);

            /// <summary>
            /// 将执行返回类型转换为导出返回类型
            /// <para>例如，'std::string'是C++模板类型，需要声明为C兼容的'char*'类型</para>
            /// </summary>
            string MakeCppExportReturnTypeString(ClassTemplate declaration, DeclarationType[] arguments);

            /// <summary>
            /// 将执行返回内容转换为导出返回内容
            /// <para>例如，'std::string'是C++模板类型，需要通过'value.c_str()'来转换为C兼容的'char*'类型</para>
            /// <para>又函数调用返回的std::string是临时变量，std::string::c_str()返回的'char*'也是临时变量，因此需要通过类似'copy_string'创建一个副本</para>
            /// </summary>
            string MakeCppExportReturnValueString(ClassTemplate declaration, DeclarationType[] arguments, string content);

            /// <summary>
            /// 将C++类型转换为C#类型
            /// <para>例如，将'std::string'转换为'string'</para>
            /// </summary>
            string MakeCSharpBindingArgumentTypeString(ClassTemplate declaration, DeclarationType[] arguments);
        }

        public interface IConstructorConverter
        {
            string? MakeCppExportReturnTypeString(DeclarationType type);
            string? MakeCppExportReturnValueString(DeclarationType type, string[] arguments);
        }

        private Dictionary<string, ITypeConverter> mTypeConverters = new Dictionary<string, ITypeConverter>();
        private Dictionary<string, ITemplateTypeConverter> mTemplateTypeConverters = new Dictionary<string, ITemplateTypeConverter>();
        private IConstructorConverter? mConstructorConverter = null;

        public void RegisterTypeConverter(string cppFuleTypeName, ITypeConverter converter)
        {
            if (!mTypeConverters.TryAdd(cppFuleTypeName, converter))
            {
                mTypeConverters[cppFuleTypeName] = converter;
            }
        }

        public string? MakeCppExportArgumentTypeString(DeclarationType type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.MakeCppExportArgumentTypeString(type) : null;
        }

        public bool? CheckCppShouldCastExportArgumentTypeToInvocationType(DeclarationType type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.CheckCppShouldCastExportArgumentTypeToInvocationType(type) : null;
        }

        public string? MakeCppExportArgumentCastString(DeclarationType type, string argumentName, string targetName)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.MakeCppExportArgumentCastString(type, argumentName, targetName) : null;
        }

        public string? MakeCppExportInvocationCastString(DeclarationType type, string content)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.MakeCppExportInvocationCastString(type, content) : null;
        }

        public string? MakeCppExportReturnTypeString(DeclarationType type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.MakeCppExportReturnTypeString(type) : null;
        }

        public string? MakeCppExportReturnValueString(DeclarationType type, string content)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.MakeCppExportReturnValueString(type, content) : null;
        }

        public string? MakeCSharpBindingArgumentTypeString(DeclarationType type)
        {
            ITypeConverter? converter;
            return mTypeConverters.TryGetValue(type.FullName, out converter) ? converter.MakeCSharpBindingArgumentTypeString(type) : null;
        }

        public void RegisterTemplateTypeConverter(string cppFuleTypeName, ITemplateTypeConverter converter)
        {
            if (!mTemplateTypeConverters.TryAdd(cppFuleTypeName, converter))
            {
                mTemplateTypeConverters[cppFuleTypeName] = converter;
            }
        }

        public string? MakeCppExportArgumentTypeString(ClassTemplate declaration, DeclarationType[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportArgumentTypeString(declaration, arguments);
        }

        public bool? CheckCppShouldCastExportArgumentTypeToInvocationType(ClassTemplate declaration, DeclarationType[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.CheckCppShouldCastExportArgumentTypeToInvocationType(declaration, arguments);
        }

        public string? MakeCppExportArgumentCastString(ClassTemplate declaration, DeclarationType[] arguments, string argumentName, string targetName)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportArgumentCastString(declaration, arguments, argumentName, targetName);
        }

        public string? MakeCppExportInvocationCastString(ClassTemplate declaration, DeclarationType[] arguments, string content)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportInvocationCastString(declaration, arguments, content);
        }

        public string? MakeCppExportReturnTypeString(ClassTemplate declaration, DeclarationType[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportReturnTypeString(declaration, arguments);
        }

        public string? MakeCppExportReturnValueString(ClassTemplate declaration, DeclarationType[] arguments, string content)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCppExportReturnValueString(declaration, arguments, content);
        }

        public string? MakeCSharpBindingArgumentTypeString(ClassTemplate declaration, DeclarationType[] arguments)
        {
            ITemplateTypeConverter? converter;
            if (!mTemplateTypeConverters.TryGetValue(declaration.FullName, out converter))
            {
                Program.ConsoleLogger.LogWarning($"extern \"C\" does not support template type, so it is better to have a covnerter for template type {declaration.FullName}");
                return null;
            }

            return converter.MakeCSharpBindingArgumentTypeString(declaration, arguments);
        }

        public void RegisterConstructorConverter(IConstructorConverter converter)
        {
            mConstructorConverter = converter;
        }

        public string? MakeConstructorCppExportReturnTypeString(DeclarationType type)
        {
            return mConstructorConverter?.MakeCppExportReturnTypeString(type) ?? null;
        }

        public string? MakeConstructorCppExportReturnValueString(DeclarationType type, string[] arguments)
        {
            return mConstructorConverter?.MakeCppExportReturnValueString(type, arguments) ?? null;
        }
    }
}
