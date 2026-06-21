using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class MonoGenerator
    {
        public void WriteMonoBindings(StringBuilder cpp, StringBuilder cs, CodeScanDB database, string namespaceBind, string outputDir, string globalBindingsClassName)
        {
            var voidType = database.GetType("void");

            cpp.AppendLine("#include <mono/jit/jit.h>");
            cpp.AppendLine("#include <mono/metadata/assembly.h>");
            cpp.AppendLine("#include <mono/metadata/reflection.h>");
            cpp.AppendLine("#include <mono/metadata/object.h>");
            cpp.AppendLine("#include <mono/metadata/class.h>");
            cpp.AppendLine("#include <mono/metadata/appdomain.h>");
            cpp.AppendLine("#include <string>");
            cpp.AppendLine("#include \"MonoCallable.h\"");
            cpp.AppendLine("");
            cpp.AppendLine("void BindMono() {");

            cs.AppendLine("using System;");
            cs.AppendLine("using System.Collections.Generic;");
            cs.AppendLine("using System.Linq;");
            cs.AppendLine("using System.Text;");
            cs.AppendLine("using System.Runtime.CompilerServices;");
            cs.AppendLine("");
            cs.AppendLine($"namespace {namespaceBind} {{");

            foreach (var t in database.types_)
            {
                if (t.Value.IsEnum || t.Value.isInternal_ || t.Value.isPrimitive_ || t.Value.containingType_ != null)
                    continue;

                string baseName = null;
                foreach (var b in t.Value.baseClass_)
                {
                    if (b.isInternal_ || b.IsEnum || b.isPrimitive_ || !b.isClass_)
                        continue;
                    baseName = b.typeName_;
                    break;
                }

                cpp.AppendLine("");
                cs.AppendLine("");
                cs.AppendLine($"    public static partial class {t.Value.typeName_}_Bindings {{");
                StringBuilder implSb = new StringBuilder();
                implSb.AppendLine("using System;");
                implSb.AppendLine("using System.Collections.Generic;");
                implSb.AppendLine("using System.Linq;");
                implSb.AppendLine("using System.Text;");
                implSb.AppendLine("using System.Runtime.CompilerServices;");
                implSb.AppendLine("");
                implSb.AppendLine($"namespace {namespaceBind} {{");
                implSb.AppendLine("");

                implSb.AppendLine("");
                if (baseName != null)
                    implSb.AppendLine($"    public partial class {t.Value.typeName_} : {baseName} {{");
                else
                    implSb.AppendLine($"    public partial class {t.Value.typeName_} {{");

                if (baseName == null)
                {
                    implSb.AppendLine("        public IntPtr NativePtr { get; internal set; }");
                    implSb.AppendLine("        public bool IsNull { get { return NativePtr == IntPtr.Zero; } }");
                    implSb.AppendLine("        public bool IsValid { get { return NativePtr != IntPtr.Zero; } }");
                }

                if (baseName != null)
                    implSb.AppendLine($"        public {t.Value.typeName_}(IntPtr ptr) : base(ptr) {{ }}");
                else
                    implSb.AppendLine($"        public {t.Value.typeName_}(IntPtr ptr) {{ NativePtr = ptr; }}");

                EmitTypeMembers(t.Value, cpp, cs, implSb, database, namespaceBind, "        ", voidType);

                // Emit nested types (one level only)
                foreach (var sub in t.Value.subTypes_)
                {
                    if (sub.IsEnum || sub.isInternal_ || sub.isPrimitive_)
                        continue;

                    if (sub.subTypes_.Count > 0)
                    {
                        Console.WriteLine($"Warning: Nested type '{sub.typeName_}' has its own nested types which exceed the one-level nesting limit and will be omitted");
                        implSb.AppendLine($"        // Warning: {sub.typeName_} has nested types which were omitted (exceeded depth limit)");
                    }

                    cs.AppendLine("");
                    cs.AppendLine($"    public static partial class {sub.typeName_}_Bindings {{");

                    implSb.AppendLine($"        public partial class {sub.typeName_} {{");
                    implSb.AppendLine("            public IntPtr NativePtr { get; internal set; }");
                    implSb.AppendLine("            public bool IsNull { get { return NativePtr == IntPtr.Zero; } }");
                    implSb.AppendLine("            public bool IsValid { get { return NativePtr != IntPtr.Zero; } }");
                    implSb.AppendLine($"            public {sub.typeName_}(IntPtr ptr) {{ NativePtr = ptr; }}");

                    EmitTypeMembers(sub, cpp, cs, implSb, database, namespaceBind, "            ", voidType);

                    implSb.AppendLine("        }");
                    cs.AppendLine("    }");
                }

                cs.AppendLine("    }");
                implSb.AppendLine("    }");
                implSb.AppendLine("}");
                string implPath = GetImplOutputPath(t.Value, outputDir);
                string implDir = System.IO.Path.GetDirectoryName(implPath);
                if (!string.IsNullOrEmpty(implDir) && !System.IO.Directory.Exists(implDir))
                    System.IO.Directory.CreateDirectory(implDir);
                System.IO.File.WriteAllText(implPath, implSb.ToString());
            }

            // Global functions
            if (database.globalFunctions_.Count > 0)
            {
                cpp.AppendLine("");
                cs.AppendLine("");
                cs.AppendLine($"    public static partial class {globalBindingsClassName}_Bindings {{");

                StringBuilder globalImplSb = new StringBuilder();
                globalImplSb.AppendLine("using System;");
                globalImplSb.AppendLine("using System.Collections.Generic;");
                globalImplSb.AppendLine("using System.Linq;");
                globalImplSb.AppendLine("using System.Text;");
                globalImplSb.AppendLine("using System.Runtime.CompilerServices;");
                globalImplSb.AppendLine("");
                globalImplSb.AppendLine($"namespace {namespaceBind} {{");
                globalImplSb.AppendLine($"    public static partial class {globalBindingsClassName} {{");

                foreach (var method in database.globalFunctions_)
                {
                    bool isStatic = true;
                    cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");

                    bool[] isStringArg = new bool[method.argumentTypes_.Count];
                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                        isStringArg[i] = method.argumentTypes_[i].type_ != null && method.argumentTypes_[i].type_.typeName_ == "std::string";

                    string methCall = "[](";
                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                    {
                        if (i > 0) methCall += ", ";
                        methCall += isStringArg[i] ? "MonoString*" : TypeToMonoType(method.argumentTypes_[i].type_);
                        methCall += $" {method.argumentNames_[i]}";
                    }
                    methCall += ") {";

                    string stringSetup = "";
                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                    {
                        if (isStringArg[i])
                            stringSetup += $"char* {method.argumentNames_[i]}_utf8 = mono_string_to_utf8({method.argumentNames_[i]}); ";
                    }
                    methCall += " " + stringSetup;

                    string csCall = "        internal static ";
                    string globalImplCall = "        public static ";
                    string globalImplDispatch = "";

                    bool returnsString = method.returnType_ != null && method.returnType_.type_ != voidType && method.returnType_.type_.typeName_ == "std::string";

                    if (method.returnType_ == null || method.returnType_.type_ == voidType)
                    {
                        methCall += $"{method.methodName_}(";
                        csCall += $"void {method.methodName_}(";
                        globalImplCall += $"void {method.methodName_}(";
                        globalImplDispatch += $"{namespaceBind}.{globalBindingsClassName}_Bindings.{method.methodName_}(";
                    }
                    else if (returnsString)
                    {
                        methCall += $"auto __cs_ret = {method.methodName_}(";
                        csCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                        globalImplCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                        globalImplDispatch += $"return {namespaceBind}.{globalBindingsClassName}_Bindings.{method.methodName_}(";
                    }
                    else
                    {
                        methCall += $"return {method.methodName_}(";
                        csCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                        globalImplCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                        globalImplDispatch += $"return {namespaceBind}.{globalBindingsClassName}_Bindings.{method.methodName_}(";
                    }

                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                    {
                        if (i > 0) methCall += ", ";
                        methCall += isStringArg[i] ? $"{method.argumentNames_[i]}_utf8" : method.argumentNames_[i];

                        if (i > 0)
                        {
                            csCall += ", ";
                            globalImplCall += ", ";
                            globalImplDispatch += ", ";
                        }
                        csCall += $"{TypeToMonoType(method.argumentTypes_[i].type_)} {method.argumentNames_[i]}";
                        globalImplCall += $"{TypeToMonoType(method.argumentTypes_[i].type_)} {method.argumentNames_[i]}";
                        globalImplDispatch += method.argumentNames_[i];
                    }

                    if (returnsString)
                    {
                        for (int i = 0; i < method.argumentTypes_.Count; ++i)
                            if (isStringArg[i])
                                methCall += $" mono_free({method.argumentNames_[i]}_utf8);";
                        methCall += " return mono_string_new(mono_domain_get(), __cs_ret.c_str()); })";
                    }
                    else
                    {
                        methCall += ");";
                        for (int i = 0; i < method.argumentTypes_.Count; ++i)
                            if (isStringArg[i])
                                methCall += $" mono_free({method.argumentNames_[i]}_utf8);";
                        methCall += " })";
                    }
                    csCall += ");";
                    globalImplCall += ") { ";
                    globalImplDispatch += ");";

                    string bindStr = $"    mono_add_internal_call(\"{namespaceBind}.{globalBindingsClassName}_Bindings::{method.methodName_}\", {methCall});";

                    cpp.AppendLine(bindStr);
                    cs.AppendLine(csCall);
                    globalImplSb.AppendLine($"        {globalImplCall}{globalImplDispatch} }}");
                }

                cs.AppendLine("    }");
                globalImplSb.AppendLine("    }");
                globalImplSb.AppendLine("}");
                string globalImplPath = System.IO.Path.Combine(outputDir, $"{globalBindingsClassName}.cs");
                string globalImplDir = System.IO.Path.GetDirectoryName(globalImplPath);
                if (!string.IsNullOrEmpty(globalImplDir) && !System.IO.Directory.Exists(globalImplDir))
                    System.IO.Directory.CreateDirectory(globalImplDir);
                System.IO.File.WriteAllText(globalImplPath, globalImplSb.ToString());
            }

            cs.AppendLine("}");
            cpp.AppendLine("}");
        }

        void EmitTypeMembers(CodeScanDB.ReflectedType type, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, string indent, CodeScanDB.ReflectedType voidType)
        {
            string bindingsClass = $"{namespaceBind}.{type.typeName_}_Bindings";
            string cppTypeName = type.typeName_;

            foreach (var prop in type.properties_)
            {
                if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Construct) || prop.accessModifiers_.HasFlag(AccessModifiers.AM_Destruct))
                    continue;

                if (prop.arraySize_ > 0)
                {
                    WriteRawArray(prop, cpp, cs, implSb, database, namespaceBind, type, indent);
                    continue;
                }

                if (prop.IsTemplate)
                {
                    if (prop.type_ == database.GetType("std::array"))
                        WriteStdArray(prop, cpp, cs, implSb, database, namespaceBind, type);
                    else if (prop.type_ == database.GetType("std::vector"))
                        WriteStdVector(prop, cpp, cs, implSb, database, namespaceBind, type);
                    else if (prop.type_ == database.GetType("std::shared_ptr"))
                        WriteSharedPtr(prop, cpp, cs, implSb, database, namespaceBind, type);
                    else if (prop.type_ == database.GetType("std::unique_ptr"))
                        WriteUniquePtr(prop, cpp, cs, implSb, database, namespaceBind, type);
                    else if (prop.type_ == database.GetType("MonoCallable"))
                        WriteMonoCallable(prop, cpp, cs, implSb, database, namespaceBind, type);

                    continue;
                }

                if (prop.type_ != null && prop.type_.typeName_ == "std::string")
                {
                    WriteStringProperty(prop, cpp, cs, implSb, database, namespaceBind, type);
                    continue;
                }

                // basic property
                if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Virtual))
                {
                    var getFunc = prop.bindingData_.Get("get");
                    var setFunc = prop.bindingData_.Get("set");

                    if (string.IsNullOrEmpty(getFunc))
                        continue;

                    cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{prop.propertyName_}\", [](void* obj) {{ return (({cppTypeName}* const)obj)->{getFunc}(); }});");

                    cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                    cs.AppendLine($"        internal static {TypeToMonoType(prop.type_)} Get_{prop.propertyName_}(IntPtr thisObj);");

                    implSb.AppendLine($"{indent}public {TypeToMonoType(prop.type_)} {prop.propertyName_} {{ get {{ return {bindingsClass}.Get_{prop.propertyName_}(NativePtr); }} ");

                    if (!string.IsNullOrEmpty(setFunc))
                    {
                        cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{prop.propertyName_}\", [](void* obj, {TypeToMonoType(prop.type_)} v) {{ (({cppTypeName}*)obj)->{setFunc}(v); }});");
                        cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                        cs.AppendLine($"        internal static void Set_{prop.propertyName_}(IntPtr thisObj, {TypeToMonoType(prop.type_)} value);");

                        implSb.Append($"set {{ {bindingsClass}.Set_{prop.propertyName_}(NativePtr, value) }} ");
                    }
                    implSb.AppendLine("}");
                }
                else // concrete property
                {
                    if (TypeIsMonoSafe(prop.type_))
                    {
                        string funcGet = $"[](void* obj) {{ return (({cppTypeName}* const)obj)->{prop.propertyName_}; }}";
                        string funcSet = $"[](void* obj, {TypeToMonoType(prop.type_)} v) {{ (({cppTypeName}*)obj)->{prop.propertyName_} = v; }}";

                        cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{prop.propertyName_}\", {funcGet});");
                        cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{prop.propertyName_}\", {funcSet});");

                        cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                        cs.AppendLine($"        internal static {TypeToMonoType(prop.type_)} Get_{prop.propertyName_}(IntPtr thisObj);");
                        cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                        cs.AppendLine($"        internal static void Set_{prop.propertyName_}(IntPtr thisObj, {TypeToMonoType(prop.type_)} value);");

                        implSb.AppendLine($"{indent}public {TypeToMonoType(prop.type_)} {prop.propertyName_} {{ get {{ return {bindingsClass}.Get_{prop.propertyName_}(NativePtr); }} set {{ {bindingsClass}.Set_{prop.propertyName_}(NativePtr, value); }} }}");
                    }
                }
            }

            foreach (var method in type.methods_)
            {
                if (method.accessModifiers_.HasFlag(AccessModifiers.AM_Construct) || method.accessModifiers_.HasFlag(AccessModifiers.AM_Destruct))
                    continue;

                bool isStatic = method.accessModifiers_.HasFlag(AccessModifiers.AM_Static);

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");

                bool[] isStringArg = new bool[method.argumentTypes_.Count];
                for (int i = 0; i < method.argumentTypes_.Count; ++i)
                    isStringArg[i] = method.argumentTypes_[i].type_ != null && method.argumentTypes_[i].type_.typeName_ == "std::string";

                string methCall = isStatic ? "[](" : "[](void* call_obj";
                for (int i = 0; i < method.argumentTypes_.Count; ++i)
                {
                    methCall += ", ";
                    methCall += isStringArg[i] ? "MonoString*" : TypeToMonoType(method.argumentTypes_[i].type_);
                    methCall += $" {method.argumentNames_[i]}";
                }
                methCall += ") {";

                string stringSetup = "";
                for (int i = 0; i < method.argumentTypes_.Count; ++i)
                {
                    if (isStringArg[i])
                        stringSetup += $"char* {method.argumentNames_[i]}_utf8 = mono_string_to_utf8({method.argumentNames_[i]}); ";
                }
                methCall += " " + stringSetup;

                string csCall = "        internal static ";
                string implSbCall = $"{indent}{(isStatic ? "public static " : "public ")}";
                string implSbDispatch = "";

                bool returnsString = method.returnType_ != null && method.returnType_.type_ != voidType && method.returnType_.type_.typeName_ == "std::string";

                if (method.returnType_ == null || method.returnType_.type_ == voidType)
                {
                    if (isStatic)
                        methCall += $"{cppTypeName}::{method.methodName_}(";
                    else
                        methCall += $"(({cppTypeName}*)call_obj)->{method.methodName_}(";
                    csCall += $"void {method.methodName_}({(isStatic ? "" : "IntPtr thisObj")}";
                    implSbCall += $"void {method.methodName_}(";
                    implSbDispatch += $"{(isStatic ? "" : $"{bindingsClass}.{method.methodName_}(")}";
                }
                else if (returnsString)
                {
                    if (isStatic)
                        methCall += $"auto __cs_ret = {cppTypeName}::{method.methodName_}(";
                    else
                        methCall += $"auto __cs_ret = (({cppTypeName}*)call_obj)->{method.methodName_}(";
                    csCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}({(isStatic ? "" : "IntPtr thisObj")}";
                    implSbCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                    implSbDispatch += $"return {(isStatic ? "" : $"{bindingsClass}.{method.methodName_}(")}";
                }
                else
                {
                    if (isStatic)
                        methCall += $"return {cppTypeName}::{method.methodName_}(";
                    else
                        methCall += $"return (({cppTypeName}*)call_obj)->{method.methodName_}(";
                    csCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}({(isStatic ? "" : "IntPtr thisObj")}";
                    implSbCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                    implSbDispatch += $"return {(isStatic ? "" : $"{bindingsClass}.{method.methodName_}(")}";
                }

                for (int i = 0; i < method.argumentTypes_.Count; ++i)
                {
                    if (i > 0)
                        methCall += ", ";
                    methCall += isStringArg[i] ? $"{method.argumentNames_[i]}_utf8" : method.argumentNames_[i];

                    csCall += ", ";
                    csCall += $"{TypeToMonoType(method.argumentTypes_[i].type_)} {method.argumentNames_[i]}";

                    if (i > 0)
                        implSbCall += ", ";
                    implSbCall += $"{TypeToMonoType(method.argumentTypes_[i].type_)} {method.argumentNames_[i]}";

                    if (!isStatic)
                    {
                        implSbDispatch += ", ";
                        implSbDispatch += method.argumentNames_[i];
                    }
                }

                if (isStatic)
                {
                    // For static methods, dispatch goes directly (no NativePtr)
                    if (implSbDispatch.StartsWith("return "))
                        implSbDispatch = "return " + $"{bindingsClass}.{method.methodName_}(";
                    else
                        implSbDispatch = $"{bindingsClass}.{method.methodName_}(";

                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                    {
                        if (i > 0) implSbDispatch += ", ";
                        implSbDispatch += method.argumentNames_[i];
                    }
                }

                if (returnsString)
                {
                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                        if (isStringArg[i])
                            methCall += $" mono_free({method.argumentNames_[i]}_utf8);";
                    methCall += " return mono_string_new(mono_domain_get(), __cs_ret.c_str()); })";
                }
                else
                {
                    methCall += ");";
                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                        if (isStringArg[i])
                            methCall += $" mono_free({method.argumentNames_[i]}_utf8);";
                    methCall += " })";
                }
                csCall += ");";
                implSbCall += ") { ";
                implSbDispatch += ");";

                string bindStr = $"    mono_add_internal_call(\"{bindingsClass}::{method.methodName_}\", {methCall});";

                cpp.AppendLine(bindStr);
                cs.AppendLine(csCall);
                implSb.AppendLine($"{implSbCall}{implSbDispatch} }}");
            }
        }

        protected virtual string GetImplOutputPath(CodeScanDB.ReflectedType type, string outputDir)
        {
            return System.IO.Path.Combine(outputDir, $"{type.typeName_}.cs");
        }

        bool TypeIsMonoSafe(CodeScanDB.ReflectedType t)
        {
            if (t.IsNumeric)
                return true;
            if (!t.isClass_)
                return true;
            return false;
        }

        string TypeToMonoType(CodeScanDB.ReflectedType t)
        {
            if (t.isPrimitive_ && t.IsNumeric)
            {
                switch (t.typeName_)
                {
                    case "short": return "short";
                    case "int": return "int";
                    case "uint32_t": return "uint";
                    case "int8_t": return "sbyte";
                    case "uint8_t": return "byte";
                    case "int16_t": return "short";
                    case "uint16_t": return "ushort";
                    case "int64_t": return "long";
                    case "uint64_t": return "ulong";
                    case "float": return "float";
                    case "double": return "double";
                }
            }
            if (t.typeName_ == "std::string")
                return "string";
            if (t.IsEnum)
                return "int";
            if (!t.isClass_)
                return t.typeName_;
            return "IntPtr";
        }

        static readonly HashSet<string> DirectMarshalTypes = new HashSet<string>
        {
            "int", "float", "double", "short", "long", "byte"
        };

        static string IntermediaryMarshalType(string csTypeName)
        {
            switch (csTypeName)
            {
                case "uint": return "int";
                case "ushort": return "short";
                case "ulong": return "long";
                case "sbyte": return "byte";
                case "bool": return "byte";
                default: return csTypeName;
            }
        }

        string PropertyToMono(CodeScanDB.Property p)
        {

            return "MonoObject*";
        }

        string TypeToMonoCPP(CodeScanDB.ReflectedType t)
        {
            return "MonoObject*";
        }

        void WriteStdArray(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            if (prop.templateParameters_.Count != 2)
                return;

            var elemProp = prop.templateParameters_[0]?.Type;
            if (elemProp == null || elemProp.type_ == null)
                return;

            if (!prop.templateParameters_[1].IsInteger)
                return;

            var elemType = elemProp.type_;
            int arraySize = prop.templateParameters_[1].IntegerValue;
            string csElemType = TypeToMonoType(elemType);
            string csClassType = elemType.typeName_;
            string cppElemType = elemProp.GetFullTypeName(true);
            string bindingsClass = $"{namespaceBind}.{owningType.typeName_}_Bindings";
            string propName = prop.propertyName_;
            string cppTypeName = owningType.typeName_;

            if (elemType.isClass_)
            {
                // Class elements — indexed access
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}_Item\", [](void* obj, int index) -> void* {{ return &(({cppTypeName}*)obj)->{propName}[index]; }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_Item\", [](void* obj, int index, void* value) {{ (({cppTypeName}*)obj)->{propName}[index] = *({cppElemType}*)value; }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Item(IntPtr thisObj, int index);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_Item(IntPtr thisObj, int index, IntPtr value);");

                implSb.AppendLine($"        public int {propName}_Count => {arraySize};");
                implSb.AppendLine($"        public {csClassType} Get_{propName}(int index) {{ IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, index); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }}");
                implSb.AppendLine($"        public void Set_{propName}(int index, {csClassType} value) {{ {bindingsClass}.Set_{propName}_Item(NativePtr, index, value?.NativePtr ?? IntPtr.Zero); }}");
                implSb.AppendLine($"        public {csClassType}[] {propName} {{");
                implSb.AppendLine($"            get {{");
                implSb.AppendLine($"                {csClassType}[] result = new {csClassType}[{arraySize}];");
                implSb.AppendLine($"                for (int i = 0; i < {arraySize}; i++) {{");
                implSb.AppendLine($"                    IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, i);");
                implSb.AppendLine($"                    result[i] = ptr != IntPtr.Zero ? new {csClassType}(ptr) : null;");
                implSb.AppendLine($"                }}");
                implSb.AppendLine($"                return result;");
                implSb.AppendLine($"            }}");
                implSb.AppendLine($"            set {{");
                implSb.AppendLine($"                if (value == null) value = new {csClassType}[0];");
                implSb.AppendLine($"                for (int i = 0; i < {arraySize} && i < value.Length; i++)");
                implSb.AppendLine($"                    {bindingsClass}.Set_{propName}_Item(NativePtr, i, value[i]?.NativePtr ?? IntPtr.Zero);");
                implSb.AppendLine($"            }}");
                implSb.AppendLine($"        }}");
            }
            else
            {
                // Blittable elements — bulk pointer access
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}\", [](void* obj) -> void* {{ return (void*)(({cppTypeName}*)obj)->{propName}.data(); }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}\", [](void* obj, void* data) {{ auto& arr = (({cppTypeName}*)obj)->{propName}; memcpy(arr.data(), data, {arraySize} * sizeof({cppElemType})); }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}(IntPtr thisObj);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}(IntPtr thisObj, IntPtr data);");

                // Determine if we can use Marshal.Copy directly
                bool marshalDirect = DirectMarshalTypes.Contains(csElemType);

                if (marshalDirect)
                {
                    implSb.AppendLine($"        public {csElemType}[] {propName} {{");
                    implSb.AppendLine($"            get {{ IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr); {csElemType}[] result = new {csElemType}[{arraySize}]; System.Runtime.InteropServices.Marshal.Copy(ptr, result, 0, {arraySize}); return result; }}");
                    implSb.AppendLine($"            set {{ if (value == null || value.Length != {arraySize}) throw new System.ArgumentException(\"Array must have length {arraySize}\"); IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal({arraySize} * System.Runtime.InteropServices.Marshal.SizeOf(typeof({csElemType}))); System.Runtime.InteropServices.Marshal.Copy(value, 0, ptr, {arraySize}); {bindingsClass}.Set_{propName}(NativePtr, ptr); System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }}");
                    implSb.AppendLine($"        }}");
                }
                else
                {
                    // Need intermediary marshaling (e.g. uint via int[])
                    string marshalHelperType = IntermediaryMarshalType(csElemType);
                    implSb.AppendLine($"        public {csElemType}[] {propName} {{");
                    implSb.AppendLine($"            get {{");
                    implSb.AppendLine($"                IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr);");
                    implSb.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[{arraySize}];");
                    implSb.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(ptr, raw, 0, {arraySize});");
                    implSb.AppendLine($"                {csElemType}[] result = new {csElemType}[{arraySize}];");
                    implSb.AppendLine($"                for (int i = 0; i < {arraySize}; i++) result[i] = ({csElemType})raw[i];");
                    implSb.AppendLine($"                return result;");
                    implSb.AppendLine($"            }}");
                    implSb.AppendLine($"            set {{");
                    implSb.AppendLine($"                if (value == null || value.Length != {arraySize}) throw new System.ArgumentException(\"Array must have length {arraySize}\");");
                    implSb.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[{arraySize}];");
                    implSb.AppendLine($"                for (int i = 0; i < {arraySize}; i++) raw[i] = ({marshalHelperType})value[i];");
                    implSb.AppendLine($"                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal({arraySize} * System.Runtime.InteropServices.Marshal.SizeOf(typeof({marshalHelperType})));");
                    implSb.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(raw, 0, ptr, {arraySize});");
                    implSb.AppendLine($"                {bindingsClass}.Set_{propName}(NativePtr, ptr);");
                    implSb.AppendLine($"                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);");
                    implSb.AppendLine($"            }}");
                    implSb.AppendLine($"        }}");
                }
            }
        }

        void WriteStdVector(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            if (prop.templateParameters_.Count != 1)
                return;

            var elemProp = prop.templateParameters_[0]?.Type;
            if (elemProp == null || elemProp.type_ == null)
                return;

            var elemType = elemProp.type_;
            string csElemType = TypeToMonoType(elemType);
            string csClassType = elemType.typeName_;
            string cppElemType = elemProp.GetFullTypeName(true);
            string bindingsClass = $"{namespaceBind}.{owningType.typeName_}_Bindings";
            string propName = prop.propertyName_;
            string cppTypeName = owningType.typeName_;

            cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}_Count\", [](void* obj) -> int {{ return (int)(({cppTypeName}*)obj)->{propName}.size(); }});");

            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static int Get_{propName}_Count(IntPtr thisObj);");

            if (elemType.isClass_)
            {
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}_Item\", [](void* obj, int index) -> void* {{ return &(({cppTypeName}*)obj)->{propName}[index]; }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_Item\", [](void* obj, int index, void* value) {{ (({cppTypeName}*)obj)->{propName}[index] = *({cppElemType}*)value; }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Item(IntPtr thisObj, int index);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_Item(IntPtr thisObj, int index, IntPtr value);");

                implSb.AppendLine($"        public int {propName}_Count => {bindingsClass}.Get_{propName}_Count(NativePtr);");
                implSb.AppendLine($"        public {csClassType} Get_{propName}(int index) {{ IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, index); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }}");
                implSb.AppendLine($"        public void Set_{propName}(int index, {csClassType} value) {{ {bindingsClass}.Set_{propName}_Item(NativePtr, index, value?.NativePtr ?? IntPtr.Zero); }}");
                implSb.AppendLine($"        public {csClassType}[] {propName} {{");
                implSb.AppendLine($"            get {{");
                implSb.AppendLine($"                int count = {bindingsClass}.Get_{propName}_Count(NativePtr);");
                implSb.AppendLine($"                {csClassType}[] result = new {csClassType}[count];");
                implSb.AppendLine($"                for (int i = 0; i < count; i++) {{");
                implSb.AppendLine($"                    IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, i);");
                implSb.AppendLine($"                    result[i] = ptr != IntPtr.Zero ? new {csClassType}(ptr) : null;");
                implSb.AppendLine($"                }}");
                implSb.AppendLine($"                return result;");
                implSb.AppendLine($"            }}");
                implSb.AppendLine($"            set {{");
                implSb.AppendLine($"                if (value == null) value = new {csClassType}[0];");
                implSb.AppendLine($"                int count = {bindingsClass}.Get_{propName}_Count(NativePtr);");
                implSb.AppendLine($"                for (int i = 0; i < count && i < value.Length; i++)");
                implSb.AppendLine($"                    {bindingsClass}.Set_{propName}_Item(NativePtr, i, value[i]?.NativePtr ?? IntPtr.Zero);");
                implSb.AppendLine($"            }}");
                implSb.AppendLine($"        }}");
            }
            else
            {
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}_Data\", [](void* obj) -> void* {{ return (void*)(({cppTypeName}*)obj)->{propName}.data(); }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_FromArray\", [](void* obj, void* data, int count) {{ auto& vec = (({cppTypeName}*)obj)->{propName}; vec.assign(({cppElemType}*)data, ({cppElemType}*)data + count); }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Data(IntPtr thisObj);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_FromArray(IntPtr thisObj, IntPtr data, int count);");

                bool marshalDirect = DirectMarshalTypes.Contains(csElemType);

                if (marshalDirect)
                {
                    implSb.AppendLine($"        public {csElemType}[] {propName} {{");
                    implSb.AppendLine($"            get {{ int count = {bindingsClass}.Get_{propName}_Count(NativePtr); IntPtr ptr = {bindingsClass}.Get_{propName}_Data(NativePtr); {csElemType}[] result = new {csElemType}[count]; System.Runtime.InteropServices.Marshal.Copy(ptr, result, 0, count); return result; }}");
                    implSb.AppendLine($"            set {{ if (value == null) value = new {csElemType}[0]; IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(value.Length * System.Runtime.InteropServices.Marshal.SizeOf(typeof({csElemType}))); System.Runtime.InteropServices.Marshal.Copy(value, 0, ptr, value.Length); {bindingsClass}.Set_{propName}_FromArray(NativePtr, ptr, value.Length); System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }}");
                    implSb.AppendLine($"        }}");
                }
                else
                {
                    string marshalHelperType = IntermediaryMarshalType(csElemType);
                    implSb.AppendLine($"        public {csElemType}[] {propName} {{");
                    implSb.AppendLine($"            get {{");
                    implSb.AppendLine($"                int count = {bindingsClass}.Get_{propName}_Count(NativePtr);");
                    implSb.AppendLine($"                IntPtr ptr = {bindingsClass}.Get_{propName}_Data(NativePtr);");
                    implSb.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[count];");
                    implSb.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(ptr, raw, 0, count);");
                    implSb.AppendLine($"                {csElemType}[] result = new {csElemType}[count];");
                    implSb.AppendLine($"                for (int i = 0; i < count; i++) result[i] = ({csElemType})raw[i];");
                    implSb.AppendLine($"                return result;");
                    implSb.AppendLine($"            }}");
                    implSb.AppendLine($"            set {{");
                    implSb.AppendLine($"                if (value == null) value = new {csElemType}[0];");
                    implSb.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[value.Length];");
                    implSb.AppendLine($"                for (int i = 0; i < value.Length; i++) raw[i] = ({marshalHelperType})value[i];");
                    implSb.AppendLine($"                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(value.Length * System.Runtime.InteropServices.Marshal.SizeOf(typeof({marshalHelperType})));");
                    implSb.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(raw, 0, ptr, value.Length);");
                    implSb.AppendLine($"                {bindingsClass}.Set_{propName}_FromArray(NativePtr, ptr, value.Length);");
                    implSb.AppendLine($"                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);");
                    implSb.AppendLine($"            }}");
                    implSb.AppendLine($"        }}");
                }
            }
        }

        void WriteRawArray(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType, string indent)
        {
            var elemType = prop.type_;
            if (elemType == null)
                return;

            int arraySize = prop.arraySize_;
            string csElemType = TypeToMonoType(elemType);
            string csClassType = elemType.typeName_;
            string bindingsClass = $"{namespaceBind}.{owningType.typeName_}_Bindings";
            string propName = prop.propertyName_;
            string cppTypeName = owningType.typeName_;

            if (elemType.isClass_)
            {
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}_Item\", [](void* obj, int index) -> void* {{ return &(({cppTypeName}*)obj)->{propName}[index]; }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_Item\", [](void* obj, int index, void* value) {{ (({cppTypeName}*)obj)->{propName}[index] = *({elemType.typeName_}*)value; }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Item(IntPtr thisObj, int index);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_Item(IntPtr thisObj, int index, IntPtr value);");

                implSb.AppendLine($"{indent}public int {propName}_Count => {arraySize};");
                implSb.AppendLine($"{indent}public {csClassType} Get_{propName}(int index) {{ IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, index); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }}");
                implSb.AppendLine($"{indent}public void Set_{propName}(int index, {csClassType} value) {{ {bindingsClass}.Set_{propName}_Item(NativePtr, index, value?.NativePtr ?? IntPtr.Zero); }}");
                implSb.AppendLine($"{indent}public {csClassType}[] {propName} {{");
                implSb.AppendLine($"{indent}    get {{");
                implSb.AppendLine($"{indent}        {csClassType}[] result = new {csClassType}[{arraySize}];");
                implSb.AppendLine($"{indent}        for (int i = 0; i < {arraySize}; i++) {{");
                implSb.AppendLine($"{indent}            IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, i);");
                implSb.AppendLine($"{indent}            result[i] = ptr != IntPtr.Zero ? new {csClassType}(ptr) : null;");
                implSb.AppendLine($"{indent}        }}");
                implSb.AppendLine($"{indent}        return result;");
                implSb.AppendLine($"{indent}    }}");
                implSb.AppendLine($"{indent}    set {{");
                implSb.AppendLine($"{indent}        if (value == null) value = new {csClassType}[0];");
                implSb.AppendLine($"{indent}        for (int i = 0; i < {arraySize} && i < value.Length; i++)");
                implSb.AppendLine($"{indent}            {bindingsClass}.Set_{propName}_Item(NativePtr, i, value[i]?.NativePtr ?? IntPtr.Zero);");
                implSb.AppendLine($"{indent}    }}");
                implSb.AppendLine($"{indent}}}");
            }
            else
            {
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}\", [](void* obj) -> void* {{ return (void*)(({cppTypeName}*)obj)->{propName}; }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}\", [](void* obj, void* data) {{ auto& arr = ({cppTypeName}*)obj->{propName}; memcpy(arr, data, {arraySize} * sizeof({elemType.typeName_})); }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}(IntPtr thisObj);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}(IntPtr thisObj, IntPtr data);");

                bool marshalDirect = DirectMarshalTypes.Contains(csElemType);

                if (marshalDirect)
                {
                    implSb.AppendLine($"{indent}public {csElemType}[] {propName} {{");
                    implSb.AppendLine($"{indent}    get {{ IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr); {csElemType}[] result = new {csElemType}[{arraySize}]; System.Runtime.InteropServices.Marshal.Copy(ptr, result, 0, {arraySize}); return result; }}");
                    implSb.AppendLine($"{indent}    set {{ if (value == null || value.Length != {arraySize}) throw new System.ArgumentException(\"Array must have length {arraySize}\"); IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal({arraySize} * System.Runtime.InteropServices.Marshal.SizeOf(typeof({csElemType}))); System.Runtime.InteropServices.Marshal.Copy(value, 0, ptr, {arraySize}); {bindingsClass}.Set_{propName}(NativePtr, ptr); System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }}");
                    implSb.AppendLine($"{indent}}}");
                }
                else
                {
                    string marshalHelperType = IntermediaryMarshalType(csElemType);
                    implSb.AppendLine($"{indent}public {csElemType}[] {propName} {{");
                    implSb.AppendLine($"{indent}    get {{");
                    implSb.AppendLine($"{indent}        IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr);");
                    implSb.AppendLine($"{indent}        {marshalHelperType}[] raw = new {marshalHelperType}[{arraySize}];");
                    implSb.AppendLine($"{indent}        System.Runtime.InteropServices.Marshal.Copy(ptr, raw, 0, {arraySize});");
                    implSb.AppendLine($"{indent}        {csElemType}[] result = new {csElemType}[{arraySize}];");
                    implSb.AppendLine($"{indent}        for (int i = 0; i < {arraySize}; i++) result[i] = ({csElemType})raw[i];");
                    implSb.AppendLine($"{indent}        return result;");
                    implSb.AppendLine($"{indent}    }}");
                    implSb.AppendLine($"{indent}    set {{");
                    implSb.AppendLine($"{indent}        if (value == null || value.Length != {arraySize}) throw new System.ArgumentException(\"Array must have length {arraySize}\");");
                    implSb.AppendLine($"{indent}        {marshalHelperType}[] raw = new {marshalHelperType}[{arraySize}];");
                    implSb.AppendLine($"{indent}        for (int i = 0; i < {arraySize}; i++) raw[i] = ({marshalHelperType})value[i];");
                    implSb.AppendLine($"{indent}        IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal({arraySize} * System.Runtime.InteropServices.Marshal.SizeOf(typeof({marshalHelperType})));");
                    implSb.AppendLine($"{indent}        System.Runtime.InteropServices.Marshal.Copy(raw, 0, ptr, {arraySize});");
                    implSb.AppendLine($"{indent}        {bindingsClass}.Set_{propName}(NativePtr, ptr);");
                    implSb.AppendLine($"{indent}        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);");
                    implSb.AppendLine($"{indent}    }}");
                    implSb.AppendLine($"{indent}}}");
                }
            }
        }

        void WriteStringProperty(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{owningType.typeName_}_Bindings::Get_{prop.propertyName_}\", [](void* obj) -> MonoString* {{ auto& s = (({owningType.typeName_}*)obj)->{prop.propertyName_}; return mono_string_new(mono_domain_get(), s.c_str()); }});");

            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{owningType.typeName_}_Bindings::Set_{prop.propertyName_}\", [](void* obj, MonoString* value) {{ char* utf8 = mono_string_to_utf8(value); (({owningType.typeName_}*)obj)->{prop.propertyName_} = utf8; mono_free(utf8); }});");

            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static string Get_{prop.propertyName_}(IntPtr thisObj);");
            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static void Set_{prop.propertyName_}(IntPtr thisObj, string value);");

            implSb.Append($"        public string {prop.propertyName_} {{ get {{ return {namespaceBind}.{owningType.typeName_}_Bindings.Get_{prop.propertyName_}(NativePtr); }} ");
            implSb.AppendLine($"set {{ {namespaceBind}.{owningType.typeName_}_Bindings.Set_{prop.propertyName_}(NativePtr, value); }} }}");
        }

        void WriteSharedPtr(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            if (prop.templateParameters_.Count != 1)
                return;

            var elemProp = prop.templateParameters_[0]?.Type;
            if (elemProp == null || elemProp.type_ == null)
                return;

            var elemType = elemProp.type_;
            string csElemType = TypeToMonoType(elemType);
            string csClassType = elemType.typeName_;
            string bindingsClass = $"{namespaceBind}.{owningType.typeName_}_Bindings";
            string propName = prop.propertyName_;
            string cppTypeName = owningType.typeName_;

            cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}\", [](void* obj) -> void* {{ return (void*)(({cppTypeName}*)obj)->{propName}.get(); }});");

            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static IntPtr Get_{propName}(IntPtr thisObj);");

            if (elemType.isClass_)
                implSb.AppendLine($"        public {csClassType} {propName} {{ get {{ IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }} }}");
            else
                implSb.AppendLine($"        public IntPtr {propName} {{ get {{ return {bindingsClass}.Get_{propName}(NativePtr); }} }}");
        }

        void WriteUniquePtr(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            if (prop.templateParameters_.Count != 1)
                return;

            var elemProp = prop.templateParameters_[0]?.Type;
            if (elemProp == null || elemProp.type_ == null)
                return;

            var elemType = elemProp.type_;
            string csElemType = TypeToMonoType(elemType);
            string csClassType = elemType.typeName_;
            string bindingsClass = $"{namespaceBind}.{owningType.typeName_}_Bindings";
            string propName = prop.propertyName_;
            string cppTypeName = owningType.typeName_;

            cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}\", [](void* obj) -> void* {{ return (void*)(({cppTypeName}*)obj)->{propName}.get(); }});");

            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static IntPtr Get_{propName}(IntPtr thisObj);");

            if (elemType.isClass_)
                implSb.AppendLine($"        public {csClassType} {propName} {{ get {{ IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }} }}");
            else
                implSb.AppendLine($"        public IntPtr {propName} {{ get {{ return {bindingsClass}.Get_{propName}(NativePtr); }} }}");
        }

        void WriteMonoCallable(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder implSb, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            string bindingsClass = $"{namespaceBind}.{owningType.typeName_}_Bindings";
            string cppTypeName = owningType.typeName_;
            string propName = prop.propertyName_;

            cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}\", [](void* obj, MonoObject* callback) {{ (({cppTypeName}*)obj)->{propName}.SetMonoDelegate(callback); }});");

            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static void Set_{propName}(IntPtr thisObj, System.Delegate callback);");

            implSb.AppendLine($"        public System.Delegate {propName} {{ set {{ {bindingsClass}.Set_{propName}(NativePtr, value); }} }}");
        }
    }
}
