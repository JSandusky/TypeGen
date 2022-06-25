# TypeGen

Limited stream parser to reflect C++ code into for type inspection. It relies on reasonably minimal annotation of source code to work in order to constrain the subset of C++ that it has to manage to parse.

Var-args macros are used so arbitrary parameters can be provided

```cpp
// include these #defines somewhere so your compiler won't complain
#define REFLECTED(...)
#define REFLECT_GLOBAL(...)
#define BITFIELD_FLAGS(NAME)
#define PROPERTY(...)
#define VIRTUAL_PROPERTY(...)
#define METHOD_CMD(...)
#define REFLECT_FAKE(...)
#define END_FAKE

// variadic parameters are used for arbitrary parameters called `Traits`
//		PROPERTY(tip = "here's a tip", step = "0.1", bigstep = 0.2, networked /*key-only trait*/)
//	Traits are key-value or a key only flag
//  Keys must not contain spaces and must be valid C-style identifier names.
//	Values must be quoted if they are not a valid C-style identifier or type name
//	Lists can be specified with semi-colon ; delimiters as myList = "1;2;3;4;5"
//  	so that utility functions can extract a list
//  Structs can be extracted from Traits with the utility `GetStruct<T>()`
//		Fields must be specified as semicolon delimited 
//		`structData = "x = 1; y = 0.5f; z = 1.0f"` and reflection will be used
//		to fill in the struct T.

// use the macros

REFLECTED()
enum class MyEnum : u8 {
  VALA = 1,
  VALB = 1 << 2
};

REFLECTED()
struct MyStruct {
    PROPERTY()
    float fieldA;
    PROPERTY()
    std::string fieldB;
    PROPERTY()
    BITFIELD_FLAGS(MyEnum)
    unsigned flags_;
    
    METHOD_CMD(use = console, cheat = false)
    void PrintToConsole();
};

// Sometimes it may be too impractical (or undesirable) to scan or 
// markup a specific type or file so use REFLECT_FAKE to define a type.
// This is just like manual attribute registration reflection approaches,
// except  it's going into the CodeScanDB so it can be used for anything, 
// be it attribute registration, serialization, networking, GUI gen, etc.
REFLECT_FAKE(name = HardStruct)
    PROPERTY(name = "roger", type = "bool", tags = "tag1;tag2;tag3")
    VIRTUAL_PROPERTY(name = "moore", type = "float", get = "GetMoore", set = "SetMoore")
END_FAKE

```



## Intent

The project is meant to be a skeleton plus baseline examples. It isn't intended to fulfill any particular role out of the box beyond that.

It is fully expected that the end user will customize it for their needs, using only the base skeleton. So copy it and tweak as you need.

## Usage

`typegen --help` for instructions.

`-f FILE` input file, required unless using -c

`-c FILE` load config file, required unless using -f

`-gen GeneratorEnumValue` specifies generator to use, required

`-o FILE` output file, required

#### Config Format

for `typegen -c configfile.txt`

```
// Can comment anything

// lines can be blank
// list files to read to accumulate into scan database
Dev/Headers/MyHeader.h
Dev/Headers/MyOtherHeader.hpp
Dev/Headers/Graphics/Renderer.hpp
```

# Working with it

See [Program.cs](.\typegen\Program.cs) and [DatabaseGenerators.cs](.\typegen\DatabaseGenerators.cs) for example of using thing [ReflectionScanner](.\typegen\ReflectionScanner.cs) and [CodeScanDB](.\typegen\CodeScanDB.cs),

First step of usage is to prime the built-in types for the ReflectionScanner to use. These include primitives (such as int and uint32_t, etc), templates (the scanner can't interpret template type definitions, only instances), and any other types desired such as those you care about knowing the type, but have no need to reflect it.

To process you feed arbitrary code into `ReflectionScanner.Scan(string code)` and when you're done call `ReflectionScanner.ConcludeScanning()` to resolve incomplete type handles and construct relationships between types.

After that you can start working with the `ReflectionScanner.database` to do whatever is required.

## Dependencies (nuget)

- CommandLine
- System.Runtime.CompilerServices
- Microsoft.CodeAnalysis
- Microsoft.CSharp
- Costura.Fody
  - For keeping the DLL sanity

# To-Do

- std::function<> like  `std::function<int(float)>` etc
- `* const`  aside from just in functions (`void MyFunc(const Object* const param)`)
- function pointers `void (*MYFUNC)(int,float);`
- move semantics
- Partial macro expansion (named list of macros to expand) 
- Header preprocessor
- Eliminate false name collisions with contained type, non-trivial
- Remove unnecessary dependencies that are from old C#->C++ transpiler code.