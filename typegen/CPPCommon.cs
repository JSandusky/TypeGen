using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class CPPCommon
    {
        public static void WriteHeaders(StringBuilder headerSrc, List<Type> typeList)
        {
            headerSrc.AppendLine("#pragma once\r\n");

            headerSrc.AppendLine("#include <map>");
            headerSrc.AppendLine("#include <memory>");
            headerSrc.AppendLine("#include <string>");
            headerSrc.AppendLine("#include <vector>");

        // Process our forced headers
            List<string> usedHeaders = new List<string>();
            foreach (var t in typeList)
            {
                var headers = t.GetCustomAttributes<GenAttr.HeaderAttribute>();
                foreach (var inc in headers)
                {
                    if (!usedHeaders.Contains(inc.name))
                    {
                        headerSrc.AppendLine($"#include \"{inc.name}\"");
                        usedHeaders.Add(inc.name);
                    }
                }
            }
        }

        static Dictionary<Type, string> typeTable = new Dictionary<Type, string>
        {
            { typeof(bool), "bool" },
            { typeof(byte), "uint8_t" },
            { typeof(char), "char" },
            { typeof(short), "int16_t" },
            { typeof(ushort), "uint16_t" },
            { typeof(int), "int" },
            { typeof(uint), "uint32_t" },
            { typeof(long), "int64_t" },
            { typeof(ulong), "uint64_t" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(string), "std::string" },
        };

        public static bool IsFlatArray(Type t, FieldInfo fld)
        {
            if (t.IsArray)
                return fld.GetCustomAttribute<GenAttr.FixedLengthAttribute>() != null &&
                       fld.GetCustomAttribute<GenAttr.ContainerAttribute>() == null;
            return false;
        }

        public static void CheckComment(StringBuilder sb, FieldInfo field)
        {
            GenAttr.NoteAttribute note = field.GetCustomAttribute<GenAttr.NoteAttribute>();
            if (note != null)
                sb.AppendLine($"    // {note.comment}");
        }

        public static string GetPrintedTypeName(Type t)
        {
            string writeName = t.GetCustomAttribute<GenAttr.NativeTypeAttribute>() != null ?
                t.GetCustomAttribute<GenAttr.NativeTypeAttribute>().name :
                t.Name;

            if (t.IsEnum)
                return $"enum {writeName}";

            if (typeTable.ContainsKey(t))
                return typeTable[t];

            bool asStruct = t.GetCustomAttribute<GenAttr.AsStructAttribute>() != null;

            if ((t.IsValueType && !t.IsPrimitive) || asStruct)
                return $"struct {writeName}";

            if (t.IsClass)
            {
                if (t.BaseType != typeof(object))
                {
                    return $"class {writeName} : public {t.BaseType.Name}";
                }
                return $"class {writeName}";
            }

            if (t.IsGenericType)
            {

            }

            throw new ArgumentException($"Unexpected type provided, {t.Name}");
        }

        public static string GetMemberTypeName(Type t, FieldInfo fld)
        {
            string writeName = t.GetCustomAttribute<GenAttr.NativeTypeAttribute>() != null ?
                t.GetCustomAttribute<GenAttr.NativeTypeAttribute>().name :
                t.Name;

            if (typeTable.ContainsKey(t))
                return typeTable[t];

            if (t.IsArray)
            {
                var fixedLen = fld.GetCustomAttribute<GenAttr.FixedLengthAttribute>();
                if (fixedLen != null)
                {
                    Type elemType = t.GetElementType();
                    var contType = fld.GetCustomAttribute<GenAttr.ContainerAttribute>();
                    if (contType != null)
                        return $"{contType.name}<{GetMemberTypeName(elemType, null)}, {fixedLen.length}>";
                    else
                        return $"{GetMemberTypeName(elemType, null)}"; // requires extra outside handling
                }
                else
                {
                    Type elemType = t.GetElementType();
                    var contType = fld.GetCustomAttribute<GenAttr.ContainerAttribute>();
                    if (contType != null)
                        return $"{contType.name}<{GetMemberTypeName(elemType, null)}>";
                    else
                        return $"std::vector<{GetMemberTypeName(elemType, null)}>";
                }
            }

            if (t.IsEnum)
                return writeName;

            bool asStruct = t.GetCustomAttribute<GenAttr.AsStructAttribute>() != null;

            if ((t.IsValueType && !t.IsPrimitive) || asStruct)
                return writeName;

            if (t.IsClass)
                return writeName;

            if (t.IsGenericType)
            {

            }

            throw new ArgumentException($"Unexpected type provided, {t.Name}");
        }

        public static void WriteConstructor(StringBuilder source, Type t)
        {
            FieldInfo[] flds = t.GetFields();

            // Constructor
            source.AppendLine($"{t.Name}::{t.Name}() {{");
            object instance = Activator.CreateInstance(t);
            foreach (FieldInfo fld in flds)
            {
                if (fld.FieldType.IsPrimitive && !fld.FieldType.IsArray)
                {
                    source.AppendLine($"    {fld.Name} = ({CPPCommon.GetPrintedTypeName(fld.FieldType)}){fld.GetValue(instance)};");
                }
                else if (fld.FieldType.IsArray && fld.FieldType.GetElementType().IsPrimitive)
                    source.AppendLine($"    memset({fld.Name}, 0, sizeof({fld.Name});");
                else if (fld.FieldType == typeof(string))
                {
                    string value = fld.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(value))
                        source.AppendLine($"    {fld.Name} = \"{fld.GetValue(instance)}\";");
                }
                else if (fld.FieldType.IsEnum)
                    source.AppendLine($"    {fld.Name} = {fld.FieldType.Name}::{fld.FieldType.GetEnumNames()[0]};");
            }
            source.AppendLine("}\r\n");
        }
    }
}
