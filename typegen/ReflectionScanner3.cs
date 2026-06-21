using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using STB;
using System.Diagnostics;

using Trait = System.Collections.Generic.KeyValuePair<string, string>;

namespace typegen
{
    /* The difference between this and ReflectionScanner is that this one operates
     * line-by-line instead of via lexing an entire source-file.
     * There are limitations, but it is intended to be more robust to code-in-the-wild
     * compared to lexing an entire file.
     * 
     * Parsing only begins when it hits a marked object and then descends into the struct
     * so long as depth == STRUCT_DEPTH + 1, if depth decreases then it's back to scanning
     * line by line for markers. This is also easier to custom-tailor to tasks such as adding
     * additional markers that do more stuff (ie -> an OPCODE(...) marker stuffing global-functions
     * into another list, etc instead requiring a more verbose marking such as METHOD_CMD(OPCODE).
     */
    public class ReflectionScanner3
    {
        DepthScanner depthScan = new DepthScanner();
        public CodeScanDB database = new CodeScanDB();
        public SortedSet<string> APIDeclarations = new SortedSet<string>();
        public List<string> ScannedHeaders = new List<string>();
        public List<string> ForwardLines = new List<string>();
        public bool IncludePrivateMembers { get; set; } = false;
        
        List<string> lines = new List<string>();
        int currentLine = 0;

        public string ProcLine { get
            {
                if (currentLine < lines.Count) return lines[currentLine];
                return "";
            } 
        }

