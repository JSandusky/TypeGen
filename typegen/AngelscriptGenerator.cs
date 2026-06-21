using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Trait = System.Collections.Generic.KeyValuePair<string,string>;

namespace typegen
{
    public class AngelscriptGenerator
    {
        public void BindScript(StringBuilder sb, ReflectionScanner scan)
        {
            foreach (var line in scan.ForwardLines)
                sb.AppendLine(line);

            foreach (var file in scan.ScannedHeaders)
                sb.AppendLine($"#include <{file}>");

            var types = scan.database.DepthSortedTypes().Where(t => t.isPrimitive_ == false && t.isTemplate_ == false && t.isInternal_ == false).ToList();
            foreach (CodeScanDB.ReflectedType type in types)
            {
                if (type.enumValues_.Count > 0) // skip enums
                    continue;

                sb.AppendLine($"void Register_{type.typeName_}(asIScriptEngine* engine) {{");

                foreach (CodeScanDB.Method meth in type.methods_)
                {
                    if (meth.bindingData_.HasTrait("noscript"))
                        continue;

                    var asRet = ToAngelscriptType(meth.returnType_);
                    var asArgs = string.Join(", ", meth.argumentTypes_.Select(a => ToAngelscriptType(a, true)));
                    var asSig = $"{asRet} {meth.methodName_}({asArgs})";
                    if (meth.accessModifiers_.HasFlag(AccessModifiers.AM_Const))
                        asSig += " const";
                    sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"{asSig}\", asMETHODPR({type.typeName_}, {meth.methodName_}, {meth.CallSig()}, {meth.ReturnTypeText()}), asCALL_THISCALL);");
                }

                foreach (CodeScanDB.Property prop in type.properties_)
                {
                    if (prop.bindingData_.HasTrait("getter"))
                    {
                        sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"{ToAngelscriptType(prop)} get_{prop.propertyName_}() const\", asMETHOD({type.typeName_}, {prop.bindingData_.Get("getter")}), asCALL_THISCALL);");
                        if (prop.bindingData_.HasTrait("setter"))
                            sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"void set_{prop.propertyName_}({ToAngelscriptType(prop, true)})\", asMETHOD({type.typeName_}, {prop.bindingData_.Get("setter")}), asCALL_THISCALL);");
                    }
                    else if (prop.type_.typeName_ == "std::string")
                        sb.AppendLine($"    engine->RegisterObjectProperty(\"{type.typeName_}\", \"{ToAngelscriptType(prop)} {prop.propertyName_}\", offsetof({type.typeName_}, {prop.propertyName_}));");
                    else if (prop.type_.typeName_ == "std::vector" && prop.templateParameters_.Count > 0)
                    {
                        // auto-generated getter/setter converts std::vector<T> via CScriptArray
                        var elemCppType = prop.templateParameters_[0].Type.GetFullTypeName(true);
                        var asArrType = ToAngelscriptType(prop);

                        sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"{asArrType} get_{prop.propertyName_}() const\", asFUNCTIONPR([](const {type.typeName_}* self) -> CScriptArray* {{");
                        sb.AppendLine($"        asIScriptContext* ctx = asGetActiveContext();");
                        sb.AppendLine($"        asIScriptEngine* engine = ctx->GetEngine();");
                        sb.AppendLine($"        asITypeInfo* ti = engine->GetTypeInfoByDecl(\"{asArrType}\");");
                        sb.AppendLine($"        CScriptArray* arr = CScriptArray::Create(ti, self->{prop.propertyName_}.size());");
                        sb.AppendLine($"        for (asUINT i = 0; i < self->{prop.propertyName_}.size(); i++)");
                        sb.AppendLine($"            *({elemCppType}*)arr->At(i) = self->{prop.propertyName_}[i];");
                        sb.AppendLine($"        return arr;");
                        sb.AppendLine($"    }}, (const {type.typeName_}*), CScriptArray*), asCALL_CDECL_OBJFIRST);");

