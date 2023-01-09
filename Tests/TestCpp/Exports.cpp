#include "pch.h"
#include "TestClass.hpp"

extern "C"
{
	__declspec (dllexport) TestNamespace::TestClass* create_test_class(int value)
	{
		return new TestNamespace::TestClass(value);
	}

	__declspec (dllexport) void test_class_set_value_change_callback(TestNamespace::TestClass* instance, TestNamespace::TestClass::ValueChange callback)
	{
		if (!instance) return;
		instance->SetValueChangeCallback(callback);
	}

	__declspec (dllexport) void test_class_set_value_uint(TestNamespace::TestClass* instance, int value)
	{
		if (!instance) return;
		instance->SetValue(value);
	}

	__declspec (dllexport) int test_class_get_value(TestNamespace::TestClass* instance)
	{
		if (!instance) return 0;
		return instance->GetValue();
	}

	__declspec (dllexport) const TestNamespace::TestClassValue* test_class_get_value_pointer(TestNamespace::TestClass* instance)
	{
		if (!instance) return nullptr;
		return instance->GetValuePointer();
	}

	__declspec (dllexport) int test_class_add(TestNamespace::TestClass* instance, int value)
	{
		if (!instance) return 0;
		return instance->Add(value);
	}

	__declspec (dllexport) int test_class_multiply(TestNamespace::TestClass* instance, int value)
	{
		if (!instance) return 0;
		return instance->Multiply(value);
	}

	__declspec (dllexport) TestNamespace::TestStruct test_class_export_struct(TestNamespace::TestClass* instance)
	{
		if (!instance) return { };
		return instance->ExportStruct();
	}

	__declspec (dllexport) TestNamespace::DerivedTestClass* create_derived_test_class(int value)
	{
		return new TestNamespace::DerivedTestClass(value);
	}

	__declspec (dllexport) int derived_test_class_substract(TestNamespace::TestClass* instance, int value)
	{
		if (!instance) return MININT;
		TestNamespace::DerivedTestClass* derived = dynamic_cast<TestNamespace::DerivedTestClass*>(instance);
		if (!derived) return MININT;
		return derived->Substract(value);
	}
}
