using TestCpp;

namespace TestCSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IntPtr instance = TestBindings.create_test_class(3);
            Console.WriteLine(TestBindings.test_class_add(instance, 1));

            TestBindings.test_class_set_value(instance, TestBindings.test_class_get_value(instance) + 1);
            Console.WriteLine(TestBindings.test_class_multiply(instance, 4));

            Console.WriteLine(TestBindings.derived_test_class_substract(instance, 4));

            Console.ReadKey(true);
        }
    }
}