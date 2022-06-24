using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

using STB;

using Trait = System.Collections.Generic.KeyValuePair<string,string>;

/*
    /// Mark a type as reflected, include additional info inside of the var-args as a list
    #define REFLECTED(...)

    /// Mark a global variable to be exposed
    #define REFLECT_GLOBAL(...)
*/

/// Can also tag for a BITFIELD_FLAGS source, an enum with the given name will be used for the bits of an numeric field.
// #define BITFIELD_FLAGS(NAME)

/// Public Properties are exposed by default
/*
    This can be used to define additional traits for the property:
        name = "Pretty Name To Print"
        tip = "Textual usage tip"
        depend = "FieldName"  (GUI hint: changing this field means having to refresh all fields with "depend")
        precise (GUI hint: should use 0.01 for steps, instead of 1.0 default)
        fine    (GUI hint: should use 0.1 for steps, instead of 1.0 default)
        get = __GetterMethodName__ (BINDING: getter must be TYPE FUNCTION() const)
        set = __SetterMethodName__ (BINDING: setter must be void FUNCTION(const TYPE&) )
        resource = __ResourceMember__ (BINDING: named property is the holder for resource data that matches this resource handle object)
            etc, it's the responsibility of generators to check for these and handle as appropriate
    #define PROPERTY(PROPERTY_INFO)
 */

/// Bind a method for GUI exposure
/*
    name = "Pretty Name To Print"
    tip = "Textual usage tip"
    editor (command will be exposed in the editor GUI, otherwise it is assumed to be for scripting only)
    cat = "Category Name" how this method should be clustered
    opcode = ## define as an indexed opcode operation
        etc. these are all arbitrary, it's the generators that deal with it
    
    #define METHOD_CMD(METHOD_INFO)
*/

/*
 What Works:
    Constructors, destructors, copy-constructors
    enums and enum classes
        including defined values ie 1 << 4
    classes, structs
    operator overloads
    global functions
    global fields

What is Limited:
    constexpr probably doesn't work in all cases

What does not Work:
    Templated class/struct definitions
        Can read existing templates, but not scan templated types correctly
        Consider a cheat like a special marking semantic?
        User REFLECT_FAKE to explicitly define?
    Move semantics
    Unions
    function pointers, std::function
        work-around: use typedef and define the type explicitly as an internal type before scanning

Fake handling to bind with an explicit definition:

    REFLECT_FAKE(name = "MyType")
        PROPERTY(name = "someVar_", type="std::string")
        VIRTUAL_PROPERTY(name = "Active", type="bool", get="IsActive", set="SetActive")
    END_FAKE
 */

namespace typegen
{
    /* ReflectionScanner myScanner = new ReflectionScanner();
     * myScanner.Sca(System.IO.File.ReadAllText("MyFile.h"));
     * myScanner.FinishScanning();
     * 
     * var filteredTypes = myScanner.database.FlatTypes.Where(o => !o.isInternal).ToList();
     * foreach (var myType in filteredTypes) {
     *      ... do work ...
     * }
     */
    public class ReflectionScanner
    {
        public CodeScanDB database = new CodeScanDB();
        public SortedSet<string> APIDeclarations = new SortedSet<string>();
        public SortedSet<string> CallingConventions = new SortedSet<string>();
        public List<string> ScannedHeaders = new List<string>();
        public List<string> ForwardLines = new List<string>();
        public bool IncludePrivateMembers { get; set; } = false;

