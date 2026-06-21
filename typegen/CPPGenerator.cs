using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class CPPGenerator
    {
        public static void Write(List<Type> types, string output)
        {
            if (types.Count == 0)
            {
                Console.WriteLine("No types found to process");
                return;
            }

            if (string.IsNullOrEmpty(output))
                output = "generated";

            StringBuilder header = new StringBuilder();
            StringBuilder source = new StringBuilder();

            CPPCommon.WriteHeaders(header, types);

            source.AppendLine($"#include \"{output + ".h"}\"");

        // open namespace, first type wins the day
            header.AppendLine($"\r\nnamespace {types[0].Namespace} {{\r\n");
            source.AppendLine($"\r\nnamespace {types[0].Namespace} {{\r\n");

        // Write the upper enum value tables
            foreach (Type t in types)
            {
                if (t.IsEnum)
                {
                    WriteEnumTables(source, t);
                    source.AppendLine("");
                }
            }

            for (int i = 0; i < types.Count; ++i)
            {
                Type t = types[i];

                // if we have a backing native type then we do not require anything here.
                if (t.GetCustomAttribute<GenAttr.NativeTypeAttribute>() != null)
                    continue;

                if (t.IsEnum)
                {
                    header.AppendLine($"enum {t.Name} {{");
                    string[] names = t.GetEnumNames();
                    var values = t.GetEnumValues();
                    for (int v = 0; v < names.Length; ++v)
                    {
                        var trueValue = System.Convert.ChangeType(values.GetValue(v), t.GetEnumUnderlyingType());
                        header.AppendLine($"    {names[v]} = {trueValue};");
                    }
                    header.AppendLine("};\r\n");
                }
                else if (t.IsStructOrClass())
                {
                    header.AppendLine(CPPCommon.GetPrintedTypeName(t) + " {");

                    header.AppendLine("public:");

                    FieldInfo[] flds = t.GetFields();
                    foreach (FieldInfo fld in flds)
                    {
                        if (fld.DeclaringType != t)
                            continue;

                        CPPCommon.CheckComment(header, fld);
                        if (fld.FieldType.IsArray && CPPCommon.IsFlatArray(fld.FieldType, fld))
                        {
                            int arraySize = fld.GetCustomAttribute<GenAttr.FixedLengthAttribute>().length;
                            header.AppendLine($"    {CPPCommon.GetMemberTypeName(fld.FieldType, fld)} {fld.Name}[{arraySize}];");
                        }
                        else
                            header.AppendLine($"    {CPPCommon.GetMemberTypeName(fld.FieldType, fld)} {fld.Name};");
                    }

                    header.AppendLine($"\r\n    {t.Name}();");
                    header.AppendLine("};\r\n");


                // Constructor
                    CPPCommon.WriteConstructor(source, t);

                // Serializers
                    CPPGenSerial.WriteSerializationSource(source, t);

                // ImGui
                    CPPDearImgui.GenerateImguiSource(source, t, flds);
                }
                else
                {
                    throw new Exception($"Unexpected type encountered, was looking for an enum, struct, or class, {t.Name}");
                }
            }

        // close namespace and extra-line for Git's whining
            header.AppendLine("}\r\n");
            source.AppendLine("}\r\n");

            System.IO.File.WriteAllText(output + ".h", header.ToString());
            System.IO.File.WriteAllText(output + ".cpp", source.ToString());
        }

        static void WriteEnumTables(StringBuilder source, Type enumType)
        {
            var names = enumType.GetEnumNames();
            string list = "";
            string nameList = "";
            foreach (var s in names)
            {
                if (list.Length > 0)
                {
                    list += ", ";
                    nameList += ", ";
                }
                nameList += "\"" + s + "\"";
                list += s;
            }

            source.AppendLine($"static const {enumType.Name} _table_{enumType.Name}[] = {{ {list} }};");
            source.AppendLine($"static const const char* _names_{enumType.Name}[] = {{ {nameList} }};");
        }
    }
}
