using System.Runtime.InteropServices;

namespace TestCpp
{
	static internal class TestBindings
	{
		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.IntPtr create_test_class(int value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern unsafe void test_class_set_value_change_callback(System.IntPtr instance, delegate* unmanaged[Cdecl]<nint, int, void> callback);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern void test_class_set_value(System.IntPtr instance, int value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern int test_class_get_value(System.IntPtr instance);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern int test_class_add(System.IntPtr instance, int value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern int test_class_multiply(System.IntPtr instance, int value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.IntPtr create_derived_test_class(int value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern int derived_test_class_substract(System.IntPtr instance, int value);
	}
}
