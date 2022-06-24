using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Trait = System.Collections.Generic.KeyValuePair<string,string>;

namespace typegen
{
    /// <summary>
    /// Example generators that work with the CodeScanDB
    /// </summary>
    public class DatabaseGenerators
    {
        public List<string> Headers = new List<string>();

        public void WriteSerialization(StringBuilder sb, ReflectionScanner scan)
        {
            var types = scan.database.FlatTypes.Where(t => t.isPrimitive_ == false && t.isTemplate_ == false && t.isInternal_ == false).ToList();

            WriteEnumTables(sb, types);

            foreach (CodeScanDB.ReflectedType type in types)
            {
                if (type.enumValues_.Count > 0)
                    continue;

                sb.AppendLine($"void {type.typeName_}::Serialize(ISerializer& dest) {{");

                if (type.FirstBase() != null)
                    sb.AppendLine($"    {type.typeName_}::BaseClass::Serialize(dest);");

                foreach (CodeScanDB.Property prop in type.properties_)
                {
                    var propName = prop.propertyName_;
                    var propTypeName = prop.type_.typeName_;

                    if (prop.type_.HasMethod("Serialize"))
                    {
                        if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Pointer))
                        {
                            sb.AppendLine(
$@"    if ({propName}) {{
        dest.Write({propTypeName}->GetType());
        {propName}->Serialize(dest);
    }} else {{
        dest.Write(StringHash(0u));
    }}");
                        }
                        else
                            sb.AppendLine($"    {propName}.Serialize(dest);");
                    }
                    else
                    {
                        // must be supported
                        if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Pointer))
                        {
                            sb.AppendLine(
$@"    if ({propName}) {{ 
        dest.Write({propName}->GetType()); 
        {propName}->Serialize(dest); 
    }} else {{ 
        dest.Write(StringHash(0u)); 
    }}");
                        }
                        else
                            sb.AppendLine($"    dest.Write({prop.propertyName_});");
                    }
                }

                sb.AppendLine("}");

                sb.AppendLine($"void {type.typeName_}::Deserialize(ISource& src) {{");

                if (type.FirstBase() != null)
                    sb.AppendLine($"    {type.typeName_}::BaseClass::Deserialize(src);");

                foreach (CodeScanDB.Property prop in type.properties_)
                {
                    if (prop.type_.HasMethod("Deserialize"))
                    {
                        if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Pointer))
                            sb.AppendLine(
$@"    {{ 
        auto typeHash = src.ReadStringHash(); 
        if (typeHash != 0u) {{ 
            {prop.propertyName_} = Factory::Create<{prop.type_.typeName_}>(typeHash); 
            if ({prop.propertyName_}) {prop.propertyName_}->Serialize(dest); 
        }} 
    }}");
                        else
                            sb.AppendLine($"    {prop.propertyName_}.Deserialize(src);");
                    }
                    else
                    {
                        // must be supported
                        if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Pointer))
                        {
                            sb.AppendLine(
$@"    {{ 
        auto typeHash = src.ReadStringHash(); 
        if (typeHash != 0u) {{ 
            {prop.propertyName_} = Factory::Create<{prop.type_.typeName_}>(typeHash); 
            {prop.propertyName_}->Deserialize(src); 
        }} 
    }}");
                        }
                        else
                            sb.AppendLine($"    src.Read({prop.propertyName_});");
                    }
                }

                sb.AppendLine("}");
            }
        }

        static Dictionary<string, string> asPrimitiveMap = new Dictionary<string, string>
        {
            { "uint32_t", "uint" },
        };
        public void BindScript(StringBuilder sb, ReflectionScanner scan)
        {
            var types = scan.database.DepthSortedTypes().Where(t => t.isPrimitive_ == false && t.isTemplate_ == false && t.isInternal_ == false);
            foreach (CodeScanDB.ReflectedType type in types)
            {
                if (type.enumValues_.Count > 0) // skip enums
                    continue;

                sb.AppendLine($"void Register_{type.typeName_}(asIScriptEngine* engine) {{");

                foreach (CodeScanDB.Method meth in type.methods_)
                {
                    if (meth.bindingData_.HasTrait("noscript"))
                        continue;

                    sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"{meth.AngelscriptSignature()}\", asMETHODPR({type.typeName_}, {meth.methodName_}, {meth.CallSig()}, {meth.ReturnTypeText()}), asCALL_THISCALL);");
                }

                foreach (CodeScanDB.Property prop in type.properties_)
                {
                    if (prop.bindingData_.HasTrait("getter"))
                    {
                        sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"{prop.GetFullTypeName(true).Replace("*", "@+")} get_{prop.propertyName_}() const\", asMETHOD({type.typeName_}, {prop.bindingData_.Get("getter")}), asCALL_THISCALL);");
                        if (prop.bindingData_.HasTrait("setter"))
                            sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"void set_{prop.propertyName_}({prop.GetFullTypeName(true).Replace("*", "@+")})\", asMETHOD({type.typeName_}, {prop.bindingData_.Get("setter")}), asCALL_THISCALL);");
                    }
                    else if (prop.templateParameters_.Count > 0) // it's template type
                    {

                    }
                    else if (prop.type_.isPrimitive_)
                        sb.AppendLine($"    engine->RegisterObjectProperty(\"{type.typeName_}\", \"{prop.GetFullTypeName(true).Replace("*", "@+")} {prop.propertyName_}\", offsetof({type.typeName_}, {prop.propertyName_}));");
                }

                sb.AppendLine("}");
            }

            sb.AppendLine("void RegisterScriptAPI(asIScriptEngine* engine) {");
            foreach (CodeScanDB.ReflectedType type in types)
            {
                var firstbase = type.FirstBase();
                if (type.enumValues_.Count > 0)
                {
                    sb.AppendLine($"    engine->RegisterEnum(\"{type.typeName_}\");");
                    foreach (var val in type.enumValues_)
                        sb.AppendLine($"    engine->RegisterEnumValue(\"{type.typeName_}\", \"{val.Key}\", {val.Value});");
                }
                else if (type.bindingData_.HasTrait("POD"))
                {
                    sb.AppendLine($"    engine->RegisterObjectType(\"{type.typeName_}\", sizeof({type.typeName_}), asOBJ_VALUE | asOBJ_POD);");
                    sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_CONSTRUCT, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ new(tgt) {type.typeName_}(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");
                    //sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName}\", asBEHAVE_DESTRUCT, \"void f()\", asFUNCTIONPR([]({type.typeName}* tgt) {{ delete(tgt) {type.typeName}(); }}, ({type.typeName}*), void), asCALL_CDECL_OBJLAST);");
                }
                else
                {
                    sb.AppendLine($"    engine->RegisterObjectType(\"{type.typeName_}\", 0, asOBJ_REF);");
                    sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_CONSTRUCT, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ new(tgt) {type.typeName_}(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");
                    sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_DESTRUCT, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ ptr->~{type.typeName_}(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");

                    sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_ADDREF, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ tgt->AddRef(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");
                    sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_RELEASE, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ tgt->ReleaseRef(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");

                    if (firstbase != null)
                        sb.AppendLine($"    RegisterSubclass<{type.typeName_}, {firstbase.typeName_}>(engine, \"{type.typeName_}\", \"{firstbase.typeName_}\");");
                }
            }

            foreach (CodeScanDB.ReflectedType type in types)
                sb.AppendLine($"    Register_{type.typeName_}(engine);");
            sb.AppendLine("}");
        }

        readonly Dictionary<string, string> urhoDefaults = new Dictionary<string, string>
        {
            { "bool", "false"},
            { "int", "0"},
            { "float", "0.0f"},
            { "String", "String()"},
            { "VariantVector", "VariantVector()"},
            { "VariantMap", "VariantMap()"},
            { "Color", "Color()"},
        };
        string GetUrhoDefault(CodeScanDB.ReflectedType type)
        {
            string outName = "";
            if (urhoDefaults.TryGetValue(type.typeName_, out outName))
                return outName;
            return type.typeName_ + "()";
        }
        public void BindUrhoAttributes(StringBuilder sb, ReflectionScanner scan)
        {
            foreach (var line in scan.ForwardLines)
                sb.AppendLine(line);

            foreach (var file in scan.ScannedHeaders)
                sb.AppendLine($"#include <{file}>");

            var types = scan.database.DepthSortedTypes().Where(t => t.isPrimitive_ == false && t.isTemplate_ == false && t.isInternal_ == false).ToList();

            WriteEnumTables(sb, types);

            foreach (var type in types)
            {
                if (type.enumValues_.Count > 0)
                    continue;

                var firstBase = type.FirstBase();

                sb.AppendLine($"void {type.typeName_}::RegisterObject(Context* context) {{");
                if (type.bindingData_.HasTrait("category"))
                    sb.AppendLine($"    context->RegisterObjectFactory<{type.typeName_}>(\"{type.bindingData_.Get("category")}\");");
                else
                    sb.AppendLine($"    context->RegisterObjectFactory<{type.typeName_}>();");

                if (firstBase != null && type.bindingData_.GetBool("copybase_attributes", false))
                    sb.AppendLine($"    URHO3D_COPY_BASE_ATTRIBUTES({firstBase.typeName_});");

                foreach (var prop in type.properties_)
                {
                    var propType = prop.type_;
                    if (prop.bindingData_.HasTrait("getter"))
                    {
                        if (prop.bindingData_.HasTrait("setter"))
                        {
                            if (prop.type_.enumValues_.Count > 0)
                                sb.AppendLine($"    URHO3D_ENUM_ACCESSOR_ATTRIBUTE(\"{prop.bindingData_.Get("display", prop.propertyName_)}\", {prop.bindingData_.Get("getter")}, {prop.bindingData_.Get("setter")}, {prop.GetFullTypeName(false)}, {propType.typeName_}_Names, {prop.bindingData_.Get("default", GetUrhoDefault(prop.type_))}, {prop.bindingData_.Get("flags", "AM_DEFAULT")});");
                            else
                                sb.AppendLine($"    URHO3D_ACCESSOR_ATTRIBUTE(\"{prop.bindingData_.Get("display", prop.propertyName_)}\", {prop.bindingData_.Get("getter")}, {prop.bindingData_.Get("setter")}, {prop.GetFullTypeName(false)}, {prop.bindingData_.Get("default", GetUrhoDefault(prop.type_))}, {prop.bindingData_.Get("flags", "AM_DEFAULT")});");
                        }
                    }
                    else
                    {
                        // standard offsetof attribute
                        if (prop.type_.enumValues_.Count > 0)
                            sb.AppendLine($"    URHO3D_ENUM_ATTRIBUTE(\"{prop.bindingData_.Get("display", prop.propertyName_)}\", {prop.propertyName_}, {propType.typeName_}_Names, {prop.bindingData_.Get("default", GetUrhoDefault(prop.type_))}, {prop.bindingData_.Get("flags", "AM_DEFAULT")});");
                        else
                            sb.AppendLine($"    URHO3D_ATTRIBUTE(\"{prop.bindingData_.Get("display", prop.propertyName_)}\", {prop.GetFullTypeName(false)}, {prop.propertyName_}, {prop.bindingData_.Get("default", GetUrhoDefault(prop.type_))}, {prop.bindingData_.Get("flags", "AM_DEFAULT")});");
                    }
                }

                sb.AppendLine("}"); // for -> void MY_TYPE::RegisterObject(Context*) {
            }

            sb.AppendLine("void Register_Auto_API(Context* context) {");
            foreach (var type in types)
            {
                if (type.enumValues_.Count > 0)
                    continue;
                sb.AppendLine($"    {type.typeName_}::RegisterObject(context);");
            }
            sb.AppendLine("}");
        }

        public void WriteEnumTables(StringBuilder sb, List<CodeScanDB.ReflectedType> types)
        {
            sb.AppendLine("/// Autogenerated Enum Tables BEGIN");
            foreach (var type in types)
            {
                if (type.enumValues_.Count > 0)
                {
                    // string list
                    sb.AppendLine($"static const char* {type.typeName_}_Names[] = {{");
                    foreach (var key in type.enumValues_)
                        sb.AppendLine($"    \"{key.Key}\",");
                    sb.AppendLine("    nullptr");
                    sb.AppendLine("};");

                    // string -> value
                    sb.AppendLine($"static const HashMap<String, int> {type.typeName_}_LUT = {{");
                    foreach (var key in type.enumValues_)
                        sb.AppendLine($"    {{ \"{key.Key}\", {key.Value} }},");
                    sb.AppendLine("};");

                    // string -> index
                    sb.AppendLine($"static const HashMap<String, int> {type.typeName_}_IDX = {{");
                    for (int i = 0; i < type.enumValues_.Count; ++i)
                        sb.AppendLine($"    {{ \"{type.enumValues_[i].Key}\", {i} }},");
                    sb.AppendLine("};");

                    // value -> index
                    sb.AppendLine($"static const HashMap<{type.typeName_}, int> {type.typeName_}_VAL_TO_IDX = {{");
                    for (int i = 0; i < type.enumValues_.Count; ++i)
                        sb.AppendLine($"    {{ {type.enumValues_[i].Key}, {i} }},");
                    sb.AppendLine("};");
                }
            }
            sb.AppendLine("/// Autogenerated Enum Tables END");
        }

        public void WriteTaggedData(StringBuilder sb, ReflectionScanner scan)
        {
            var types = scan.database.DepthSortedTypes().Where(t => !t.isPrimitive_ && !t.isTemplate_ && !t.isInternal_).ToList();

            sb.AppendLine();
            sb.AppendLine("<fieldict.h>");
            sb.AppendLine();

            foreach (var type in types)
            {
                if (type.enumValues_.Count > 0)
                    continue;

                var baseType = type.FirstBase();

                sb.AppendLine("/// Autogenerated code: get values from tagged data");
                sb.AppendLine($"void {type.typeName_}::GetData(TaggedData& dest) {{");
                if (baseType != null)
                    sb.AppendLine($"    {baseType.typeName_}::GetData(dest);");
                foreach (var prop in type.properties_)
                {
                    string tagID = prop.bindingData_.Get("tag");
                    string propName = prop.propertyName_;

                    if (prop.type_.isPrimitive_ || prop.type_.isInternal_)
                        sb.AppendLine($"    dest.Set({tagID}, {propName});");
                    else
                    {
                        if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Pointer))
                        {
                            sb.AppendLine(
$@"    if ({propName}) {{ 
        TaggedData t; t.Set(ttClassID, {propName}->GetTypeID()); 
        {propName}->GetData(t); 
        dest.SetTaggedData({tagID}, t); 
    }}");
                        }
                        else
                        {
                            sb.AppendLine(
$@"    {{ 
        TaggedData t; t.Set(ttClassID, {prop.type_.typeName_}::StaticTypeID()); 
        {propName}.GetData(t); 
        dest.SetTaggedData({tagID}, t); 
    }}");
                        }
                    }
                }
                sb.AppendLine("}");

                sb.AppendLine("/// Autogenerated code: bulk value setting from tagged data");
                sb.AppendLine($"void {type.typeName_}::SetData(const TaggedData& src) {{");
                if (baseType != null)
                    sb.AppendLine($"    {baseType.typeName_}::SetData(src);");
                foreach (var prop in type.properties_)
                {
                    string tagID = prop.bindingData_.Get("tag");
                    string propName = prop.propertyName_;
                    string propType = prop.type_.typeName_;

                    if (prop.type_.isPrimitive_ || prop.type_.isInternal_)
                        sb.AppendLine($"    src.Get({tagID}, {propName});");
                    else
                    {
                        if (prop.accessModifiers_.HasFlag(AccessModifiers.AM_Pointer))
                            sb.AppendLine(
$@"    if (src.HasField({tagID}) {{ 
        TaggedData t = src.GetTaggedData({tagID}); 
        StringHash typeID = t.GetStringHash(ttClassID); 
        if ({propName} && {propName}->GetType() != typeID) {{ delete {propName}; {propName} = nullptr; }} 
        if ({propName} == nullptr && typeID != 0u) {{ {propName} = Factory::Create<{propType}>(typeID); }} 
        if ({propName}) {{ {propName}->SetData(t); }}
    }}");
                    }
                }
                sb.AppendLine("}");

                sb.AppendLine("/// Autogenerated code: field getter by tag");
                sb.AppendLine($"bool {type.typeName_}::GetField(FieldTag aTag, Variant& data) {{");
                if (baseType != null)
                    sb.AppendLine($"    if ({baseType.typeName_}::GetField(aTag, data)) return true;");
                sb.AppendLine("    switch (aTag) {");
                foreach (var prop in type.properties_)
                {
                    sb.AppendLine($"    case {prop.bindingData_.Get("tag")}: {{");
                    if (prop.type_.isPrimitive_ || prop.type_.isInternal_)
                        sb.AppendLine($"        data.Set({prop.propertyName_});"); // DirectTransfer<T>(T& target)
                    else if (prop.type_.enumValues_.Count > 0)
                        sb.AppendLine($"        data.Set((int){prop.propertyName_});");
                    sb.AppendLine("        return true;");
                    sb.AppendLine("    }");
                }
                sb.AppendLine("    }");
                sb.AppendLine("    return false;");
                sb.AppendLine("}");

                sb.AppendLine("/// Autogenerated code: field setter by tag");
                sb.AppendLine($"bool {type.typeName_}::SetField(FieldTag aTag, const Variant& data) {{");
                if (baseType != null)
                    sb.AppendLine($"    if ({baseType.typeName_}::SetField(aTag, data)) return true;");
                sb.AppendLine("    switch (aTag) {");
                foreach (var prop in type.properties_)
                {
                    sb.AppendLine($"    case {prop.bindingData_.Get("tag")}: {{");
                    if (prop.type_.isPrimitive_ || prop.type_.isInternal_)
                        sb.AppendLine($"        data.DirectTransfer({prop.propertyName_});"); // DirectTransfer<T>(T& target)
                    else if (prop.type_.enumValues_.Count > 0)
                        sb.AppendLine($"        {prop.propertyName_} = ({prop.GetFullTypeName(false)})data.GetInt();");
                    sb.AppendLine("        return true;");
                    sb.AppendLine("    }");
                }
                sb.AppendLine("    }");
                sb.AppendLine("    return false;");
                sb.AppendLine("}");
            }
        }

        public void BindVariantCalls(StringBuilder sb, CodeScanDB database)
        {
            var voidType = database.types_["void"];

            foreach (var method in database.globalFunctions_)
            {
                int i = 0;
                sb.AppendLine($"    RegisterCall(\"{method.methodName_}\", [](void* object, VariantVector& args) -> Variant {{");
                if (method.returnType_.type_ != voidType)
                {
                    sb.AppendLine($"        Variant retVal;");
                    sb.AppendLine($"        retVal = ");
                }
                sb.Append($"{method.methodName_}(");
                foreach (var arg in method.argumentTypes_)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append($"args[{i}].Get<{arg.type_.typeName_}>()");
                    ++i;
                }
                sb.AppendLine(");");
                if (method.returnType_.type_ == voidType)
                    sb.AppendLine("        return Variant();");
                else
                    sb.AppendLine("        return retVal;");
                sb.AppendLine("    });");
            }

            foreach (var type in database.types_)
            {
                if (type.Value.HasAnyFunctions())
                {
                    sb.AppendLine("    {");
                    sb.AppendLine($"        StringHash typeName(\"{type.Value.typeName_}\");");
                    foreach (var method in type.Value.methods_)
                    {
                        sb.AppendLine($"        RegisterCall(typeName, \"{method.methodName_}\", [](void* object, VariantVector& args) -> Variant {{");
                        if (method.returnType_.type_ != voidType)
                        {
                            sb.AppendLine($"            Variant retVal;");
                            sb.Append($"            retVal = ");
                        }

                        sb.Append($"(({type.Value.typeName_}*)obj)->{method.methodName_}(");
                        int i = 0;
                        foreach (var arg in method.argumentTypes_)
                        {
                            if (i > 0)
                                sb.Append(", ");
                            sb.Append($"args[{i}].Get<{arg.type_.typeName_}>()");
                            ++i;
                        }
                        sb.AppendLine(");");
                        if (method.returnType_.type_ == voidType)
                            sb.AppendLine("            return Variant();");
                        else
                            sb.AppendLine("            return ret;");

                        sb.AppendLine($"        }});");
                    }
                    sb.AppendLine("    }");
                }
            }
        }

        public void WriteDataFields(StringBuilder sb, CodeScanDB database)
        {
            var types = database.FlatTypes.Where(t => t.isPrimitive_ == false && t.isTemplate_ == false && t.isInternal_ == false);

            sb.AppendLine();
            sb.AppendLine("<fieldict.h>");
            sb.AppendLine();
            sb.AppendLine("void RegisterDataFields() {");

            foreach (var type in types)
            {
                sb.AppendLine("  {");
                sb.AppendLine("    auto fieldList = std::make_shared<DataFieldInfoList>()");
                sb.AppendLine("    std::shared_ptr<DataFieldInfo> fieldInfo;");
                sb.AppendLine();
                foreach (var fld in type.properties_)
                {
                    string tagID = fld.bindingData_.Get("tag");
                    sb.AppendLine($"    fieldInfo = fieldList->CreateFieldInfo(\"{fld.propertyName_}\", {tagID}, FieldType<{fld.GetFullTypeName(false)}>::Value);");
                    if (fld.bindingData_.GetBool("readonly", false))
                        sb.AppendLine("    fieldInfo->SetReadOnly(true);");
                    if (fld.bindingData_.GetBool("multiline", false))
                        sb.AppendLine("    fieldInfo->SetMultiline(true);");
                    if (fld.bindingData_.HasTrait("min"))
                        sb.AppendLine($"    fieldInfo->SetMinValue({fld.bindingData_.Get("min")});");
                    if (fld.bindingData_.HasTrait("max"))
                        sb.AppendLine($"    fieldInfo->SetMaxValue({fld.bindingData_.Get("max")});");
                    if (fld.bindingData_.HasTrait("step"))
                        sb.AppendLine($"    fieldInfo->SetStepValue({fld.bindingData_.Get("step")});");
                    if (fld.type_.enumValues_.Count > 0)
                    {
                        foreach (var value in fld.type_.enumValues_)
                            sb.AppendLine($"    fieldInfo->AddChoice(\"{value.Key}\", {value.Value});");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"    RegisterFieldList(\"{type.typeName_}\", fieldList);");
                sb.AppendLine("  }");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        public void WriteVariantMath(StringBuilder sb, CodeScanDB database)
        {
            var types = database.FlatTypes.Where(t => t.isInternal_ == true && t.isVector_ == true);

            string[] operators = new string[] { "+", "-", "*", "/"};
            string[] calls = new string[] { "Add", "Sub", "Mul", "Div" };

            for (int m = 0; m < operators.Length; ++m)
            { 
                sb.AppendLine($"Variant Variant_{calls[m]}(const Variant& lhs, const Variant& rhs) {{");
                foreach (var t in types)
                {
                    sb.AppendLine($"    if (lhs.GetType() == GetVariantType<{t.typeName_}>()) {{");
                    sb.AppendLine($"        auto lhsVal = rhs.Get<{t.typeName_}>();");
                    sb.AppendLine(
$@"        if (rhs.GetType() == VAR_FLOAT || rhs.GetType() == VAR_INT || rhs.GetType() == VAR_BOOL || rhs.GetType() == VAR_DOUBLE) {{
            return lhsVal * rhs.GetFloatSafe();
        }}");
                    foreach (var o in types)
                    {
                        if (o == t)
                            continue;
                        sb.AppendLine($"        if (rhs.GetType() == GetVariantType<{o.typeName_}>()) {{");
                        sb.AppendLine($"            auto rhsVal = rhs.Get<{o.typeName_}>();");
                        sb.Append($"            return {t.typeName_}(");
                        for (int i = 0; i < t.properties_.Count; ++i)
                        {
                            if (i > 0)
                                sb.Append(", ");
                            if (i < o.properties_.Count)
                                sb.Append($"lhsVal.{t.properties_[i].propertyName_} {operators[m]} rhsVal.{o.properties_[i].propertyName_}");
                            else
                                sb.Append($"lhsVal.{t.properties_[i].propertyName_}");
                        }
                        sb.AppendLine(");");
                        sb.AppendLine($"        }}");
                    }
                    sb.AppendLine($"    }}");
                }
                sb.AppendLine("    return Variant();");
                sb.AppendLine($"}}");
                sb.AppendLine();
            }
        }

        public void WriteXMacros(StringBuilder sb, CodeScanDB database)
        {
            sb.AppendLine("/// =============================");
            sb.AppendLine("/// AUTOGENERATED - DO NOT MODIFY");
            sb.AppendLine("/// =============================");
            sb.AppendLine("");
            sb.AppendLine("/// ==============================");
            sb.AppendLine("/// GENERATED ENUMERATION X MACROS");
            sb.AppendLine("/// ==============================");
            sb.AppendLine("");
            foreach (var e in database.types_)
            {
                if (e.Value.isInternal_ || e.Value.isPrimitive_)
                    continue;
                if (e.Value.IsEnum)
                {
                    CodeScanDB.ReflectedType type = e.Value;
                    sb.AppendLine($"#define _x_{type.typeName_}_values \\");
                    foreach (var val in type.enumValues_)
                        sb.AppendLine($"    X({val.Key}, {val.Value}) \\");
                    sb.AppendLine("");
                }
            }

            sb.AppendLine("/// ==============================");
            sb.AppendLine("/// GENERATED PROPERTIES X MACROS");
            sb.AppendLine("/// ==============================");
            sb.AppendLine("");
            foreach (var t in database.types_)
            {
                if (t.Value.IsEnum || t.Value.isInternal_ || t.Value.isPrimitive_)
                    continue;

                CodeScanDB.ReflectedType type = t.Value;
                sb.AppendLine($"#define _x_{type.typeName_}_properties \\");
                foreach (var p in type.properties_)
                { 
                    if (p.accessModifiers_.HasFlag(AccessModifiers.AM_Virtual))
                    {
                        // Getter / Setter
                        if (!string.IsNullOrEmpty(p.bindingData_.Get("set")))
                            sb.AppendLine($"    V({p.GetFullTypeName(false)}, {p.propertyName_}, \"{p.propertyName_}\", {p.bindingData_.Get("get")}, {p.bindingData_.Get("set")}) \\");
                        else // Getter only == read-only
                            sb.AppendLine($"    RO({p.GetFullTypeName(false)}, {p.propertyName_}, \"{p.propertyName_}\", {p.bindingData_.Get("get")}) \\");
                    }
                    else // direct access
                        sb.AppendLine($"    X({p.GetFullTypeName(false)}, {p.propertyName_}, \"{p.propertyName_}\") \\");
                }
                sb.AppendLine("");
            }
        }
    }

    public static class GenExt
    {
        public static bool IsList(this CodeScanDB.Property prop, DatabaseGenerators gen)
        {
            if (prop.type_.typeName_ == "std::vector" || prop.type_.typeName_ == "std::array" || prop.type_.typeName_ == "ResourceRefList")
                return true;
            return false;
        }
    }
}
