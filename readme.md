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

// Sometimes it may be too be impractical (or undesirable) to scan 
// or markup a specific type so use REFLECT_FAKE to note what to use
// this is akin to a manual attribute registration reflection, except
// it's going into the CodeScanDB so it can be used for anything, be it
// attribute registration, serialization, networking, GUI gen, etc.
REFLECT_FAKE(name = HardStruct)
    PROPERTY(name = "roger", type = "bool")
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
- Eliminate false name collisions