#pragma once

namespace TestNamespace
{
	class TestClass;

	EXPORT_STRUCT(TestStruct);
	struct TestStruct
	{
		int value1;
		/// <summary>
		/// 这是一条测试用注释
		/// </summary>
		long long int value2;
		double value3;
	};

	EXPORT_STRUCT(TestStruct1);
	struct TestStruct1
	{
		int value1;
		union 
		{
			long long int value2;
			double value3;
		};
	};

	EXPORT_STRUCT(TestStruct2);
	struct TestStruct2
	{
		union
		{
			int value1;
			long long int value2;
		};
		double value3;
	};

	enum TestEnum : unsigned short
	{
		TestEnum1,
		TestEnum2,
		TestEnum3,
	};

	EXPORT_ENUM(TestEnum)
	enum class TestEnumClass
	{
		/// <summary>
		/// 这是一条测试用注释
		/// </summary>
		Enum1,
		Enum2,
		Enum3,
	};

	EXPORT_STRUCT(TestClassValue);
	struct TestClassValue
	{
		int previousValue;
		int currentValue;
	};

	EXPORT_CLASS(TestClass, test_class)
	class TestClass
	{
	public:
		EXPORT_FUNCTION_POINTER
		typedef void (*ValueChange)(TestClass* instance, int value);
	private:
		TestClassValue mValue;
		ValueChange mValueChangeCallback;
	public:
		EXPORT_CONSTRUCTOR(create_test_class);
		TestClass(const int& value);
		virtual ~TestClass();

		EXPORT_FUNCTION(set_value_change_callback, 0);
		void SetValueChangeCallback(ValueChange callback);

		EXPORT_FUNCTION(set_value, 0);
		void SetValue(const int& value);

		EXPORT_FUNCTION(set_value_uint, 0);
		void SetValue(const unsigned int& value);

		EXPORT_FUNCTION(get_value, 0);
		int GetValue();

		EXPORT_FUNCTION(get_value_pointer, nullptr);
		const TestClassValue* GetValuePointer() const;

		EXPORT_FUNCTION(add, 0);
		int Add(const int& value);
		EXPORT_FUNCTION(multiply, 0);
		int Multiply(const int& value);

		EXPORT_FUNCTION(export_struct, { });
		TestStruct ExportStruct() const;
	};

	EXPORT_CLASS(TestClass, derived_test_class);
	class DerivedTestClass : public TestClass
	{
	public:
		EXPORT_CONSTRUCTOR(create_derived_test_class);
		DerivedTestClass(const int& value);
		~DerivedTestClass();

		EXPORT_FUNCTION(substract, MININT);
		int Substract(const int& value);
	};
}

namespace TestNamespace1
{
	EXPORT_STRUCT(TestStruct1)
	struct TestStruct1
	{
		int value;
	};
}