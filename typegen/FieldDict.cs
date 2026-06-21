using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    /*  Super simple utilities to rip out #define XXX ### from a file.
     *  
     *  FieldDict.Build("MySrcFile.h", "MySrcOut.cpp", "FieldTag", "Tag")
     *      or
     *  FieldDict.Build("MySrcFile.h", "MySrcOut.cpp", "MsgID", "Msg")
     *      etc
     * 
     *  Example: process a file containing lists of:
     *      #define ttMyTagName 57512
     *      #define ttMyOtherTag 57513
     *  It will then dump out a source file containing static dictionaries for conversions
     *  to and from string/ID value as wells as function implementations for XXXToName and XXXFromString
     *  
     *  ParseIDHeader(string[]) can be used to extract #define abcdefg #### parts out of files
     */
    public class FieldEntry
    {
        public string Name { get; set; }
        public ushort Value { get; set; }
        public string Comment { get; set; }

        public FieldEntry(string name, ushort value, string comment = "")
        {
            Name = name;
            Value = value;
            Comment = comment ?? "";
        }
    }

    public class FieldDict
    {
        public static List<FieldEntry> ParseIDHeader(string[] lines)
        {
            List<FieldEntry> fields = new List<FieldEntry>();

            foreach (var line in lines)
            {
                var trim = line.Trim();
                if (trim.StartsWith("//") || trim.StartsWith("/*"))
                    continue;
                if (trim.StartsWith("#pragma"))
                    continue;
                if (trim.StartsWith("#ifdef") || trim.StartsWith("#ifndef") || trim.StartsWith("#endif") || trim.StartsWith("#elif"))
                    continue;
                if (trim.StartsWith("#define FIELDICT_H"))
                    continue;
                if (trim.Length == 0)
                    continue;

                if (trim.StartsWith("#define "))
                {
                    var tagText = trim.Replace("#define ", "");

                    string tagName = "";
                    int c = 0;
                    for (; c < tagText.Length; ++c)
                    {
                        if (char.IsWhiteSpace(tagText[c]))
                            break;
                        tagName += tagText[c];
                    }

                    while (c < tagText.Length && char.IsWhiteSpace(tagText[c]))
                        ++c;

                    string remainder = c < tagText.Length ? tagText.Substring(c) : "";

                    string comment = "";
                    string valueText = remainder;
                    int commentStart = remainder.IndexOf("//");
                    if (commentStart >= 0)
                    {
                        comment = remainder.Substring(commentStart + 2).Trim();
                        valueText = remainder.Substring(0, commentStart).Trim();
                    }
                    else
                    {
                        int blockStart = remainder.IndexOf("/*");
                        if (blockStart >= 0)
                        {
                            int blockEnd = remainder.IndexOf("*/", blockStart + 2);
                            if (blockEnd >= 0)
                            {
                                comment = remainder.Substring(blockStart + 2, blockEnd - blockStart - 2).Trim();
                                valueText = remainder.Substring(0, blockStart).Trim();
                            }
                        }
                    }

                    ushort value = 0;
                    valueText = valueText.Replace("U", "");
                    if (valueText.StartsWith("0x"))
                    {
                        valueText = valueText.Replace("0x", "");
                        value = ushort.Parse(valueText, System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        if (ushort.TryParse(valueText, out value) == false)
                        {
                            value = fields.FirstOrDefault(i => i.Name == valueText)?.Value ?? 0;
                        }
                    }

                    fields.Add(new FieldEntry(tagName, value, comment));
                }
            }

            return fields;
        }

        /// <summary>
        /// Example generator that calls ParseIDHEader and then builds some ToName FromName functions
        /// </summary>
        /// <param name="srcFile">Filepath to read and process</param>
        /// <param name="outFile">Filepath to write the generated code to</param>
        /// <param name="tagTypeName">What the tag datatype is called, ie FieldTag, MsgID, etc</param>
        /// <param name="functionWord">World used in the functions, ie "Tag" for "TagFromName"</param>
        public static void Build(string srcFile, string outFile, string tagTypeName, string functionWord)
        {
            var lines = System.IO.File.ReadAllLines(srcFile);

            var fields = ParseIDHeader(lines);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("/// AUTOGENERATED CODE - DO NOT MODIFY");
            sb.AppendLine($"/// Generated: {DateTime.Now.ToString()}");
            sb.AppendLine();
            sb.AppendLine($"#include <{tagTypeName}.h>");
            sb.AppendLine("#include <map.h>");
            sb.AppendLine("#include <string.h>");
            sb.AppendLine();

            sb.AppendLine($"static const std::map<std::string, {tagTypeName}> _{functionWord}_from_name = {{");
            foreach (var v in fields)
                sb.AppendLine($"    {{ \"{v.Name}\", {v.Value} }},");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine($"{tagTypeName} {functionWord}FromName(const std::string& aName) {{");
            sb.AppendLine($"    auto found = _{functionWord}_from_name.find(aName);");
            sb.AppendLine($"    if (found != _{functionWord}_from_name.end())");
            sb.AppendLine("        return found->second;");
            sb.AppendLine("    return {};");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine($"static const std::map<{tagTypeName}, std::string> _{functionWord}_to_name = {{");
            foreach (var v in fields)
                sb.AppendLine($"    {{ {v.Value}, \"{v.Name}\" }},");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine($"std::string {functionWord}ToName({tagTypeName} aTag) {{");
            sb.AppendLine($"    auto found = _{functionWord}_to_name.find(aTag);");
            sb.AppendLine($"    if (found != _{functionWord}_to_name.end())");
            sb.AppendLine("        return found->second;");
            sb.AppendLine("    return std::string();");
            sb.AppendLine("}");

            System.IO.File.WriteAllText(outFile, sb.ToString());
        }
    }
}