        public ReflectionScanner3()
        {
            APIDeclarations.Add("DLL_EXPORT");

            // primitives
            database.AddInternalType("void", "void", 0, true, false);
            database.AddInternalType("bool", "bool", 0, true, false);
            database.AddInternalType("int", "int", 0, true, false);
            database.AddInternalType("float", "float", 0, true, false);
            database.AddInternalType("unsigned", "unsigned", 0, true, false);
            database.AddInternalType("uint32_t", "unsigned", 0, true, false);
            database.AddInternalType("double", "double", 0, true, false);
            database.AddInternalType("std::string", "string", 0, true, false);

            // URHO3D
            database.AddInternalType("IntVector2", "IntVector2", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("IntVector3", "IntVector3", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("IntRect", "IntRect", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "left_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "top_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "right_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "bottom_", type_ = database.GetType("int"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Rect", "Rect", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Left()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Top()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Right()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "Bottom()", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Vector2", "Vector2", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Vector3", "Vector3", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Vector4", "Vector4", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "w_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("Quaternion", "Quaternion", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "x_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "y_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "z_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "w_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("SharedPtr", "SharedPtr", 0, false, true);
            database.AddInternalType("Vector", "Vector", 0, false, true);
            database.AddInternalType("PODVector", "PODVector", 0, false, true);
            database.AddInternalType("HashMap", "HashMap", 0, false, true);
            database.AddInternalType("Variant", "Variant", 0, true, false);
            database.AddInternalType("VariantVector", "VariantVector", 0, true, false);
            database.AddInternalType("VariantMap", "VariantMap", 0, true, false);
            database.AddInternalType("Color", "Color", 0, true, false)
                .AddProperty(new CodeScanDB.Property { propertyName_ = "r_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "g_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "b_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public })
                .AddProperty(new CodeScanDB.Property { propertyName_ = "a_", type_ = database.GetType("float"), accessModifiers_ = AccessModifiers.AM_Public }).isVector_ = true;
            database.AddInternalType("String", "String", 0, true, false);
            database.AddInternalType("StringHash", "StringHash", 0, true, false);

            // MathGeoLib
            database.AddInternalType("float2", "float2", 0, false, false);
            database.AddInternalType("float3", "float3", 0, false, false);
            database.AddInternalType("float4", "float4", 0, false, false);
            database.AddInternalType("rgba", "rgba", 0, false, false);
            database.AddInternalType("Quat", "Quat", 0, false, false);
            database.AddInternalType("float3x3", "float3x3", 0, false, false);
            database.AddInternalType("float3x4", "float3x4", 0, false, false);
            database.AddInternalType("float4x4", "float4x4", 0, false, false);

            // templates
            database.AddInternalType("std::vector", "", 0, false, true);
            database.AddInternalType("std::array", "", 0, false, true);
            database.AddInternalType("std::set", "", 0, false, true);
            database.AddInternalType("std::unordered_map", "", 0, false, true);
            database.AddInternalType("std::map", "", 0, false, true);
        }

        public void ConcludeScanning()
        {
            database.ResolveIncompleteTypes();
        }

        public void Scan(string code)
        {
            code = code.Replace("unsigned char", "uint8_t");
            code = code.Replace("unsigned short", "uint16_t");
            code = code.Replace("unsigned int", "uint32_t");
            code = code.Replace("unsigned long", "uint64_t");
            code = code.Replace("unsigned long long", "uint64_t");
            code = code.Replace("short", "int16_t");
            code = code.Replace("long", "int64_t");

            depthScan.Process(code);

            lines = code.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            currentLine = 0;

            List<CodeScanDB.ReflectedType> typeStack = new List<CodeScanDB.ReflectedType>();

            while (currentLine < lines.Count)
            {
                if (lines[currentLine].StartsWith("REFLECTED"))
                {
                    var traits = ReadTraits(lines[currentLine]);
                    // type
                    if (lines[currentLine+1].Contains("struct") || lines[currentLine+1].Contains("class"))
                    {
                        ++currentLine;
                        ProcessReflected(lines[currentLine], database, traits, ref typeStack);
                    }
                    else // global property
                    {
                        ++currentLine;
                        ReadMember(lines[currentLine], null, database, traits, null, ref typeStack);
                    }
                }
                else if (lines[currentLine].StartsWith("METHOD_CMD"))
                {
                    var traits = ReadTraits(lines[currentLine]);
                    ++currentLine;
                    ReadMember(lines[currentLine], null, database, traits, null, ref typeStack);
                }
                ++currentLine;
            }
        }

        void ProcessReflected(string line, CodeScanDB database, List<Trait> bindingInfo, ref List<CodeScanDB.ReflectedType> typeStack)
        {
            int depth = depthScan.GetBraceDepth(currentLine);
            Lexer lexer = new Lexer(line);
            while (AdvanceLexer(lexer) != 0)
            {
                if (lexer.token == Token.ID)
                {
                    if (lexer.string_value == "struct")
                    {
                        var type = ProcessStruct(lexer, false, database, true, ref typeStack);
                        if (type != null)
                            database.types_.Add(type.typeName_, type);
                        type.bindingData_.AddRange(bindingInfo);
                    }
                    else if (lexer.string_value == "class")
                    {
                        var type = ProcessStruct(lexer, true, database, false, ref typeStack);
                        if (type != null)
                            database.types_.Add(type.typeName_, type);
                        type.bindingData_.AddRange(bindingInfo);
                    }
                    else if (lexer.string_value == "enum")
                    {
                        var type = ProcessEnum(lexer);
                        if (type != null)
                            database.types_.Add(type.typeName_, type);
                        type.bindingData_.AddRange(bindingInfo);
                    }
                }
                return;
            }
        }

        CodeScanDB.ReflectedType ProcessStruct(Lexer lexer, bool isClass, CodeScanDB database, bool defaultIsPublic, ref List<CodeScanDB.ReflectedType> typeStack)
        {
            bool inPublicScope = defaultIsPublic;
            int depth = depthScan.GetBraceDepth(currentLine);

            CodeScanDB.ReflectedType type = new CodeScanDB.ReflectedType();
            if (AdvanceLexer(lexer) != 0 && lexer.token == Token.ID)
                type.typeName_ = lexer.string_value;

            while (AdvanceLexer(lexer) != 0 && (lexer.token != ':' && lexer.token != '{'))
            {
                if (lexer.TokenText == "abstract")
                    type.isAbstract_ = true;
                if (lexer.TokenText == "final")
                    type.isFinal_ = true;
                continue;
            }

            if (lexer.token == ':')
            {
                AccessModifiers accessModifiers = 0;
                string baseName = null;

                while (lexer.PeekText() == "public" || lexer.PeekText() == "private" || lexer.PeekText() == "protected")
                    AdvanceLexer(lexer);

                List<CodeScanDB.TemplateParam> templateParams = new List<CodeScanDB.TemplateParam>();
                var foundBaseClass = GetTypeInformation(lexer, database, ref accessModifiers, ref templateParams, out baseName);
                if (foundBaseClass != null)
                {
                    type.baseClass_.Add(foundBaseClass);
                }
                else
                {
                    CodeScanDB.ReflectedType baseType = new CodeScanDB.ReflectedType();
                    type.baseClass_.Add(baseType);
                    baseType.typeName_ = baseName;
                    baseType.isComplete_ = false;
                }
            }

            ++currentLine;
            while (depthScan.GetBraceDepth(currentLine) > depth)
            {
                if (depthScan.GetBraceDepth(currentLine) == depth + 1) // are we in the body
                {
                    if (lines[currentLine].StartsWith("public:"))
                    {
                        inPublicScope = true;
                    }
                    else if (lines[currentLine].StartsWith("private:"))
                    {
                        inPublicScope = false;
                    }

                    if (inPublicScope || IncludePrivateMembers)
                    {
                        if (lines[currentLine].Trim().StartsWith("NO_REFLECT"))
                        { 
                            currentLine += 2; // skip us and the next line
                            continue;
                        }
                        else
                        {
                            if (lines[currentLine].StartsWith("//"))
                            {
                                currentLine += 1;
                                continue;
                            }

                            List<Trait> bindingInfo = new List<Trait>();
                            string bitFieldName = "";
                            if (lines[currentLine].Trim().StartsWith("BITFIELD_FLAGS"))
                            {
                                int startIdx = ProcLine.IndexOf('(') + 1;
                                int endIdx = ProcLine.IndexOf(')');
                                bitFieldName = ProcLine.Substring(startIdx + 1, endIdx - startIdx);
                                ++currentLine;
                            }

                            if (lines[currentLine].Trim().StartsWith("PROPERTY"))
                            {
                                bindingInfo = ReadTraits(lines[currentLine]);
                                ++currentLine;

                                if (lines[currentLine].Trim().StartsWith("BITFIELD_FLAGS"))
                                {
                                    int startIdx = ProcLine.IndexOf('(') + 1;
                                    int endIdx = ProcLine.IndexOf(')');
                                    bitFieldName = ProcLine.Substring(startIdx + 1, endIdx - startIdx);
                                    ++currentLine;
                                }

                                ReadMember(lines[currentLine].Trim(), type, database, bindingInfo, bitFieldName, ref typeStack);
                            }
                            else
                            {
                                ReadMember(lines[currentLine].Trim(), type, database, bindingInfo, bitFieldName, ref typeStack);
                            }
                        }
                    }
                }
                ++currentLine;
            }

            return type;
        }

        CodeScanDB.ReflectedType ProcessEnum(Lexer lexer)
        {
            string typeName = "";

            if (AdvanceLexer(lexer) != 0 && lexer.token == Token.ID)
                typeName = lexer.TokenText;

            if (string.IsNullOrEmpty(typeName))
                return null;

            CodeScanDB.ReflectedType ret = new CodeScanDB.ReflectedType();
            ret.typeName_ = typeName;

            while (AdvanceLexer(lexer) != 0 && lexer.token != '{')
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
                    ret.enumValues_.Add(new KeyValuePair<string, int>(valueName, value));
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

        void ReadMember(string codeLine, CodeScanDB.ReflectedType forType, CodeScanDB database, List<Trait> bindingInfo, string bitClassName, ref List<CodeScanDB.ReflectedType> typeStack)
        {
            Lexer lexer = new Lexer(codeLine);
            lexer.GetToken();
            CodeScanDB.ReflectedType bitNames = null;

            if (lexer.string_value == "NO_REFLECT")
            {
                while (AdvanceLexer(lexer) != 0 && lexer.token != ';' && lexer.token != '{') ;
                if (lexer.token == '{')
                    lexer.EatBlock('{', '}');
                return;
            }

            bool allowMethodProcessing = true;
            if (!string.IsNullOrEmpty(bitClassName))
            {
                var found = database.GetType(bitClassName);
                if (found != null)
                    bitNames = found;
            }

            // We're now at the "int bob;" part
            /*

            cases to handle:
                int data;               // easy case
                int data = 3;           // default initialization ... initializer is grabbed as a string and placed literally in the generated code
                int* data;              // pointers
                int** data;             // pointer pointers
                int& data;              // references
                Vore::Type data;        // scoped type
                const int jim;          // const-ness
                mutable int jim;        // mutable
                thread_local int jim;   // thread_local is always ignored, assumed to be ephemeral thread state
                shared_ptr<Roger> bob;  // templates

            special function cases to handle:
                void SimpleFunc();                      // No return and no arguments
                int SimpleFunc();                       // Has a return value
                int SimpleFunc() const;                 // Is a constant method
                void ArgFunc(int argumnet);             // Has an argument
                int ArgFunc(int argumnet = 1);          // Has an argument with default value
                int ArgFunc(const int** argument = 0x0); // Has a complex argument
            */

            AccessModifiers mods = 0;
            List<CodeScanDB.TemplateParam> templateParams = new List<CodeScanDB.TemplateParam>();
            string foundName = "";
            var foundType = GetTypeInformation(lexer, database, ref mods, ref templateParams, out foundName);
            if (foundType != null)
            {
                string name = null;
                // if (AdvanceLexer(lexer)) // already there because of GetTypeInformation call
                {
                    if (lexer.token == Token.ID)
                        name = lexer.string_value;
                }

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
                        newMethod.methodName_ = name;
                        newMethod.returnType_ = new CodeScanDB.Property { type_ = foundType, accessModifiers_ = mods & ~AccessModifiers.AM_Virtual };
                        newMethod.bindingData_ = bindingInfo;
                        newMethod.accessModifiers_ = mods & AccessModifiers.AM_Virtual; // only the virtual modifier is allowed at this point
                        if (forType != null)
                            forType.methods_.Add(newMethod);
                        else
                            database.globalFunctions_.Add(newMethod);

                        while (AdvanceLexer(lexer) != 0 && lexer.token != ')' && lexer.token != ';')
                        {
                            // get the argument
                            CodeScanDB.Property prop = new CodeScanDB.Property();
                            newMethod.argumentTypes_.Add(prop);
                            string foundTypeName = "";
                            var functionArgType = GetTypeInformation(lexer, database, ref prop.accessModifiers_, ref prop.templateParameters_, out foundTypeName);
                            if (functionArgType != null)
                                prop.type_ = functionArgType;
                            else
                                prop.type_ = new CodeScanDB.ReflectedType { isComplete_ = false, typeName_ = foundTypeName };

                            if (lexer.token == '*')
                            {
                                prop.accessModifiers_ |= AccessModifiers.AM_Pointer;
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

                            // extract default parameters
                            while (lexer.token != ',' && lexer.token != ')')
                            {
                                if (lexer.token == '=')
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

                        if (lexer.Peek() == '{')
                        {
                            lexer.EatBlock('{', '}');
                            lexer.token = ';'; // "inject" the semi-colon
                        }
                    }
                    else
                    {
                        // not processing method, eat until we hit a semi-colon
                        while (AdvanceLexer(lexer) != ')') ;
                        while (lexer.PeekText() == "const" || lexer.PeekText() == "override")
                            AdvanceLexer(lexer);
                        if (lexer.PeekText() == "abstract")
                            AdvanceLexer(lexer);
                        if (lexer.Peek() == '{')
                        {
                            lexer.EatBlock('{', '}');
                            lexer.token = ';';
                            return;
                        }
                    }
                }
                // ARRAY, you should be using std::array dumbass!
                else if (lexer.token == '[')
                {
                    CodeScanDB.Property property = new CodeScanDB.Property();
                    property.propertyName_ = name;
                    property.enumSource_ = bitNames;
                    property.type_ = foundType;
                    property.bindingData_ = bindingInfo;
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
                    property.bindingData_ = bindingInfo;
                    property.templateParameters_ = templateParams;
                    property.accessModifiers_ = mods;
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

        CodeScanDB.ReflectedType GetTypeInformation(Lexer lexer, CodeScanDB database, ref AccessModifiers modifiers, ref List<CodeScanDB.TemplateParam> templateParams, out string foundName)
        {
            CodeScanDB.ReflectedType foundType = null;

            foundName = "";
            string name = null;
            if (lexer.token == Token.ID)
            {
                if (lexer.string_value == "static")
                {
                    modifiers |= AccessModifiers.AM_Static;
                    AdvanceLexer(lexer);
                }

                if (lexer.string_value == "virtual")
                {
                    modifiers |= AccessModifiers.AM_Virtual;
                    AdvanceLexer(lexer);
                }

                if (lexer.string_value == "transient")
                {
                    modifiers |= AccessModifiers.AM_Transient;
                    AdvanceLexer(lexer);
                }

                if (lexer.string_value == "const")
                {
                    modifiers |= AccessModifiers.AM_Const;
                    AdvanceLexer(lexer);
                }

                if (lexer.string_value == "mutable")
                {
                    modifiers |= AccessModifiers.AM_Mutable;
                    AdvanceLexer(lexer);
                }

                if (lexer.string_value == "volatile")
                {
                    modifiers |= AccessModifiers.AM_Volatile;
                    AdvanceLexer(lexer);
                }

                // just eat the inline
                if (lexer.string_value == "inline")
                    AdvanceLexer(lexer);

                name = lexer.string_value;
                var found = database.GetType(name);
                foundName = name;
                if (found != null)
                    foundType = found;
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
                bool closedByShiftRight = false;
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
                        CodeScanDB.ReflectedType templateType = GetTypeInformation(lexer, database, ref mods, ref junk, out foundTemplateName);
                        if (templateType != null)
                            templateTypes.Add(new CodeScanDB.TemplateParam { Type = new CodeScanDB.Property { type_ = templateType, accessModifiers_ = mods, templateParameters_ = junk } });
                        else
                            templateTypes.Add(new CodeScanDB.TemplateParam { Type = new CodeScanDB.Property { type_ = new CodeScanDB.ReflectedType { isComplete_ = false, typeName_ = foundTemplateName }, accessModifiers_ = mods, templateParameters_ = junk } });
                    }

                    if (lexer.token == Token.ShiftRight)
                    {
                        lexer.token = '>';
                        lexer.StashToken('>');
                        closedByShiftRight = true;
                        break;
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
                if (!closedByShiftRight)
                    AdvanceLexer(lexer);
            }

            if (lexer.token == '*')
            {
                modifiers |= AccessModifiers.AM_Pointer;
                AdvanceLexer(lexer);
            }

            if (lexer.token == '&')
            {
                modifiers |= AccessModifiers.AM_Reference;
                AdvanceLexer(lexer);
            }

            return foundType;
        }

        List<Trait> ReadTraits(string line)
        {
            List<Trait> traits = new List<KeyValuePair<string, string>>();

            Lexer lexer = new Lexer(line);
            lexer.GetToken(); // eat the CLEX_id
            lexer.GetToken(); // eat the (

            while (lexer.GetToken() != 0 && (lexer.token == Token.ID || lexer.token == Token.DQString))
            {
                string key = lexer.TokenText;
                string value = null;
                if (lexer.Peek() == '=')
                {
                    lexer.GetToken();
                    lexer.GetToken();
                    value = lexer.TokenText;
                    if (lexer.Peek() == ':') // callback = Something::Something
                    {
                        value += "::";
                        lexer.GetToken(); // :
                        lexer.GetToken(); // :
                        lexer.GetToken(); // text
                        value += lexer.TokenText;
                    }
                }
                if (lexer.Peek() == ',')
                    lexer.GetToken();

                traits.Add(new Trait(key, value));
            }

            return traits;
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
                if (APIDeclarations.Contains(lexer.TokenText))
                    return AdvanceLexer(lexer);
                //for (var lexCall in lexerCalls)
                //    lexCall(lexer, code);
                return code;
            }
            else
                return 0;
        }

        // Lexer knows about ignoring comments in code we lex, this is for line-by-line
        void TestComment()
        {
            bool anyHit = false;
            do
            {
                anyHit = false;
                while (lines[currentLine].StartsWith("//"))
                {
                    ++currentLine;
                    anyHit = true;
                }

                // eat block comment
                if (lines[currentLine].StartsWith("/*"))
                {
                    do { 
                        ++currentLine;
                    } while (!lines[currentLine].EndsWith("*/"));
                }
            } while (anyHit == true);
        }

        bool SatisfiesAsType(string line)
        {
            // we don't understand typedefs ... this means we're not parsing C code doing typedef struct { } myStruct;
            if (line.Contains("typedef"))
                return false;

            // incomplete type or not? class MyClass;
            if ((line.Contains("class") || line.Contains("struct")) && !line.EndsWith(";"))
                return true;

            if (line.Contains("enum"))
                return true;

            return false;
        }
    }
}
