#pragma once

namespace TestNamespace
{
	enum TestEnum : unsigned short
	{
		TestEnum1,
		TestEnum2,
		TestEnum3,
	};

	EXPORT_ENUM(TestEnum)
	enum class TestEnumClass
	{
		Enum1,
		Enum2,
		Enum3,
	};

	EXPORT_CLASS(TestClass, test_class)
	class TestClass
	{
	private:
		int mValue;
	public:
		EXPORT_CONSTRUCTOR(create_test_class);
		TestClass(const int& value);
		virtual ~TestClass();

		EXPORT_FUNCTION(set_value, 0);
		void SetValue(const int& value);

		EXPORT_FUNCTION(get_value, 0);
		int GetValue();

		EXPORT_FUNCTION(add, 0);
		int Add(const int& value);
		EXPORT_FUNCTION(multiply, 0);
		int Multiply(const int& value);
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