        public ReflectionScanner()
        {
            APIDeclarations.Add("DLL_EXPORT");
            
            CallingConventions.Add("__cdecl");
            CallingConventions.Add("__stdcall");
            CallingConventions.Add("__fastcall");
            CallingConventions.Add("__thiscall");
            CallingConventions.Add("__vectorcall");

            // primitives
            database.AddInternalType("void", "VT_Invalid", 0, true, false);
            database.AddInternalType("bool", "VT_Bool", 0, true, false);
            database.AddInternalType("int", "VT_Int32", 0, true, false);
            database.AddInternalType("float", "VT_Float", 0, true, false);
            database.AddInternalType("unsigned", "VT_UInt32", 0, true, false);
            database.AddInternalType("uint32_t", "VT_UInt32", 0, true, false);
            database.AddInternalType("uint16_t", "VT_UShort", 0, true, false);
            database.AddInternalType("int16_t", "VT_Short", 0, true, false);
            database.AddInternalType("uint64_t", "VT_UInt64", 0, true, false);
            database.AddInternalType("int64_t", "VT_Int64", 0, true, false);
            database.AddInternalType("double", "VT_Double", 0, true, false);
            database.AddInternalType("std::string", "VT_String", 0, true, false);
            database.AddInternalType("size_t", "", 0, true, false);

            // URHO3D
            database.AddInternalType("IntVector2", "VT_IntVector2", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("IntVector3", "VT_IntVector3", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("IntRect", "VT_IntRect", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "left_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "top_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "right_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "bottom_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Rect", "VT_Rect", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Left()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Top()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Right()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Bottom()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Vector2", "VT_Vector2", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Vector3", "VT_Vector3", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Vector4", "VT_Vector4", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "w_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Quaternion", "VT_Quat", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "w_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("SharedPtr", "VT_SharedPtr", 0, false, true);
            database.AddInternalType("Vector", "", 0, false, true);
            database.AddInternalType("PODVector", "", 0, false, true);
            database.AddInternalType("HashMap", "", 0, false, true);
            database.AddInternalType("Variant", "", 0, true, false);
            database.AddInternalType("VariantVector", "VT_VariantVector", 0, true, false);
            database.AddInternalType("VariantMap", "VT_VariantMap", 0, true, false);
            database.AddInternalType("Color", "VT_Color", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "r_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "g_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "b_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "a_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("String", "VT_String", 0, true, false);
            database.AddInternalType("StringHash", "VT_StringHash", 0, true, false);

            // MathGeoLib
            database.AddInternalType("float2", "", 0, false, false);
            database.AddInternalType("float3", "", 0, false, false);
            database.AddInternalType("float4", "", 0, false, false);
            database.AddInternalType("rgba", "", 0, false, false);
            database.AddInternalType("Quat", "", 0, false, false);
            database.AddInternalType("float3x3", "", 0, false, false);
            database.AddInternalType("float3x4", "", 0, false, false);
            database.AddInternalType("float4x4", "", 0, false, false);

            // templates
            database.AddInternalType("std::vector", "", 0, false, true);
            database.AddInternalType("std::array", "", 0, false, true);
            database.AddInternalType("std::set", "", 0, false, true);
            database.AddInternalType("std::unordered_map", "", 0, false, true);
            database.AddInternalType("std::map", "", 0, false, true);
        }

        public void Scan(string code)
        {
            // change these guys so shit is all the same
            // todo: can size_t be safely turned into uint64_t?
            // replace compound items with standard single word types
            // normalize short/long
            code = Regex.Replace(code, @"\bunsigned char\b", "uint8_t");
            code = Regex.Replace(code, @"\bunsigned short\b", "uint16_t");
            code = Regex.Replace(code, @"\bunsigned int\b", "uint32_t");
            code = Regex.Replace(code, @"\bunsigned long\b", "uint64_t");
            code = Regex.Replace(code, @"\bshort\b", "int16_t");
            code = Regex.Replace(code, @"\blong\b", "int64_t");

            Lexer lexer = new Lexer(code);
            //string minVer = Minimalize(code.Replace("\r", "").Split('\n'));

            Stack<CodeScanDB.ReflectedType> typeStack = new Stack<CodeScanDB.ReflectedType>();
            while (AdvanceLexer(lexer) != 0)
            {
                if (lexer.token == Token.ID)
                {
                    if (lexer.string_value == "REFLECTED")
                    {
                        // look for a bound enum or struct
                        ProcessReflected(lexer, database, ref typeStack);
                    }
                    else if (lexer.string_value == "METHOD_CMD")
                    {
                        // global method to be bound, ie. commandlet
                        ReadMember(lexer, null, database, ref typeStack);
                    }
                    else if (lexer.string_value == "REFLECT_GLOBAL")
                    {
                        // Process a global variable, ie CVar like bindings
                        ReadMember(lexer, null, database, ref typeStack);
                    }
                    else if (lexer.string_value == "REFLECT_FAKE")
                    {
                        var traits = ReadTraits(lexer);
                        var t = new CodeScanDB.ReflectedType { typeName_ = traits.Get("name") };
                        database.types_.Add(t.typeName_, t);
                        for (;;)
                        { 
                            if (lexer.string_value == "PROPERTY")
                            {
                                var pTraits = ReadTraits(lexer); // eats the trailing )
                                CodeScanDB.Property p = new CodeScanDB.Property {  propertyName_ = pTraits.Get("name") };
                                p.bindingData_ = pTraits;
                                p.accessModifiers_ |= AccessModifiers.AM_Public;
                                
                                Lexer l = new Lexer(pTraits.Get("type").Unquote());
                                l.GetToken();
                                string fName = "";
                                p.type_ = GetTypeInformation(l, database, t, ref p.accessModifiers_, ref p.templateParameters_, out fName, out p.pointerLevel_);
                                t.properties_.Add(p);
                            }
                            else if (lexer.string_value == "VIRTUAL_PROPERTY")
                            {
                                var pTraits = ReadTraits(lexer); // eats the trailing )
                                CodeScanDB.Property p = new CodeScanDB.Property { propertyName_ = pTraits.Get("name") };
                                p.bindingData_ = pTraits;
                                p.accessModifiers_ |= AccessModifiers.AM_Public | AccessModifiers.AM_Virtual;

                                Lexer subLex = new Lexer(pTraits.Get("type").Unquote());
                                subLex.GetToken();
                                string dud = "";
                                p.type_ = GetTypeInformation(subLex, database, t, ref p.accessModifiers_, ref p.templateParameters_, out dud, out p.pointerLevel_);

                                t.properties_.Add(p);
                            }
                            else if (lexer.string_value == "END_FAKE")
                                break;
                            else // hit someting illegal
                            {
                                Console.WriteLine($"Illegal token in REFLECT_FAKE @ {lexer.line_number}:{lexer.line_start}");
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void ConcludeScanning()
        {
            database.ResolveIncompleteTypes();
        }

        void ProcessReflected(Lexer lexer, CodeScanDB database, ref Stack<CodeScanDB.ReflectedType> typeStack)
        {
            List<Trait> bindingInfo = new List<Trait>();

            while (AdvanceLexer(lexer) != 0)
            {
                if (lexer.token == Token.ID || lexer.token == Token.DQString)
                {
                    string key = lexer.TokenText;
                    string value = null;
                    if (lexer.Peek() == '=')
                    {
                        AdvanceLexer(lexer);
                        AdvanceLexer(lexer);
                        value = lexer.TokenText;
                    }

                    bindingInfo.Add(new Trait(key, value));
                }
                else if (lexer.token == ')')
                    break;
                else if (lexer.token == '(')
                    continue;
            }

            while (AdvanceLexer(lexer) != 0)
            {
                if (lexer.token == Token.ID)
                {
                    if (lexer.string_value == "struct")
                    {
                        var type = ProcessStruct(lexer, false, database, true, ref typeStack);
                        type.bindingData_.AddRange(bindingInfo);
                    }
                    else if (lexer.string_value == "class")
                    {
                        var type = ProcessStruct(lexer, true, database, false, ref typeStack);
                        type.bindingData_.AddRange(bindingInfo);
                    }
                    else if (lexer.string_value == "enum")
                    {
                        var type = ProcessEnum(lexer, database);
                        if (type != null)
                            database.types_.Add(type.typeName_, type);
                        type.bindingData_.AddRange(bindingInfo);
                    }
                }
                return;
            }
        }

        CodeScanDB.ReflectedType ProcessEnum(Lexer lexer, CodeScanDB db)
        {
            string typeName = "";

            bool isEnumClass = false;
            if (AdvanceLexer(lexer) != 0 && lexer.token == Token.ID)
            { 
                if (lexer.string_value == "class")
                { 
                    AdvanceLexer(lexer);
                    isEnumClass = true;
                }
                typeName = lexer.TokenText;
            }

            if (string.IsNullOrEmpty(typeName))
                return null;

            CodeScanDB.ReflectedType ret = new CodeScanDB.ReflectedType();
            ret.typeName_ = typeName;

            if (isEnumClass)
            {
                AdvanceLexer(lexer); // eat the ':'
                AdvanceLexer(lexer); // get the char,int,etc
                List<CodeScanDB.TemplateParam> dudTemps = new List<CodeScanDB.TemplateParam>();
                string dudName = ""; int dudPtrLevel = 0; AccessModifiers dudMods = 0;
                ret.enumType_ = GetTypeInformation(lexer, database, null, ref dudMods, ref dudTemps, out dudName, out dudPtrLevel);
            }

            while (lexer.token != '{' && AdvanceLexer(lexer) != 0)
                continue;

            int lastEnumValue = 0;
            string valueName = "";

            while (AdvanceLexer(lexer) != 0)
            {
                if (lexer.token == '}')
                    break;

                int value = int.MinValue;
                if (lexer.token == Token.ID && string.IsNullOrEmpty(valueName))
                { 
                    valueName = lexer.string_value;
                    continue;
                }
                else if (lexer.token == '=')
                {
                    while (AdvanceLexer(lexer) != 0)
                    {
                        if (lexer.token == '(')
                            continue;
                        else if (lexer.token == ')')
                            break;
                        else if (lexer.token == ',')
                            break;
                        else if (lexer.token == '}')
                            break;

                        if (lexer.token == Token.IntLit)
                            value = (int)lexer.int_number;
                        else if (lexer.token == Token.ID && lexer.string_value == "FLAG") // FLAG(32)
                        {
                            AdvanceLexer(lexer); //(
                            AdvanceLexer(lexer);
                            if (lexer.token == Token.IntLit)
                                value = 1 << (int)lexer.int_number;
                            else
                                value = database.GetPossibleLiteral(lexer.string_value);
                            AdvanceLexer(lexer); //)
                        }

                        // support 1 << 4
                        if (lexer.token == Token.ShiftLeft)
                        {
                            if (AdvanceLexer(lexer) != 0 && lexer.token == Token.IntLit)
                            {
                                value <<= (int)lexer.int_number;
                                break;
                            }
                        }
                    }
                }

                if (value != int.MinValue && !string.IsNullOrEmpty(valueName))
                {
                    lastEnumValue = value;
                    lastEnumValue += 1;
                    ret.enumValues_.Add(new KeyValuePair<string,int>(valueName, value));
                }
                else if (!string.IsNullOrEmpty(valueName))
                {
                    value = lastEnumValue;
                    lastEnumValue += 1;
                    ret.enumValues_.Add(new KeyValuePair<string, int>(valueName, value));
                }
                valueName = null;
            }

            if (!string.IsNullOrEmpty(valueName))
            {
                ret.enumValues_.Add(new KeyValuePair<string, int>(valueName, lastEnumValue));
            }

            // only return an enum if it contains actual values
            if (ret.enumValues_.Count > 0)
                return ret;

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isClass"></param>
        /// <param name="database"></param>
        /// <param name="defaultIsPublic"></param>
        /// <param name="typeStack"></param>
        /// <returns></returns>
        CodeScanDB.ReflectedType ProcessStruct(Lexer lexer, bool isClass, CodeScanDB database, bool defaultIsPublic, ref Stack<CodeScanDB.ReflectedType> typeStack)
        {
            bool inPublicScope = defaultIsPublic;

            CodeScanDB.ReflectedType type = new CodeScanDB.ReflectedType();
            type.isClass_ = isClass;

            if (AdvanceLexer(lexer) != 0 && lexer.token == Token.ID)
            {
                if (APIDeclarations.Contains(lexer.string_value))
                { 
                    type.apiDecl_ = lexer.string_value;
                    AdvanceLexer(lexer);
                }
                type.typeName_ = lexer.string_value;
            }

            database.types_.Add(type.typeName_, type);

            while (AdvanceLexer(lexer) != 0 && (lexer.token != ':' && lexer.token != '{'))
            {
                if (lexer.TokenText == "abtract")
                    type.isAbstract_ = true;
                if (lexer.TokenText == "final")
                    type.isFinal_ = true;
                continue;
            }

            if (lexer.token == ':')
            {
                do { 
                    AccessModifiers accessModifiers = 0;
                    string baseName = null;
                    if (ReadNameOrModifiers(lexer, ref accessModifiers, out baseName))
                    {
                        if (!string.IsNullOrEmpty(baseName))
                        {
                            var found = database.GetType(baseName);
                            if (found != null)
                                type.baseClass_.Add(found);
                            else
                            {
                                CodeScanDB.ReflectedType baseType = new CodeScanDB.ReflectedType();
                                type.baseClass_.Add(baseType);
                                baseType.typeName_ = baseName;
                                baseType.isComplete_ = false;
                            }
                        }
                    }
                } while (lexer.token == ',');
            }

            while (lexer.token != '}' && AdvanceLexer(lexer) != 0 && lexer.token != '}')
            {
                if (lexer.token == Token.ID && lexer.string_value == "public")
                {
                    AdvanceLexer(lexer);
                    AdvanceLexer(lexer); // eat the trailing :
                    inPublicScope = true;
                }
                else if (lexer.token == Token.ID && lexer.string_value == "private")
                {
                    AdvanceLexer(lexer);
                    AdvanceLexer(lexer); // eat the trailing :
                    inPublicScope = false;
                }
                else if (lexer.token == Token.ID && lexer.string_value == "REFLECTED")
                {
                    // nested
                    List<Trait> bindingTraits = ReadTraits(lexer);

                    if (lexer.token == Token.ID)
                    {
                        if (lexer.string_value == "struct")
                        {
                            var subType = ProcessStruct(lexer, false, database, true, ref typeStack);
                            type.subTypes_.Add(subType);
                            subType.containingType_ = type;
                        }
                        else if (lexer.string_value == "class")
                        {
                            var subType = ProcessStruct(lexer, true, database, false, ref typeStack);
                            type.subTypes_.Add(subType);
                            subType.containingType_ = type;
                        }
                        else if (lexer.string_value == "enum")
                        {
                            var subType = ProcessEnum(lexer, database);
                            type.subTypes_.Add(subType);
                            subType.containingType_ = type;
                            database.types_.Add(subType.typeName_, subType);
                        }
                    }
                }
                else if (lexer.token == Token.ID && lexer.string_value == "VIRTUAL_PROPERTY")
                {
                    List<Trait> bindingTraits = ReadTraits(lexer);

                    CodeScanDB.Property prop = new CodeScanDB.Property();
                    prop.bindingData_ = bindingTraits;
                    prop.propertyName_ = bindingTraits.Get("name");
                    prop.type_ = database.GetType(bindingTraits.Get("type").Unquote());
                    prop.accessModifiers_ |= AccessModifiers.AM_Virtual | AccessModifiers.AM_Virtual;
                    type.properties_.Add(prop);
                    continue;
                }
                
                // Read the field/method
                if (inPublicScope || IncludePrivateMembers)
                    ReadMember(lexer, type, database, ref typeStack);

                // this has historically been a hotspot for trouble with functions 
                //      "void MyFunc() { return 0; }"
                //          vs
                //      "void MyFunc();"
                //  Note where the semicolon differs
                while (lexer.token != ';' && lexer.token != Token.EOF)
                    AdvanceLexer(lexer);
            }

            return type;
        }

        void ReadMember(Lexer lexer, CodeScanDB.ReflectedType forType, CodeScanDB database, ref Stack<CodeScanDB.ReflectedType> typeStack)
        {
            CodeScanDB.ReflectedType bitNames = null;

            if (lexer.string_value == "NO_REFLECT")
            {
                while (AdvanceLexer(lexer) != 0 && lexer.token != ';' && lexer.token != '{');
                if (lexer.token == '{')
                    lexer.EatBlock('{', '}');
                return;
            }

            List<Trait> bindingTraits = new List<Trait>();
            bool allowMethodProcessing = false;
            if (lexer.string_value == "PROPERTY" || (allowMethodProcessing = lexer.string_value == "METHOD_CMD") || lexer.string_value == "REFLECT_GLOBAL")
            {
                allowMethodProcessing = true;
                bindingTraits = ReadTraits(lexer);
            }
            
            if (lexer.string_value == "BITFIELD_FLAGS")
            {
                AdvanceLexer(lexer); // eat the (
                if (AdvanceLexer(lexer) != 0 && lexer.token == Token.ID)
                {
                    var found = database.GetType(lexer.string_value);
                    if (found != null)
                        bitNames = found;
                }
                AdvanceLexer(lexer);
            }

            // We're now at the "int bob;" part
            /*

            cases to handle:
                int data;               // easy case
                int data = 3;           // default initialization ... initializer is grabbed as a string and placed literally in the generated code
                int* data;              // pointers
                int& data;              // references
                const int jim;          // const-ness
                shared_ptr<Roger> bob;  // templates

            special function cases to handle:
                void SimpleFunc();                      // No return and no arguments
                int SimpleFunc();                       // Has a return value
                int SimpleFunc() const;                 // Is a constant method
                void ArgFunc(int argumnet);             // Has an argument
                int ArgFunc(int argument = 1);          // Has an argument with default value
                int ArgFunc(const int* argumnet = 0x0); // Has a complex argument
            */

            string apiDecl = null;
            if (APIDeclarations.Contains(lexer.string_value))
            {
                apiDecl = lexer.string_value;
                AdvanceLexer(lexer);
            }

            AccessModifiers mods = 0;
            List<CodeScanDB.TemplateParam> templateParams = new List<CodeScanDB.TemplateParam>();
            string foundName = "";
            int optrLevel = 0;
            var foundType = GetTypeInformation(lexer, database, forType, ref mods, ref templateParams, out foundName, out optrLevel);
            if (foundType != null)
            {
                // if we're a ctor/dtor then we'll accept the found name as our name
                bool ctorDtor = mods.HasFlag(AccessModifiers.AM_Construct) || mods.HasFlag(AccessModifiers.AM_Destruct);
                string name = ctorDtor ? foundName : null;
                string callingConv = null;

                if (!ctorDtor)
                {
                    FETCH_NAME:
                    if (lexer.token == Token.ID)
                    {
                        name = lexer.string_value;
                        if (CallingConventions.Contains(name))
                        { 
                            callingConv = lexer.string_value;
                            AdvanceLexer(lexer);
                            goto FETCH_NAME;
                        }
                    }
                    
                    // this allows operator handling, by eating everything until (
                    // So operator+ operator[] etc all work.
                    if (name == "operator")
                    { 
                        while (lexer.Peek() != '(')
                        {
                            AdvanceLexer(lexer);
                            name += lexer.string_value;
                        }
                    }
                }

                lexer.SaveState();
                if (AdvanceLexer(lexer) == 0)
                    return;

                // FUNCTION OR METHOD
                if (lexer.token == '(')
                {
                    // they aren't automatically bound, requiring binding allows making some strictness that wouldn't fly in an autobinding situation
                    // not handling everything under the sun
                    if (allowMethodProcessing)
                    {
                        CodeScanDB.Method newMethod = new CodeScanDB.Method();
                        newMethod.declaringType_ = forType;
                        newMethod.callConv_ = callingConv;
                        newMethod.apiDecl_ = apiDecl;
                        newMethod.methodName_ = name;
                        newMethod.returnType_ = new CodeScanDB.Property { type_ = foundType, accessModifiers_ = mods & ~AccessModifiers.AM_Virtual };
                        newMethod.bindingData_ = bindingTraits;
                        newMethod.accessModifiers_ = mods & AccessModifiers.AM_Virtual; // only the virtual modifier is allowed at this point
                        if (forType != null)
                            forType.methods_.Add(newMethod);
                        else
                            database.globalFunctions_.Add(newMethod);

                        while (lexer.token != ')' && AdvanceLexer(lexer) != 0 && lexer.token != ')' && lexer.token != ';')
                        {
                            // get the argument
                            CodeScanDB.Property prop = new CodeScanDB.Property();
                            newMethod.argumentTypes_.Add(prop);
                            string foundTypeName = "";
                            // null on the forType specifically so we can handle copy constructors, otherwise it would fail
                            var functionArgType = GetTypeInformation(lexer, database, null, ref prop.accessModifiers_, ref prop.templateParameters_, out foundTypeName, out prop.pointerLevel_);
                            if (functionArgType != null)
                                prop.type_ = functionArgType;
                            else
                                prop.type_ = new CodeScanDB.ReflectedType { isComplete_ = false, typeName_ = foundTypeName };

                            if (lexer.token == '*')
                            {
                                prop.accessModifiers_ |= AccessModifiers.AM_Pointer;
                                prop.pointerLevel_ += 1;
                                AdvanceLexer(lexer);
                            }
                            else if (lexer.token == '&')
                            {
                                prop.accessModifiers_ |= AccessModifiers.AM_Reference;
                                AdvanceLexer(lexer);
                            }
                            
                            if (lexer.token == Token.ID)
                            {
                                newMethod.argumentNames_.Add(lexer.string_value);
                                AdvanceLexer(lexer);
                            }

                            while (lexer.token != ',' && lexer.token != ')')
                            {
                                if (lexer.token == '=') // scrape any default assignment verbatim
                                {
                                    AdvanceLexer(lexer);
                                    prop.defaultValue_ = lexer.ToStringUntil(new long[] { ',', ';', ')' });
                                    newMethod.defaultArguments_.Add(prop.defaultValue_);
                                }
                                else
                                    AdvanceLexer(lexer);
                            }

                            // make sure we're always the right size, if we've got nothing
                            while (newMethod.defaultArguments_.Count < newMethod.argumentTypes_.Count)
                                newMethod.defaultArguments_.Add(null);
                            while (newMethod.argumentNames_.Count < newMethod.argumentTypes_.Count)
                                newMethod.argumentNames_.Add(null);

                        }

                        if (lexer.PeekText() == "const")
                        {
                            newMethod.accessModifiers_ |= AccessModifiers.AM_Const;
                            AdvanceLexer(lexer);
                        }

                        if (lexer.PeekText() == "override")
                        {
                            newMethod.accessModifiers_ |= AccessModifiers.AM_Override;
                            AdvanceLexer(lexer);
                        }

                        if (lexer.PeekText() == "final")
                        {
                            newMethod.accessModifiers_ |= AccessModifiers.AM_Final;
                            AdvanceLexer(lexer);
                        }

                        if (lexer.PeekText() == "abstract")
                        {
                            newMethod.accessModifiers_ |= AccessModifiers.AM_Abstract;
                            AdvanceLexer(lexer);
                        }

                        // clean up constexpr, our return type isn't constexpr, we are
                        if (newMethod.returnType_ != null && newMethod.returnType_.accessModifiers_.HasFlag(AccessModifiers.AM_ConstExpr))
                        {
                            newMethod.accessModifiers_ |= AccessModifiers.AM_ConstExpr;
                            newMethod.returnType_.accessModifiers_ &= ~AccessModifiers.AM_ConstExpr;
                        }

                        if (lexer.Peek() == '{')
                        {
                            lexer.EatBlock('{', '}');
                            lexer.token = ';'; // "inject" the semi-colon
                        }
                    }
                    else
                    {
                        // not processing method, eat until we hit a semi-colon
                        lexer.RestoreState();
                        lexer.EatBlock('(', ')');
                        if (lexer.PeekText() == "const")
                            AdvanceLexer(lexer);
                        if (lexer.PeekText() == "abstract")
                            AdvanceLexer(lexer);
                        if (lexer.Peek() == '{')
                        {
                            lexer.EatBlock('{', '}');
                            lexer.token = ';';
                            return;
                        }
                        // code that calls this will expect lexer.toke == ';'
                    }
                }
                // ARRAY, you should be using std::array dumbass!
                else if (lexer.token == '[')
                {
                    CodeScanDB.Property property = new CodeScanDB.Property();
                    property.propertyName_ = name;
                    property.enumSource_ = bitNames;
                    property.type_ = foundType;
                    property.bindingData_ = bindingTraits;
                    property.pointerLevel_ = optrLevel;
                    if (forType != null)
                        forType.properties_.Add(property);
                    else
                        database.globalProperties_.Add(property);

                    if (AdvanceLexer(lexer) != 0)
                    {
                        if (lexer.token == Token.IntLit)
                            property.arraySize_ = (int)lexer.int_number;
                        else if (lexer.token == Token.ID)
                            property.arraySize_ = database.GetPossibleLiteral(lexer.string_value);
                    }

                    AdvanceLexer(lexer);
                    Debug.Assert(lexer.token == ']');
                }
                // VANILLA FIELD
                else
                {
                    CodeScanDB.Property property = new CodeScanDB.Property();
                    property.propertyName_ = name;
                    property.type_ = foundType;
                    property.enumSource_ = bitNames;
                    property.bindingData_ = bindingTraits;
                    property.templateParameters_ = templateParams;
                    property.accessModifiers_ = mods;
                    property.pointerLevel_ = optrLevel;
                    if (forType != null)
                        forType.properties_.Add(property);
                    else
                        database.globalProperties_.Add(property);

                    // extract default value
                    if (lexer.token == '=')
                    {
                        AdvanceLexer(lexer);
                        property.defaultValue_ = lexer.ToStringUntil(new long[] { ',', ';', ')' });
                    }

                    while (lexer.token != ';' && lexer.token != Token.EOF)
                        AdvanceLexer(lexer);
                }
            }
            else // unhandled case
            { 
                lexer.EatLine();
                lexer.token = ';';
                //if (lexer.token == '~')
                //    AdvanceLexer(lexer);
                //??Debug.Assert(false, "ReflectionScanner.ReadMember entered unhandled case");
                return;
            }
        }

        CodeScanDB.ReflectedType GetTypeInformation(Lexer lexer, CodeScanDB database, CodeScanDB.ReflectedType curType, ref AccessModifiers modifiers, ref List<CodeScanDB.TemplateParam> templateParams, out string foundName, out int pointerLevel)
        {
            CodeScanDB.ReflectedType foundType = null;

            pointerLevel = 0;
            foundName = "";
            string name = null;

            // we've got a fucking douchebag writing void MyFunc(class MyClass*, struct MyStruct*);
            if (lexer.token == Token.ID && (lexer.string_value == "class" || lexer.string_value == "struct"))
                AdvanceLexer(lexer);

            // virtual dtor
            if (lexer.token == Token.ID && lexer.string_value == "virtual")
            {
                modifiers |= AccessModifiers.AM_Virtual;
                AdvanceLexer(lexer);
            }

            // check for dtor
            if (lexer.token == '~')
            { 
                name += "~";
                modifiers |= AccessModifiers.AM_Destruct;
                AdvanceLexer(lexer);
            }

            while (lexer.token == Token.ID)
            {
                if (lexer.string_value == "static")
                {
                    modifiers |= AccessModifiers.AM_Static;
                    AdvanceLexer(lexer);
                    continue;
                }

                if (lexer.string_value == "virtual")
                {
                    modifiers |= AccessModifiers.AM_Virtual;
                    AdvanceLexer(lexer);
                    continue;
                }

                if (lexer.string_value == "transient")
                {
                    modifiers |= AccessModifiers.AM_Transient;
                    AdvanceLexer(lexer);
                    continue;
                }

                if (lexer.string_value == "const")
                {
                    modifiers |= AccessModifiers.AM_Const;
                    AdvanceLexer(lexer);
                    continue;
                }

                if (lexer.string_value == "constexpr")
                {
                    modifiers |= AccessModifiers.AM_ConstExpr;
                    AdvanceLexer(lexer);
                    continue;
                }

                if (lexer.string_value == "mutable")
                {
                    modifiers |= AccessModifiers.AM_Mutable;
                    AdvanceLexer(lexer);
                    continue;
                }

                if (lexer.string_value == "volatile")
                {
                    modifiers |= AccessModifiers.AM_Volatile;
                    AdvanceLexer(lexer);
                    continue;
                }

                // just eat the inline
                if (lexer.string_value == "inline")
                    AdvanceLexer(lexer);

                name += lexer.string_value;
                var found = database.GetType(name);
                
                // check for the ( so we still support 'MyType Add(const MyType&) const;' and family.
                if (found == curType && lexer.Peek() == '(') // if this is the case then we're a ctor/dtor/copy-tor
                { 
                    found = null;
                    if (!modifiers.HasFlag(AccessModifiers.AM_Destruct))
                        modifiers |= AccessModifiers.AM_Construct;
                    found = database.GetType("void");
                }

                foundName = name;
                if (found != null)
                    foundType = found;

                break;
            }

            AdvanceLexer(lexer);

            // deal with namespaces
            if (lexer.token == ':')
            {
                AdvanceLexer(lexer);
                if (lexer.token == ':')
                {
                    name += "::";
                    AdvanceLexer(lexer);
                    name += lexer.string_value;
                    AdvanceLexer(lexer);

                    if (foundType == null)
                    {
                        var found = database.GetType(name);
                        if (found != null)
                            foundType = found;
                    }
                }
            }

            if (lexer.token == '<')
            {
                AdvanceLexer(lexer);
                List<CodeScanDB.TemplateParam> templateTypes = new List<CodeScanDB.TemplateParam>();
                do
                {
                    List<CodeScanDB.TemplateParam> junk = new List<CodeScanDB.TemplateParam>();
                    AccessModifiers mods = 0;
                    if (lexer.token == Token.IntLit)
                    {
                        templateTypes.Add(new CodeScanDB.TemplateParam { IntegerValue = (int)lexer.int_number });
                    }
                    else if (lexer.token == Token.ID)
                    {   
                        string foundTemplateName = "";
                        int ptrLevel = 0;
                        CodeScanDB.ReflectedType templateType = GetTypeInformation(lexer, database, null, ref mods, ref junk, out foundTemplateName, out ptrLevel);
                        if (templateType != null)
                            templateTypes.Add(new CodeScanDB.TemplateParam { Type = new CodeScanDB.Property { type_ = templateType, accessModifiers_ = mods, templateParameters_ = junk, pointerLevel_ = ptrLevel } });
                        else
                            templateTypes.Add(new CodeScanDB.TemplateParam {  Type = new CodeScanDB.Property { type_ = new CodeScanDB.ReflectedType { isComplete_ = false, typeName_ = foundTemplateName }, accessModifiers_ = mods, templateParameters_ = junk, pointerLevel_ = ptrLevel } });
                    }
                    if (lexer.Peek() == ',')
                    { 
                        AdvanceLexer(lexer);
                        AdvanceLexer(lexer);
                    }
                    else if (lexer.token == ',')
                        AdvanceLexer(lexer);
                    else
                        AdvanceLexer(lexer);
                } while (lexer.token != '>' && lexer.token != Token.EOF);

                templateParams = templateTypes;
                AdvanceLexer(lexer);
            }

            // deal with assholes that write `Object const myThing;`
            if (lexer.token == Token.ID && lexer.string_value == "const")
            {
                modifiers |= AccessModifiers.AM_Const;
                AdvanceLexer(lexer);
            }

            while (lexer.token == '*')
            {
                modifiers |= AccessModifiers.AM_Pointer;
                pointerLevel += 1;
                AdvanceLexer(lexer);
            }

            // deal with `Object* const myThing;`
            if (lexer.token == Token.ID && lexer.string_value == "const")
            {
                modifiers |= AccessModifiers.AM_ConstPtr;
                AdvanceLexer(lexer);
            }

            if (lexer.token == '&')
            {
                modifiers |= AccessModifiers.AM_Reference;
                AdvanceLexer(lexer);
            }

            return foundType;
        }

        bool ReadNameOrModifiers(Lexer lexer, ref AccessModifiers modifiers, out string name)
        {
            bool ret = false;
            name = null;

            while (AdvanceLexer(lexer) == Token.ID)
            {
                if (lexer.string_value == "public")
                {
                    modifiers |= AccessModifiers.AM_Public;
                    ret = true;
                }
                else if (lexer.string_value == "private")
                {
                    modifiers |= AccessModifiers.AM_Private;
                    ret = true;
                }
                else if (lexer.string_value == "abstract")
                {
                    modifiers |= AccessModifiers.AM_Abstract;
                    ret = true;
                }
                else if (lexer.string_value == "const")
                {
                    modifiers |= AccessModifiers.AM_Const;
                    ret = true;
                }
                else if (lexer.string_value == "virtual")
                {
                    modifiers |= AccessModifiers.AM_Virtual;
                    ret = true;
                }
                else
                {
                    name = lexer.string_value;
                    ret = true;
                }
            }

            return ret;
        }

        int AdvanceLexer(Lexer lexer)
        {
            if (lexer.token == Token.EOF)
                return 0;
            if (lexer.parse_point >= lexer.eof)
                return 0;
            int code = lexer.GetToken();
            if (code != 0)
            {
                // ie. skip URHO3D_API
                //if (APIDeclarations.Contains(lexer.TokenText))
                //    return AdvanceLexer(lexer);
                //for (var lexCall in lexerCalls)
                //    lexCall(lexer, code);
                return code;
            }
            else
                return 0;
        }

        /// <summary>
        /// Takes the input lines and returns a single block of code with everything deeper than 2 levels removed.
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string Minimalize(string[] source)
        {
            DepthScanner scan = new DepthScanner();
            scan.Process(string.Join("\n", source));
            //+1 namespace
            //+1 type

            List<string> taken = new List<string>();
            for (int i = 0; i < source.Length; ++i)
            {
                if (scan.GetBraceDepth(i) <= 2)
                    taken.Add(source[i]);
            }
            return string.Join("\n", taken);
        }

        List<Trait> ReadTraits(Lexer lexer)
        {
            AdvanceLexer(lexer); // (
            List<Trait> bindingTraits = new List<Trait>();

            while (AdvanceLexer(lexer) != 0 && (lexer.token == Token.ID || lexer.token == Token.DQString))
            {
                string key = lexer.TokenText;
                string value = null;
                if (lexer.Peek() == '=')
                {
                    AdvanceLexer(lexer);
                    AdvanceLexer(lexer);
                    value = lexer.TokenText.Unquote();
                    if (lexer.Peek() == ':') // callback = Something::Something
                    {
                        value += "::";
                        AdvanceLexer(lexer); // :
                        AdvanceLexer(lexer); // :
                        AdvanceLexer(lexer);
                        value += lexer.TokenText.Unquote();
                    }
                }
                if (lexer.Peek() == ',')
                    AdvanceLexer(lexer);

                bindingTraits.Add(new Trait(key, value));
            }

            if (lexer.token == ')') // end of VIRTUAL_PROPERTY(...)
                AdvanceLexer(lexer);

            return bindingTraits;
        }
    }

    public static class BindingExt
    {
        public static bool HasTrait(this List<Trait> traits, string key)
        {
            return traits.Any(t => t.Key == key);
        }

        public static string Get(this List<Trait> bindingTraits, string key, string defaultVal = "")
        {
            for (int i = 0; i < bindingTraits.Count; ++i)
            {
                if (bindingTraits[i].Key == key)
                    return bindingTraits[i].Value;
            }
            return defaultVal;
        }

        public static bool GetBool(this List<Trait> traits, string key, bool defaultVal)
        {
            string v = traits.Get(key).ToLowerInvariant();
            if (!string.IsNullOrEmpty(v))
            {
                if (v == "true" || v == "on")
                    return true;
                else
                    return false;
            }
            return defaultVal;
        }

        public static float GetFloat(this List<Trait> traits, string key, float defaultVal)
        {
            string v = traits.Get(key);
            if (!string.IsNullOrEmpty(v))
            {
                float ret = 0.0f;
                if (float.TryParse(v, out ret))
                    return ret;
            }
            return defaultVal;
        }

        public static int GetInt(this List<Trait> traits, string key, int defaultVal)
        {
            string v = traits.Get(key);
            if (!string.IsNullOrEmpty(v))
            {
                int ret = 0;
                if (int.TryParse(v, out ret))
                    return ret;
            }
            return defaultVal;
        }

        // Double duty, finds repetitive values like:
        //      myVar="some value" myVar = "another Value" 
        // as well as:
        //      myVar = "some value; another value"
        public static List<string> GetList(this List<Trait> traits, string key)
        {
            List<string> ret = new List<string>();
            for (int i = 0; i < traits.Count; ++i)
            {
                if (traits[i].Key == key)
                {
                    var split = traits[i].Value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (split != null && split.Count > 0)
                        ret.AddRange(split.ConvertAll(s => s.Trim()));
                }
            }
            return ret;
        }

        // used so we can fill out something like PROPERTY(myData = "min = 0.1, max = 0.5") then pull a struct Range { float min; float max; } out of it.
        static void ReflectedFill(object obj, string txt)
        {
            string[] argList = txt.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < argList.Length; ++i)
            {
                string[] terms = argList[i].Split('=');
                if (terms.Length == 2)
                {
                    terms[0] = terms[0].Trim();
                    // ~ and ` replace for quotes when filling
                    terms[1] = terms[1].Trim().Replace("~", "\"").Replace("`", "\"");

                    var prop = obj.GetType().GetField(terms[0]);
                    if (prop != null)
                    {
                        if (prop.FieldType == typeof(bool))
                        {
                            bool b = false;
                            bool.TryParse(terms[1], out b);
                            prop.SetValue(obj, b);
                        }
                        else if (prop.FieldType == typeof(int))
                        {
                            int v = 0;
                            int.TryParse(terms[1], out v);
                            prop.SetValue(obj, v);
                        }
                        else if (prop.FieldType == typeof(float))
                        {
                            float v = 0;
                            float.TryParse(terms[1], out v);
                            prop.SetValue(obj, v);
                        }
                        else if (prop.FieldType == typeof(string))
                            prop.SetValue(obj, terms[1]);
                    }
                }
            }
        }

        /// <summary>
        /// Extract a struct MyStruct
        /// MyStruct = "myVar = False, myVar2 = 1.0, myString = 'Some text but don`t do this'"
        /// Slider = "min = 0.0, max = 1.0"
        /// </summary>
        public static T GetStruct<T>(this List<Trait> self, string traitName) where T : new()
        {
            T ret = new T();

            var found = self.FirstOrDefault(k => k.Key == traitName);
            if (found.Key == traitName)
                ReflectedFill(ret, found.Value);

            return ret;
        }

        public static T GetStruct<T>(this List<Trait> self) where T : new()
        {
            return GetStruct<T>(self, typeof(T).Name);
        }

        public static T GetClass<T>(this List<Trait> self, string traitName) where T : class, new()
        {
            var fnd = self.FirstOrDefault(k => k.Key == traitName);
            if (string.IsNullOrEmpty(fnd.Key))
                return null;

            T ret = new T();
            ReflectedFill(ret, fnd.Value);
            return ret;
        }

        public static T GetClass<T>(this List<Trait> self) where T : class, new()
        {
            return GetClass<T>(self, typeof(T).Name);
        }

        public static KeyValuePair<float, float> GetRange(this List<Trait> self, string traitName, KeyValuePair<float, float> defVal)
        {
            foreach (Trait t in self)
            {
                if (t.Key == traitName)
                {
                    var terms = t.Value.Split(':');
                    return new KeyValuePair<float, float>(float.Parse(terms[0]), float.Parse(terms[1]));
                }
            }
            return defVal;
        }

        public static string AngelscriptSignature(this CodeScanDB.Method method)
        {
            return $"{method.ReturnTypeText().Replace("*", "@+")} {method.methodName_}{method.CallSig().Replace("*", "@+")}";
        }
    }
}
