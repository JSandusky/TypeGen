using GenAttr;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class PInvokeGenerator
    {
        static HashSet<Type> BlobTypes = new HashSet<Type>(new Type[]
        {
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(float),
            typeof(bool),
            typeof(double),
            typeof(byte)
        });

        public static void Generate(List<Type> types, string output)
        {
            var dirPath = System.IO.Path.GetDirectoryName(output);

            foreach (var type in types)
            {

            }

            StringBuilder cppHeader = new StringBuilder();
            StringBuilder cppSource = new StringBuilder();
            StringBuilder csPInvoke = new StringBuilder();
            StringBuilder csTypes = new StringBuilder();

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<NoProcessAttribute>() != null)
                    continue;

                foreach (var field in type.GetFields())
                {
                    if (field.GetCustomAttribute<NoProcessAttribute>() != null)
                        continue;

                    if (field.FieldType.IsPrimitive && BlobTypes.Contains(field.FieldType))
                    { 
                        /*
                         float MyType_Field_Get(void* data) {
                            return ((MyType*)data)->GetField();
                         }
                         void MyType_Field_Set(void* data, float value) {
                            ((MyType*)data)->SetField(value);
                         }
                         */ 
                        cppSource.AppendLine($"{field.FieldType.Name} {type.Name}_{field.Name}_Get(void* data) {{");
                        cppSource.AppendLine($"    return (({type.Name}*)data)->Get{field.Name}();");
                        cppSource.AppendLine("}");

                        cppSource.AppendLine($"void {type.Name}_{field.Name}_Set(void* data, {field.FieldType.Name} value) {{");
                        cppSource.AppendLine($"    (({type.Name}*)data)->Set{field.Name}(value);");
                        cppSource.AppendLine("}");

                        csPInvoke.AppendLine($"[DllImport(\"Engine.dll\")]");
                        csPInvoke.AppendLine($"internal static {field.FieldType.Name} {type.Name}_{field.Name}_Get(IntPtr obj);");
                        csPInvoke.AppendLine($"[DllImport(\"Engine.dll\")]");
                        csPInvoke.AppendLine($"internal static {field.FieldType.Name} {type.Name}_{field.Name}_Set(IntPtr obj, {field.FieldType.Name} value);");
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        cppSource.AppendLine($"const char* {type.Name}_{field.Name}_Get(void* data) {{");
                        cppSource.AppendLine($"    return (({type.Name}*)data)->Get{field.Name}().c_str();");
                        cppSource.AppendLine($"}}");
                        cppSource.AppendLine($"void {type.Name}_{field.Name}_Set(void* data, char* str) {{");
                        cppSource.AppendLine($"    return (({type.Name}*)data)->Set{field.Name}(str);");
                        cppSource.AppendLine($"}}");

                        csPInvoke.AppendLine($"[DllImport(\"Engine.dll\")]");
                        csPInvoke.AppendLine($"internal static {field.FieldType.Name} {type.Name}_{field.Name}_Get(IntPtr obj);");
                        csPInvoke.AppendLine($"[DllImport(\"Engine.dll\")]");
                        csPInvoke.AppendLine($"internal static {field.FieldType.Name} {type.Name}_{field.Name}_Get(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string value);");
                    }
                    else if (typeof(IList).IsAssignableFrom(field.FieldType))
                    {

                    }
                    else if (typeof(IDictionary).IsAssignableFrom(field.FieldType))
                    {

                    }
                }
            }
        }
    }
}
