using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TestCpp;

namespace TestCSharp
{
    internal class Program
    {
        static unsafe void Main(string[] args)
        {
            IntPtr instance = TestBindings.create_test_class(3);
            TestBindings.test_class_set_value_change_callback(instance, &OnInstanceValueChange);

            Console.WriteLine(TestBindings.test_class_add(instance, 1));

            TestBindings.test_class_set_value(instance, TestBindings.test_class_get_value(instance) + 1);
            Console.WriteLine(TestBindings.test_class_multiply(instance, 4));

            Console.WriteLine(TestBindings.derived_test_class_substract(instance, 4)); // we created a TestClass instance, not a DerivedTestClass instance, so it will return default value set by EXPORT_FUNCTION

            Console.ReadKey(true);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static private void OnInstanceValueChange(IntPtr instance, int value)
        {
            Trace.Assert(TestBindings.test_class_get_value(instance) == value);
            Console.WriteLine($"TestBindings.test_class_get_value(instance) == value");
        }
    }
}