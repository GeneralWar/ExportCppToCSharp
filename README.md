# ExportCppToCSharp

## Test Files

C++ header file: [TestClass.hpp](Tests/TestCpp/TestClass.hpp)  
C++ export file: [Exports.cpp](Tests/TestCpp/Exports.cpp)  
Xml export file: [Bindings.xml](Tests/TestCSharp/Bindings.xml)  
C# binding file: [Tests/TestCSharp/Bindings.cs](Tests/TestCSharp/Bindings.cs)  

## Test Project 

Exporter [ExportCpp.csproj](Sources/ExportCpp/)  
Binding Test [TestCSharp.csproj](Tests/TestCSharp/)  

## Preview

### C++ Header

```
EXPORT_CLASS(TestClass, test_class)
class TestClass
{
public:
    EXPORT_FUNCTION_POINTER
    typedef void (*ValueChange)(TestClass* instance, int value);
private:
    int mValue;
public:
    EXPORT_CONSTRUCTOR(create_test_class);
    TestClass(const int& value);
    virtual ~TestClass();

    EXPORT_FUNCTION(set_value_change_callback, 0);
    void SetValueChangeCallback(ValueChange callback);

    EXPORT_FUNCTION(set_value, 0);
    void SetValue(const int& value);

    EXPORT_FUNCTION(get_value, 0);
    int GetValue();

    EXPORT_FUNCTION(add, 0);
    int Add(const int& value);
    EXPORT_FUNCTION(multiply, 0);
    int Multiply(const int& value);
};
```

### C++ Exports

```
__declspec (dllexport) TestNamespace::TestClass* create_test_class(int value)
{
    return new TestNamespace::TestClass(value);
}

__declspec (dllexport) void test_class_set_value_change_callback(TestNamespace::TestClass* instance, TestNamespace::TestClass::ValueChange callback)
{
    if (!instance) return;
    instance->SetValueChangeCallback(callback);
}

__declspec (dllexport) void test_class_set_value(TestNamespace::TestClass* instance, int value)
{
    if (!instance) return;
    instance->SetValue(value);
}

__declspec (dllexport) int test_class_get_value(TestNamespace::TestClass* instance)
{
    if (!instance) return 0;
    return instance->GetValue();
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
```

### C# Bindings

```
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
static internal extern System.Int32 test_class_substract(IntPtr instance, System.Int32 value);
```

## Test Environment

```
>clang -v
clang version 15.0.0
Target: x86_64-pc-windows-msvc
Thread model: posix
```

## Caution

- It only works on clang version 15.x
- You should copy libClangSharp.dll to output directory manually

## Suggestions welcome
