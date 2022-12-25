#ifndef PCH_H
#define PCH_H

// 添加要在此处预编译的标头
#include "framework.h"

#ifndef EXPORT_CLASS
#define EXPORT_CLASS(type, prefix) // export cpp class as prefix of constructions, functions and so on
#endif

#ifndef EXPORT_CONSTRUCTOR
#define EXPORT_CONSTRUCTOR(name) // export cpp construction as #name
#endif

#ifndef EXPORT_FUNCTION
#define EXPORT_FUNCTION(name, default_return) // export cpp function as #name
#endif

#ifndef EXPORT_FUNCTION_POINTER
#define EXPORT_FUNCTION_POINTER
#endif

#ifndef EXPORT_STRUCT
#define EXPORT_STRUCT(name) // export cpp class as prefix of constructions, functions and so on
#endif

#ifndef EXPORT_ENUM
#define EXPORT_ENUM(prefix) // export cpp class as prefix of constructions, functions and so on
#endif

#ifndef EXPORT_ENUM_VALUE
#define EXPORT_ENUM_VALUE(name) // export cpp class as prefix of constructions, functions and so on
#endif

#endif //PCH_H
