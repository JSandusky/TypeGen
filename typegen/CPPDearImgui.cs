using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    /// <summary>
    /// Generates ImGui rendering code for the types.
    /// </summary>
    public static class CPPDearImgui
    {
        public static void GenerateImguiSource(StringBuilder source, Type t, FieldInfo[] flds)
        {
            source.AppendLine($"void {t.Name}::OnGUI() {{");
            foreach (FieldInfo fld in flds)
            {
                string fldTypeName = fld.FieldType.Name;

                if (!fld.FieldType.IsArray && fld.FieldType.IsPrimitive)
                {
                    CPPDearImgui.WritePrimitive(source, fld.FieldType, fld.Name, $"&{fld.Name}", 1, fld);
                }
                else if (fld.FieldType.IsEnum)
                {
                    CPPDearImgui.WriteEnum(source, fldTypeName, fld);
                }
                else if (fld.FieldType.IsArray)
                {
                    source.AppendLine($"    if (ImGui::CollapsingHeader(\"{fld.Name}\")) {{");
                    source.AppendLine($"        ImGui::Indent();");

                    if (CPPCommon.IsFlatArray(fld.FieldType, fld))
                    {
                        int arrayLen = fld.GetCustomAttribute<GenAttr.FixedLengthAttribute>().length;
                        for (int a = 0; a < arrayLen; ++a)
                        {
                            if (fld.FieldType.GetElementType().IsPrimitive)
                                CPPDearImgui.WritePrimitive(source, fld.FieldType.GetElementType(), $"{fld.Name} [{a}]", $"{fld.Name} + {a}", 2, fld);
                            else
                                CPPDearImgui.WriteStructOrClass(source, fld, fld.FieldType.GetElementType(), $"{fld.Name} [{a}]", $"*({fld.Name} + {a})", 2);
                        }
                    }
                    else
                    {
                        source.AppendLine($"        if (ImGui::Button(\"Add\")) {fld.Name}.push_back({{ }});");
                        source.AppendLine("        ImGui::SameLine();");
                        source.AppendLine($"        if (ImGui::Button(\"Clear\")) {fld.Name}.clear();");

                        source.AppendLine($"        for (size_t i = 0; i < {fld.Name}.size(); ++i) {{");

                        if (fld.FieldType.GetElementType().IsPrimitive)
                            CPPDearImgui.WritePrimitive(source, fld.FieldType.GetElementType(), fld.Name, $"{fld.Name}.data() + i", 3, fld);
                        else
                            CPPDearImgui.WriteStructOrClass(source, fld, fld.FieldType.GetElementType(), fld.Name, $"*({fld.Name}.data() + i)", 3);

                        source.AppendLine("        }");
                        source.AppendLine($"        if (ImGui::Button(\"Add\")) {fld.Name}.push_back({{ }});");
                    }
                    source.AppendLine($"        ImGui::Unindent();");
                    source.AppendLine($"    }}");
                }
                else if (fld.FieldType.IsStructOrClass())
                {
                    CPPDearImgui.WriteStructOrClass(source, fld, fld.FieldType, fld.Name, fld.Name, 1);
                }
            }

            var methods = t.GetMethods();
            foreach (var m in methods)
            {
                if (m.GetCustomAttribute<GenAttr.ComputedValueAttribute>() != null)
                    source.AppendLine($"    ImGui::Text(to_string({m.Name}()));");
            }

            source.AppendLine("}\r\n");
        }

        static void WriteEnum(StringBuilder source, string fldTypeName, FieldInfo fld)
        {
            source.AppendLine($"    if (ImGui::BeginCombo(\"{fld.Name}\", 0, 0)) {{");

            var names = fld.FieldType.GetEnumNames();
            source.AppendLine($"        for (int i = 0; i < {fld.FieldType.EnumIterations()}; ++i) {{");
            source.AppendLine($"            bool isSelected = {fld.Name} == _table_{fldTypeName}[i];");
            source.AppendLine($"            if (ImGui::Selectable(_names_{fldTypeName}[i], &isSelected))");
            source.AppendLine($"                {fld.Name} = _table_{fldTypeName}[i];");
            source.AppendLine("        }");
            source.AppendLine("        ImGui::EndCombo();");
            source.AppendLine("    }");
        }

        static void WritePrimitive(StringBuilder source, Type primType, string name, string pointer, int level, FieldInfo fld)
        {
            if (primType == typeof(byte))
            {
                var flagsSrc = fld.GetCustomAttribute<GenAttr.BitflagsAttribute>();
                if (flagsSrc != null)
                    source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::Bitmask(\"{name}\", {pointer}, __);");
                else
                    source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragByte(\"{name}\", {pointer});");
            }
            if (primType == typeof(short))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragShort(\"{name}\", {pointer});");
            else if (primType == typeof(ushort))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragUShort(\"{name}\", {pointer});");
            else if (primType == typeof(int))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragInt(\"{name}\", {pointer});");
            else if (primType == typeof(uint))
            {
                var flagsSrc = fld.GetCustomAttribute<GenAttr.BitflagsAttribute>();
                if (flagsSrc != null)
                    source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::Bitmask(\"{name}\", {pointer}, __);");
                else
                    source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragUInt(\"{name}\", {pointer});");
            }
            else if (primType == typeof(long))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragLong(\"{name}\", {pointer});");
            else if (primType == typeof(ulong))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragULong(\"{name}\", {pointer});");
            else if (primType == typeof(float))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragFloat(\"{name}\", {pointer});");
            else if (primType == typeof(double))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::DragDouble(\"{name}\", {pointer});");
            else if (primType == typeof(string))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::EditSTLString(\"{name}\", {pointer});");
            else if (primType == typeof(bool))
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::Checkbox(\"{name}\", {pointer});");
        }

        static void WriteStructOrClass(StringBuilder source, FieldInfo fld, Type type, string name, string pointer, int level)
        {
            if (type.GetCustomAttribute<GenAttr.NativeTypeAttribute>() != null)
                source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::CustomWidget(\"{name}\", {pointer});");
            else
            {
                if (fld.GetCustomAttribute<GenAttr.DecomposeUI>() != null)
                {
                    source.AppendLine($"{GeneralUtility.Indent(level)}ImGui::Banner(\"{name}\");");
                    source.AppendLine($"{GeneralUtility.Indent(level)}({pointer}).OnGUI();");
                }
                else
                {
                    source.AppendLine($"{GeneralUtility.Indent(level)}if (ImGui::CollapsingHeader(\"{name}\")) {{");
                    source.AppendLine($"{GeneralUtility.Indent(level+1)}ImGui::Indent();");
                    source.AppendLine($"{GeneralUtility.Indent(level+1)}({pointer}).OnGUI();");
                    source.AppendLine($"{GeneralUtility.Indent(level+1)}ImGui::Unindent();");
                    source.AppendLine($"{GeneralUtility.Indent(level)}}}");
                }
            }
        }
    }
}
