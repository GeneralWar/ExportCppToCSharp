#include "pch.h"
#include "TestClass.hpp"

namespace TestNamespace
{
	TestClass::TestClass(const int& value) : mValue({ MININT, value }), mValueChangeCallback() { }

	TestClass::~TestClass() { }

	void TestClass::SetValueChangeCallback(ValueChange callback)
	{
		mValueChangeCallback = callback;
	}

	void TestClass::SetValue(const int& value)
	{
		mValue.previousValue = mValue.currentValue;
		mValue.currentValue = value;
		if (mValueChangeCallback)
		{
			mValueChangeCallback(this, value);
		}
	}

	void TestClass::SetValue(const unsigned int& value)
	{
		this->SetValue(static_cast<int>(value));
	}

	int TestClass::GetValue()
	{
		return mValue.currentValue;
	}
	
	const TestClassValue* TestClass::GetValuePointer() const
	{
		return &mValue;
	}

	int TestClass::Add(const int& value)
	{
		return mValue.currentValue + value;
	}

	int TestClass::Multiply(const int& value)
	{
		return mValue.currentValue * value;
	}

	TestStruct TestClass::ExportStruct() const
	{
		TestStruct value = { };
		value.value1 = 1;
		value.value2 = 2;
		value.value3 = 3;
		return value;
	}

	TestStruct* TestClass::ExportStructPointer() const
	{
		return nullptr;
	}

	DerivedTestClass::DerivedTestClass(const int& value) : TestClass(value) { }

	DerivedTestClass::~DerivedTestClass() { }

	int DerivedTestClass::Substract(const int& value) 
	{ 
		return this->GetValue() - value;
	}
}
