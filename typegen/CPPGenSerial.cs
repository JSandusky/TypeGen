using System;
using System.Reflection;
using System.Text;

namespace typegen
{
    public class CPPGenSerial
    {
        public static void WriteSerializationSource(StringBuilder source, Type forType)
        {
            FieldInfo[] flds = forType.GetFields();

            // Serializer
            source.AppendLine($"void {forType.Name}::Save(Serializer& dest) {{");
            foreach (FieldInfo fld in flds)
            {
                if (fld.FieldType.IsPrimitive || fld.FieldType.IsValueType)
                    source.AppendLine($"    dest.Write({fld.Name});");
                else
                    source.AppendLine($"    if ({fld.Name}) {{ dest.Write(true); {fld.Name}.Save(dest); }} else {{ dest.Write(false); }}");
            }
            source.AppendLine("}\r\n");

            // Deserializer
            source.AppendLine($"void {forType.Name}::Load(Deserializer& src) {{");
            foreach (FieldInfo fld in flds)
            {
                if (fld.FieldType.IsPrimitive || fld.FieldType.IsValueType)
                    source.AppendLine($"    src.Read({fld.Name});");
                else
                    source.AppendLine($"    if (src.ReadBool()) {{ src.CreateAndRead({fld.Name}); }}");
            }
            source.AppendLine("}\r\n");
        }

        public static void WriteUrhoSerializable(StringBuilder source, Type forType)
        {
            FieldInfo[] flds = forType.GetFields();

            // Serializer
            source.AppendLine($"void {forType.Name}::SaveXML(XMLElement& dest) {{");
            foreach (FieldInfo fld in flds)
            {
                if (fld.FieldType.IsPrimitive || fld.FieldType.IsValueType)
                    source.AppendLine($"    dest.Write({fld.Name});");
                else
                    source.AppendLine($"    if ({fld.Name}) {{ dest.Write(true); {fld.Name}.Save(dest); }} else {{ dest.Write(false); }}");
            }
            source.AppendLine("}\r\n");

            // Deserializer
            source.AppendLine($"void {forType.Name}::LoadXML(const XMLElement& src) {{");
            foreach (FieldInfo fld in flds)
            {
                if (fld.FieldType.IsPrimitive || fld.FieldType.IsValueType)
                    source.AppendLine($"    src.Read({fld.Name});");
                else
                    source.AppendLine($"    if (src.ReadBool()) {{ src.CreateAndRead({fld.Name}); }}");
            }
            source.AppendLine("}\r\n");
        }
    }
}
