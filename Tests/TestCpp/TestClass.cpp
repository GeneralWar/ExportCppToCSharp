#include "pch.h"
#include "TestClass.hpp"

namespace TestNamespace
{
	TestClass::TestClass(const int& value) : mValue(value) { }

	TestClass::~TestClass() { }

	void TestClass::SetValue(const int& value)
	{
		mValue = value;
	}

	int TestClass::GetValue()
	{
		return mValue;
	}

	int TestClass::Add(const int& value)
	{
		return mValue + value;
	}

	int TestClass::Multiply(const int& value)
	{
		return mValue * value;
	}

	DerivedTestClass::DerivedTestClass(const int& value) : TestClass(value) { }

	DerivedTestClass::~DerivedTestClass() { }

	int DerivedTestClass::Substract(const int& value) 
	{ 
		return this->GetValue() - value;
	}
}
