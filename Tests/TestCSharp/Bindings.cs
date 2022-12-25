using System.Runtime.InteropServices;

namespace TestCpp
{
	static internal class TestBindings
	{
		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.IntPtr create_test_class(System.Int32 value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern unsafe void test_class_set_value_change_callback(IntPtr instance, delegate* unmanaged[Cdecl]<nint, int, void> callback);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern void test_class_set_value(IntPtr instance, System.Int32 value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.Int32 test_class_get_value(IntPtr instance);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.Int32 test_class_add(IntPtr instance, System.Int32 value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.Int32 test_class_multiply(IntPtr instance, System.Int32 value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.IntPtr create_derived_test_class(System.Int32 value);

		[DllImport("TestCpp", CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.Int32 derived_test_class_substract(IntPtr instance, System.Int32 value);
	}
}
