using System.Runtime.InteropServices;
﻿﻿
namespace TestNamespace
{
	public enum TestEnum
	{
		/// <summary>
		/// 这是一条测试用注释
		/// </summary>
		Enum1 = 0,
		Enum2 = 1,
		Enum3 = 2,
	}

	[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 24)]
	public struct TestStruct
	{
		[FieldOffset(0)]
		public int value1;
		/// <summary>
		/// 这是一条测试用注释
		/// </summary>
		[FieldOffset(8)]
		public long value2;
		[FieldOffset(16)]
		public double value3;
	}

	[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 16)]
	public struct TestStruct1
	{
		[FieldOffset(0)]
		public int value1;
		[FieldOffset(8)]
		public long value2;
		[FieldOffset(8)]
		public double value3;
	}

	[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 16)]
	public struct TestStruct2
	{
		[FieldOffset(0)]
		public int value1;
		[FieldOffset(0)]
		public long value2;
		[FieldOffset(8)]
		public double value3;
	}

	[StructLayout(LayoutKind.Explicit, Pack = 4, Size = 8)]
	public struct TestClassValue
	{
		[FieldOffset(0)]
		public int previousValue;
		[FieldOffset(4)]
		public int currentValue;
	}
}
﻿
namespace TestNamespace1
{
	[StructLayout(LayoutKind.Explicit, Pack = 4, Size = 4)]
	public struct TestStruct1
	{
		[FieldOffset(0)]
		public int value;
	}
}

namespace TestCpp
{
	static internal unsafe class TestBindings
	{
		private const string LIBRARY_NAME = "TestCpp";

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.IntPtr create_test_class(int value);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern void test_class_set_value_change_callback(System.IntPtr instance, delegate* unmanaged[Cdecl]<nint, int, void> callback);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern void test_class_set_value_uint(System.IntPtr instance, int value);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern int test_class_get_value(System.IntPtr instance);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern TestNamespace.TestClassValue* test_class_get_value_pointer(System.IntPtr instance);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern int test_class_add(System.IntPtr instance, int value);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern int test_class_multiply(System.IntPtr instance, int value);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern TestNamespace.TestStruct test_class_export_struct(System.IntPtr instance);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern System.IntPtr create_derived_test_class(int value);

		[DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
		static internal extern int derived_test_class_substract(System.IntPtr instance, int value);
	}
}