                        sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"void set_{prop.propertyName_}({asArrType})\", asFUNCTIONPR([]({type.typeName_}* self, CScriptArray* arr) {{");
                        sb.AppendLine($"        self->{prop.propertyName_}.resize(arr->GetSize());");
                        sb.AppendLine($"        for (asUINT i = 0; i < arr->GetSize(); i++)");
                        sb.AppendLine($"            self->{prop.propertyName_}[i] = *({elemCppType}*)arr->At(i);");
                        sb.AppendLine($"    }}, ({type.typeName_}*, CScriptArray*), void), asCALL_CDECL_OBJFIRST);");
                    }
                    else if (prop.type_.typeName_ == "std::map" && prop.templateParameters_.Count >= 2)
                    {
                        // auto-generated getter/setter converts std::map<string, V> via CScriptDictionary
                        var keyProp = prop.templateParameters_[0].Type;
                        var valProp = prop.templateParameters_[1].Type;
                        if (keyProp.type_.typeName_ == "std::string")
                        {
                            var valAsType = ToAngelscriptType(valProp);
                            var valCppType = valProp.GetFullTypeName(true);
                            var asDictType = $"dictionary<string, {valAsType}>@+";

                            sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"{asDictType} get_{prop.propertyName_}() const\", asFUNCTIONPR([](const {type.typeName_}* self) -> CScriptDictionary* {{");
                            sb.AppendLine($"        asIScriptContext* ctx = asGetActiveContext();");
                            sb.AppendLine($"        asIScriptEngine* engine = ctx->GetEngine();");
                            sb.AppendLine($"        int valTypeId = engine->GetTypeIdByDecl(\"{valAsType}\");");
                            sb.AppendLine($"        CScriptDictionary* dict = CScriptDictionary::Create(engine);");
                            sb.AppendLine($"        for (auto& kvp : self->{prop.propertyName_})");
                            sb.AppendLine($"            dict->Set(kvp.first.c_str(), &kvp.second, valTypeId);");
                            sb.AppendLine($"        return dict;");
                            sb.AppendLine($"    }}, (const {type.typeName_}*), CScriptDictionary*), asCALL_CDECL_OBJFIRST);");

                            sb.AppendLine($"    engine->RegisterObjectMethod(\"{type.typeName_}\", \"void set_{prop.propertyName_}({asDictType})\", asFUNCTIONPR([]({type.typeName_}* self, CScriptDictionary* dict) {{");
                            sb.AppendLine($"        asIScriptContext* ctx = asGetActiveContext();");
                            sb.AppendLine($"        asIScriptEngine* engine = ctx->GetEngine();");
                            sb.AppendLine($"        int valTypeId = engine->GetTypeIdByDecl(\"{valAsType}\");");
                            sb.AppendLine($"        CScriptArray* keys = dict->GetKeys();");
                            sb.AppendLine($"        self->{prop.propertyName_}.clear();");
                            sb.AppendLine($"        for (asUINT i = 0; i < keys->GetSize(); i++)");
                            sb.AppendLine($"        {{");
                            sb.AppendLine($"            string key = *(string*)keys->At(i);");
                            sb.AppendLine($"            {valCppType} val;");
                            sb.AppendLine($"            dict->Get(key.c_str(), &val, valTypeId);");
                            sb.AppendLine($"            self->{prop.propertyName_}[key] = val;");
                            sb.AppendLine($"        }}");
                            sb.AppendLine($"    }}, ({type.typeName_}*, CScriptDictionary*), void), asCALL_CDECL_OBJFIRST);");
                        }
                    }
                    else if (prop.templateParameters_.Count > 0)
                    {
                        // Unhandled template type — requires manual binding
                    }
                    else if (prop.type_.isPrimitive_)
                        sb.AppendLine($"    engine->RegisterObjectProperty(\"{type.typeName_}\", \"{ToAngelscriptType(prop)} {prop.propertyName_}\", offsetof({type.typeName_}, {prop.propertyName_}));");
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
                }
                else
                {
                    sb.AppendLine($"    engine->RegisterObjectType(\"{type.typeName_}\", 0, asOBJ_REF);");
                    // asBEHAVE_CONSTRUCT/DESTRUCT are for value types only, not asOBJ_REF
                    //sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_CONSTRUCT, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ new(tgt) {type.typeName_}(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");
                    //sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_DESTRUCT, \"void f()\", asFUNCTIONPR([]({type.typeName_}* tgt) {{ tgt->~{type.typeName_}(); }}, ({type.typeName_}*), void), asCALL_CDECL_OBJLAST);");
                    sb.AppendLine($"    engine->RegisterObjectBehaviour(\"{type.typeName_}\", asBEHAVE_FACTORY, \"{type.typeName_}@ f()\", asFUNCTIONPR([]() -> {type.typeName_}* {{ return new {type.typeName_}(); }}, (), {type.typeName_}*), asCALL_CDECL);");

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
        static string ToAngelscriptType(CodeScanDB.Property prop, bool isParam = false)
        {
            var typeName = prop.type_.typeName_;

            if (typeName == "std::string")
                return isParam ? "const string&in" : "string";

            if (typeName == "std::vector" && prop.templateParameters_.Count > 0)
                return $"array<{ToAngelscriptType(prop.templateParameters_[0].Type)}>@+";

            return prop.GetFullTypeName(true).Replace("*", "@+");
        }
    }
}
