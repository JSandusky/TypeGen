using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class MonoGenerator
    {
        public void WriteMonoBindings(StringBuilder cpp, StringBuilder cs, CodeScanDB database, string namespaceBind)
        {
            var voidType = database.GetType("void");

            StringBuilder csClean = new StringBuilder();

            cpp.AppendLine("#include <mono/jit/jit.h>");
            cpp.AppendLine("#include <mono/metadata/assembly.h>");
            cpp.AppendLine("#include <mono/metadata/reflection.h>");
            cpp.AppendLine("#include <mono/metadata/object.h>");
            cpp.AppendLine("#include <mono/metadata/class.h>");
            cpp.AppendLine("#include <mono/metadata/appdomain.h>");
            cpp.AppendLine("#include <string>");
            cpp.AppendLine("");
            cpp.AppendLine("void BindMono() {");

            cs.AppendLine("using System;");
            cs.AppendLine("using System.Collections.Generic;");
            cs.AppendLine("using System.Linq;");
            cs.AppendLine("using System.Text;");
            cs.AppendLine("using System.Runtime.CompilerServices;");
            cs.AppendLine("");
            cs.AppendLine($"namespace {namespaceBind} {{");

            csClean.AppendLine("using System;");
            csClean.AppendLine("using System.Collections.Generic;");
            csClean.AppendLine("using System.Linq;");
            csClean.AppendLine("using System.Text;");
            csClean.AppendLine("using System.Runtime.CompilerServices;");
            csClean.AppendLine("");
            csClean.AppendLine($"namespace {namespaceBind} {{");

            foreach (var t in database.types_)
            {
                if (t.Value.IsEnum || t.Value.isInternal_ || t.Value.isPrimitive_)
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

                csClean.AppendLine("");
                if (baseName != null)
                    csClean.AppendLine($"    public partial class {t.Value.typeName_} : {baseName} {{");
                else
                    csClean.AppendLine($"    public partial class {t.Value.typeName_} {{");

                if (baseName == null)
                {
                    csClean.AppendLine("        public IntPtr NativePtr { get; internal set; }");
                    csClean.AppendLine("        public bool IsNull { get { return NativePtr == IntPtr.Zero; } }");
                    csClean.AppendLine("        public bool IsValid { get { return NativePtr != IntPtr.Zero; } }");
                }

                if (baseName != null)
                    csClean.AppendLine($"        public {t.Value.typeName_}(IntPtr ptr) : base(ptr) {{ }}");
                else
                    csClean.AppendLine($"        public {t.Value.typeName_}(IntPtr ptr) {{ NativePtr = ptr; }}");

                foreach (var prop in t.Value.properties_)
                {
                    if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Construct) || prop.accessModifiers_.HasFlag(AccessModifiers.AM_Destruct))
                        continue;

                    if (prop.IsTemplate)
                    {
                        if (prop.type_ == database.GetType("std::array"))
                            WriteStdArray(prop, cpp, cs, csClean, database, namespaceBind, t.Value);
                        else if (prop.type_ == database.GetType("std::vector"))
                            WriteStdVector(prop, cpp, cs, csClean, database, namespaceBind, t.Value);
                        else if (prop.type_ == database.GetType("std::shared_ptr"))
                            WriteSharedPtr(prop, cpp, cs, database, namespaceBind);
                        else if (prop.type_ == database.GetType("std::unique_ptr"))
                            WriteUniquePtr(prop, cpp, cs, database, namespaceBind);                        

                        continue;
                    }

                    if (prop.type_ != null && prop.type_.typeName_ == "std::string")
                    {
                        WriteStringProperty(prop, cpp, cs, csClean, database, namespaceBind, t.Value);
                        continue;
                    }

                    // basic property
                    if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Virtual))
                    {
                        var getFunc = prop.bindingData_.Get("get");
                        var setFunc = prop.bindingData_.Get("set");

                        if (string.IsNullOrEmpty(getFunc))
                            continue;

                        cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{t.Value.typeName_}_Bindings::Get_{prop.propertyName_}\", [](void* obj) {{ return (({t.Value.typeName_}* const)obj)->{getFunc}(); }});");

                        cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                        cs.AppendLine($"        internal static {TypeToMonoType(prop.type_)} Get_{prop.propertyName_}(IntPtr thisObj);");

                        csClean.Append($"        public {TypeToMonoType(prop.type_)} {prop.propertyName_} {{ get {{ return {namespaceBind}.{t.Value.typeName_}_Bindings.Get_{prop.propertyName_}(NativePtr); }} ");

                        if (!string.IsNullOrEmpty(setFunc))
                        {
                            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{t.Value.typeName_}_Bindings::Set_{prop.propertyName_}\", [](void* obj, {TypeToMonoType(prop.type_)} v) {{ (({t.Value.typeName_}*)obj)->{setFunc}(v); }});");
                            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                            cs.AppendLine($"        internal static void Set_{prop.propertyName_}(IntPtr thisObj, {TypeToMonoType(prop.type_)} value);");

                            csClean.Append($"set {{ {namespaceBind}.{t.Value.typeName_}_Bindings.Set_{prop.propertyName_}(NativePtr, value) }} ");
                        }
                        csClean.AppendLine("}");
                    }
                    else // concrete property
                    {
                        if (TypeIsMonoSafe(prop.type_))
                        {
                            string funcGet = $"[](void* obj) {{ return (({t.Value.typeName_}* const)obj)->{prop.propertyName_}; }}";
                            string funcSet = $"[](void* obj, {TypeToMonoType(prop.type_)} v) {{ (({t.Value.typeName_}*)obj)->{prop.propertyName_} = v; }}";

                            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{t.Value.typeName_}_Bindings::Get_{prop.propertyName_}\", {funcGet});");
                            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{t.Value.typeName_}_Bindings::Set_{prop.propertyName_}\", {funcSet});");

                            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                            cs.AppendLine($"        internal static {TypeToMonoType(prop.type_)} Get_{prop.propertyName_}(IntPtr thisObj);");
                            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                            cs.AppendLine($"        internal static void Set_{prop.propertyName_}(IntPtr thisObj, {TypeToMonoType(prop.type_)} value);");

                            csClean.Append($"        public {TypeToMonoType(prop.type_)} {prop.propertyName_} {{ get {{ return {namespaceBind}.{t.Value.typeName_}_Bindings.Get_{prop.propertyName_}(NativePtr); }} ");
                            csClean.AppendLine($"set {{ {namespaceBind}.{t.Value.typeName_}_Bindings.Set_{prop.propertyName_}(NativePtr, value); }} }}");
                        }
                    }
                }

                foreach (var method in t.Value.methods_)
                {
                    if (method.accessModifiers_.HasFlag(AccessModifiers.AM_Construct) || method.accessModifiers_.HasFlag(AccessModifiers.AM_Destruct) || method.accessModifiers_.HasFlag(AccessModifiers.AM_Static))
                        continue;

                    cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");

                    bool[] isStringArg = new bool[method.argumentTypes_.Count];
                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                        isStringArg[i] = method.argumentTypes_[i].type_ != null && method.argumentTypes_[i].type_.typeName_ == "std::string";

                    string methCall = "[](void* call_obj";
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
                    string csCleanCall = "        public ";
                    string csCleanDispatch = "";

                    bool returnsString = method.returnType_ != null && method.returnType_.type_ != voidType && method.returnType_.type_.typeName_ == "std::string";

                    if (method.returnType_ == null || method.returnType_.type_ == voidType)
                    {
                        methCall += $"(({t.Value.typeName_}*)call_obj)->{method.methodName_}(";
                        csCall += $"void {method.methodName_}(IntPtr thisObj";
                        csCleanCall += $"void {method.methodName_}(";
                        csCleanDispatch += $"{namespaceBind}.{t.Value.typeName_}_Bindings.{method.methodName_}(NativePtr";
                    }
                    else if (returnsString)
                    {
                        methCall += $"auto __cs_ret = (({t.Value.typeName_}*)call_obj)->{method.methodName_}(";
                        csCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(IntPtr thisObj";
                        csCleanCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                        csCleanDispatch += $"return {namespaceBind}.{t.Value.typeName_}_Bindings.{method.methodName_}(NativePtr";
                    }
                    else
                    {
                        methCall += $"return (({t.Value.typeName_}*)call_obj)->{method.methodName_}(";
                        csCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(IntPtr thisObj";
                        csCleanCall += $"{TypeToMonoType(method.returnType_.type_)} {method.methodName_}(";
                        csCleanDispatch += $"return {namespaceBind}.{t.Value.typeName_}_Bindings.{method.methodName_}(NativePtr";
                    }

                    for (int i = 0; i < method.argumentTypes_.Count; ++i)
                    {
                        if (i > 0)
                            methCall += ", ";
                        methCall += isStringArg[i] ? $"{method.argumentNames_[i]}_utf8" : method.argumentNames_[i];

                        csCall += ", ";
                        csCall += $"{TypeToMonoType(method.argumentTypes_[i].type_)} {method.argumentNames_[i]}";

                        if (i > 0)
                            csCleanCall += ", ";
                        csCleanCall += $"{TypeToMonoType(method.argumentTypes_[i].type_)} {method.argumentNames_[i]}";

                        csCleanDispatch += ", ";
                        csCleanDispatch += method.argumentNames_[i];
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
                    csCleanCall += ") { ";
                    csCleanDispatch += ");";

                    string bindStr = $"    mono_add_internal_call(\"{namespaceBind}.{t.Value.typeName_}_Bindings::{method.methodName_}\", {methCall});";

                    cpp.AppendLine(bindStr);
                    cs.AppendLine(csCall);
                    csClean.AppendLine($"{csCleanCall}{csCleanDispatch} }}");
                }

                cs.AppendLine("    }");
                csClean.AppendLine("    }");
            }

            cs.AppendLine("}");
            cpp.AppendLine("}");
            csClean.AppendLine("}");

            System.IO.File.WriteAllText("MonoClean.cs", csClean.ToString());
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

        void WriteStdArray(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder csClean, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
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
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_Item\", [](void* obj, int index, void* value) {{ (({cppTypeName}*)obj)->{propName}[index] = *(const {cppElemType}*)value; }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Item(IntPtr thisObj, int index);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_Item(IntPtr thisObj, int index, IntPtr value);");

                csClean.AppendLine($"        public int {propName}_Count => {arraySize};");
                csClean.AppendLine($"        public {csClassType} Get_{propName}(int index) {{ IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, index); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }}");
                csClean.AppendLine($"        public void Set_{propName}(int index, {csClassType} value) {{ {bindingsClass}.Set_{propName}_Item(NativePtr, index, value?.NativePtr ?? IntPtr.Zero); }}");
                csClean.AppendLine($"        public {csClassType}[] {propName} {{");
                csClean.AppendLine($"            get {{");
                csClean.AppendLine($"                {csClassType}[] result = new {csClassType}[{arraySize}];");
                csClean.AppendLine($"                for (int i = 0; i < {arraySize}; i++) {{");
                csClean.AppendLine($"                    IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, i);");
                csClean.AppendLine($"                    result[i] = ptr != IntPtr.Zero ? new {csClassType}(ptr) : null;");
                csClean.AppendLine($"                }}");
                csClean.AppendLine($"                return result;");
                csClean.AppendLine($"            }}");
                csClean.AppendLine($"            set {{");
                csClean.AppendLine($"                if (value == null) value = new {csClassType}[0];");
                csClean.AppendLine($"                for (int i = 0; i < {arraySize} && i < value.Length; i++)");
                csClean.AppendLine($"                    {bindingsClass}.Set_{propName}_Item(NativePtr, i, value[i]?.NativePtr ?? IntPtr.Zero);");
                csClean.AppendLine($"            }}");
                csClean.AppendLine($"        }}");
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
                    csClean.AppendLine($"        public {csElemType}[] {propName} {{");
                    csClean.AppendLine($"            get {{ IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr); {csElemType}[] result = new {csElemType}[{arraySize}]; System.Runtime.InteropServices.Marshal.Copy(ptr, result, 0, {arraySize}); return result; }}");
                    csClean.AppendLine($"            set {{ if (value == null || value.Length != {arraySize}) throw new System.ArgumentException(\"Array must have length {arraySize}\"); IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal({arraySize} * System.Runtime.InteropServices.Marshal.SizeOf(typeof({csElemType}))); System.Runtime.InteropServices.Marshal.Copy(value, 0, ptr, {arraySize}); {bindingsClass}.Set_{propName}(NativePtr, ptr); System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }}");
                    csClean.AppendLine($"        }}");
                }
                else
                {
                    // Need intermediary marshaling (e.g. uint via int[])
                    string marshalHelperType = IntermediaryMarshalType(csElemType);
                    csClean.AppendLine($"        public {csElemType}[] {propName} {{");
                    csClean.AppendLine($"            get {{");
                    csClean.AppendLine($"                IntPtr ptr = {bindingsClass}.Get_{propName}(NativePtr);");
                    csClean.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[{arraySize}];");
                    csClean.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(ptr, raw, 0, {arraySize});");
                    csClean.AppendLine($"                {csElemType}[] result = new {csElemType}[{arraySize}];");
                    csClean.AppendLine($"                for (int i = 0; i < {arraySize}; i++) result[i] = ({csElemType})raw[i];");
                    csClean.AppendLine($"                return result;");
                    csClean.AppendLine($"            }}");
                    csClean.AppendLine($"            set {{");
                    csClean.AppendLine($"                if (value == null || value.Length != {arraySize}) throw new System.ArgumentException(\"Array must have length {arraySize}\");");
                    csClean.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[{arraySize}];");
                    csClean.AppendLine($"                for (int i = 0; i < {arraySize}; i++) raw[i] = ({marshalHelperType})value[i];");
                    csClean.AppendLine($"                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal({arraySize} * System.Runtime.InteropServices.Marshal.SizeOf(typeof({marshalHelperType})));");
                    csClean.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(raw, 0, ptr, {arraySize});");
                    csClean.AppendLine($"                {bindingsClass}.Set_{propName}(NativePtr, ptr);");
                    csClean.AppendLine($"                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);");
                    csClean.AppendLine($"            }}");
                    csClean.AppendLine($"        }}");
                }
            }
        }

        void WriteStdVector(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder csClean, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
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
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_Item\", [](void* obj, int index, void* value) {{ (({cppTypeName}*)obj)->{propName}[index] = *(const {cppElemType}*)value; }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Item(IntPtr thisObj, int index);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_Item(IntPtr thisObj, int index, IntPtr value);");

                csClean.AppendLine($"        public int {propName}_Count => {bindingsClass}.Get_{propName}_Count(NativePtr);");
                csClean.AppendLine($"        public {csClassType} Get_{propName}(int index) {{ IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, index); return ptr != IntPtr.Zero ? new {csClassType}(ptr) : null; }}");
                csClean.AppendLine($"        public void Set_{propName}(int index, {csClassType} value) {{ {bindingsClass}.Set_{propName}_Item(NativePtr, index, value?.NativePtr ?? IntPtr.Zero); }}");
                csClean.AppendLine($"        public {csClassType}[] {propName} {{");
                csClean.AppendLine($"            get {{");
                csClean.AppendLine($"                int count = {bindingsClass}.Get_{propName}_Count(NativePtr);");
                csClean.AppendLine($"                {csClassType}[] result = new {csClassType}[count];");
                csClean.AppendLine($"                for (int i = 0; i < count; i++) {{");
                csClean.AppendLine($"                    IntPtr ptr = {bindingsClass}.Get_{propName}_Item(NativePtr, i);");
                csClean.AppendLine($"                    result[i] = ptr != IntPtr.Zero ? new {csClassType}(ptr) : null;");
                csClean.AppendLine($"                }}");
                csClean.AppendLine($"                return result;");
                csClean.AppendLine($"            }}");
                csClean.AppendLine($"            set {{");
                csClean.AppendLine($"                if (value == null) value = new {csClassType}[0];");
                csClean.AppendLine($"                int count = {bindingsClass}.Get_{propName}_Count(NativePtr);");
                csClean.AppendLine($"                for (int i = 0; i < count && i < value.Length; i++)");
                csClean.AppendLine($"                    {bindingsClass}.Set_{propName}_Item(NativePtr, i, value[i]?.NativePtr ?? IntPtr.Zero);");
                csClean.AppendLine($"            }}");
                csClean.AppendLine($"        }}");
            }
            else
            {
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Get_{propName}_Data\", [](void* obj) -> void* {{ return (void*)(({cppTypeName}*)obj)->{propName}.data(); }});");
                cpp.AppendLine($"    mono_add_internal_call(\"{bindingsClass}::Set_{propName}_FromArray\", [](void* obj, void* data, int count) {{ auto& vec = (({cppTypeName}*)obj)->{propName}; vec.assign((const {cppElemType}*)data, (const {cppElemType}*)data + count); }});");

                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static IntPtr Get_{propName}_Data(IntPtr thisObj);");
                cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
                cs.AppendLine($"        internal static void Set_{propName}_FromArray(IntPtr thisObj, IntPtr data, int count);");

                bool marshalDirect = DirectMarshalTypes.Contains(csElemType);

                if (marshalDirect)
                {
                    csClean.AppendLine($"        public {csElemType}[] {propName} {{");
                    csClean.AppendLine($"            get {{ int count = {bindingsClass}.Get_{propName}_Count(NativePtr); IntPtr ptr = {bindingsClass}.Get_{propName}_Data(NativePtr); {csElemType}[] result = new {csElemType}[count]; System.Runtime.InteropServices.Marshal.Copy(ptr, result, 0, count); return result; }}");
                    csClean.AppendLine($"            set {{ if (value == null) value = new {csElemType}[0]; IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(value.Length * System.Runtime.InteropServices.Marshal.SizeOf(typeof({csElemType}))); System.Runtime.InteropServices.Marshal.Copy(value, 0, ptr, value.Length); {bindingsClass}.Set_{propName}_FromArray(NativePtr, ptr, value.Length); System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }}");
                    csClean.AppendLine($"        }}");
                }
                else
                {
                    string marshalHelperType = IntermediaryMarshalType(csElemType);
                    csClean.AppendLine($"        public {csElemType}[] {propName} {{");
                    csClean.AppendLine($"            get {{");
                    csClean.AppendLine($"                int count = {bindingsClass}.Get_{propName}_Count(NativePtr);");
                    csClean.AppendLine($"                IntPtr ptr = {bindingsClass}.Get_{propName}_Data(NativePtr);");
                    csClean.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[count];");
                    csClean.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(ptr, raw, 0, count);");
                    csClean.AppendLine($"                {csElemType}[] result = new {csElemType}[count];");
                    csClean.AppendLine($"                for (int i = 0; i < count; i++) result[i] = ({csElemType})raw[i];");
                    csClean.AppendLine($"                return result;");
                    csClean.AppendLine($"            }}");
                    csClean.AppendLine($"            set {{");
                    csClean.AppendLine($"                if (value == null) value = new {csElemType}[0];");
                    csClean.AppendLine($"                {marshalHelperType}[] raw = new {marshalHelperType}[value.Length];");
                    csClean.AppendLine($"                for (int i = 0; i < value.Length; i++) raw[i] = ({marshalHelperType})value[i];");
                    csClean.AppendLine($"                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(value.Length * System.Runtime.InteropServices.Marshal.SizeOf(typeof({marshalHelperType})));");
                    csClean.AppendLine($"                System.Runtime.InteropServices.Marshal.Copy(raw, 0, ptr, value.Length);");
                    csClean.AppendLine($"                {bindingsClass}.Set_{propName}_FromArray(NativePtr, ptr, value.Length);");
                    csClean.AppendLine($"                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);");
                    csClean.AppendLine($"            }}");
                    csClean.AppendLine($"        }}");
                }
            }
        }

        void WriteStringProperty(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, StringBuilder csClean, CodeScanDB database, string namespaceBind, CodeScanDB.ReflectedType owningType)
        {
            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{owningType.typeName_}_Bindings::Get_{prop.propertyName_}\", [](void* obj) -> MonoString* {{ auto& s = (({owningType.typeName_}*)obj)->{prop.propertyName_}; return mono_string_new(mono_domain_get(), s.c_str()); }});");

            cpp.AppendLine($"    mono_add_internal_call(\"{namespaceBind}.{owningType.typeName_}_Bindings::Set_{prop.propertyName_}\", [](void* obj, MonoString* value) {{ char* utf8 = mono_string_to_utf8(value); (({owningType.typeName_}*)obj)->{prop.propertyName_} = utf8; mono_free(utf8); }});");

            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static string Get_{prop.propertyName_}(IntPtr thisObj);");
            cs.AppendLine("        [MethodImpl(MethodImplOptions.InternalCall)]");
            cs.AppendLine($"        internal static void Set_{prop.propertyName_}(IntPtr thisObj, string value);");

            csClean.Append($"        public string {prop.propertyName_} {{ get {{ return {namespaceBind}.{owningType.typeName_}_Bindings.Get_{prop.propertyName_}(NativePtr); }} ");
            csClean.AppendLine($"set {{ {namespaceBind}.{owningType.typeName_}_Bindings.Set_{prop.propertyName_}(NativePtr, value); }} }}");
        }

        void WriteSharedPtr(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, CodeScanDB database, string namespaceBind)
        {

        }

        void WriteUniquePtr(CodeScanDB.Property prop, StringBuilder cpp, StringBuilder cs, CodeScanDB database, string namespaceBind)
        {

        }
    }
}
