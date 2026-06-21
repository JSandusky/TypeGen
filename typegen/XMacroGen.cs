using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;

namespace typegen
{
    public class XMacroGen
    {
        public static void WriteAsDefines(List<Type> types, string output)
        {
            if (types.Count == 0)
            {
                Console.WriteLine("No types found to process");
                return;
            }

            Filter(ref types);

            if (string.IsNullOrEmpty(output))
                output = "XMacroLists";

            StringBuilder src = new StringBuilder();

            StringBuilder classMacroList = new StringBuilder();
            StringBuilder structMacroList = new StringBuilder();
            StringBuilder enumMacroList = new StringBuilder();
            classMacroList.AppendLine("#define _xmacro_classes_List \\ ");
            structMacroList.AppendLine("#define _xmacro_structs_List \\ ");
            enumMacroList.AppendLine("#define _xmacro_enums_List \\ ");

            foreach (var type in types)
            {
                if (type.IsStructOrClass())
                {
                    if (type.GetCustomAttribute<GenAttr.AsStructAttribute>() == null && type.IsClass == true)
                    { 
                        classMacroList.AppendLine($"    XMACRO( {type.Name}, {type.BaseType.Name} ) \\ ");
                    }
                    else
                        structMacroList.AppendLine($"    XMACRO( {type.Name}, {type.BaseType.Name} ) \\ ");
                }
                else if (type.IsEnum)
                    enumMacroList.AppendLine($"    XMACRO( {type.Name} ) \\ ");
            }

            src.AppendLine(classMacroList.ToString());
            src.AppendLine(structMacroList.ToString());
            src.AppendLine(enumMacroList.ToString());

            foreach (var type in types)
            {
                // should we do this?
                //??? if (type.GetCustomAttribute<GenAttr.NativeTypeAttribute>() != null)
                //???     continue;

                src.AppendLine($"#define xmacro_{type.Name}_List \\ ");
                if (type.IsEnum)
                {
                    string[] enumNames = type.GetEnumNames();
                    bool hasCount = type.EnumHasCount();

                    for (int i = 0; i < type.EnumIterations(); ++i)
                    { 
                        src.Append($"    XMACRO( {type.Name}, {enumNames[i]}, \"{GeneralUtility.ToPrettyString(enumNames[i])}\" )");
                        if (i < enumNames.Length - 1)
                            src.Append(" \\ \r\n");
                        else
                            src.Append("\r\n");
                    }
                }
                else
                {
                    List<FieldInfo> flds = type.GetFields().ToList();
                    Filter(ref flds);

                    for (int i = 0; i < flds.Count; ++i)
                    { 
                        string typename = CPPCommon.GetMemberTypeName(flds[i].FieldType, flds[i]);

                        src.Append($"    XMACRO( {typename}, {flds[i].Name} , \"{GeneralUtility.ToPrettyString(flds[i].Name)}\" )");
                        if (i < flds.Count - 1 )
                            src.Append(" \\ \r\n");
                        else
                            src.Append("\r\n");
                    }
                }
            }

            GeneralUtility.WriteIfDifferent(output + ".h", src.ToString());
        }

        public static void WriteAsFiles(List<Type> types, string output)
        {
            throw new Exception("deprecated");
            if (types.Count == 0)
            {
                Console.WriteLine("No types found to process");
                return;
            }

            Filter(ref types);

            foreach (var type in types)
            {
                string outputPath = output + $"/{type.Name}.xmacro";
                // should we do this?
                //??? if (type.GetCustomAttribute<GenAttr.NativeTypeAttribute>() != null)
                //???     continue;

                StringBuilder src = new StringBuilder();
                if (type.IsEnum)
                {
                    string[] enumNames = type.GetEnumNames();

                    foreach (var name in enumNames)
                        src.AppendLine($"XMACRO( {type.Name},  {name}, \"{GeneralUtility.ToPrettyString(name)}\" ),");

                    GeneralUtility.WriteIfDifferent(outputPath, src.ToString());
                }
                else
                {
                    List<FieldInfo> flds = type.GetFields().ToList();
                    Filter(ref flds);

                    foreach (var fld in flds)
                        src.AppendLine($"XMACRO( {fld.FieldType.Name}, {fld.Name}, \"{GeneralUtility.ToPrettyString(fld.Name)}\" ),");

                    GeneralUtility.WriteIfDifferent(outputPath, src.ToString());
                    
                }
            }
        }

        static void Filter(ref List<Type> types)
        {
            for (int i = 0; i < types.Count; ++i)
            {
                var tags = types[i].GetCustomAttributes<GenAttr.TagAttribute>();
                if (tags != null)
                {
                    var result = tags.Where(tag => tag.tag.ToLowerInvariant().Trim() == "no_xmacro");
                    if (result != null && result.Count() > 0)
                    {
                        types.RemoveAt(i);
                        --i;
                        continue;
                    }
                }
            }
        }

        static void Filter(ref List<FieldInfo> fields)
        {
            for (int i = 0; i < fields.Count; ++i)
            {
                var tags = fields[i].GetCustomAttributes<GenAttr.TagAttribute>();
                if (tags != null)
                {
                    var result = tags.Where(tag => tag.tag.ToLowerInvariant().Trim() == "no_xmacro");
                    if (result != null && result.Count() > 0)
                    {
                        fields.RemoveAt(i);
                        --i;
                        continue;
                    }
                }
            }
        }
    }
}
