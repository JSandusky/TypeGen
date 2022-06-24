using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace typegen
{

    public static class GeneralUtility
    {
        /// <summary>
        /// Silly helper to generate space-indentation.
        /// </summary>
        public static string Indent(int level)
        {
            string indent = "";
            while (level > 0)
            {
                indent += "    ";
                level -= 1;
            }
            return indent;
        }

        /// <summary>
        /// Brute-force equality comparison, not intended to be smart, just to not
        /// redundantly trip "file has changed" tracking in other toolchains if
        /// reflection processing is being used during a prebuild-step.
        /// 
        /// The odds are high generated code either a super-massive cpp file or
        /// a frequently referenced header (the worst case)
        /// </summary>
        public static void WriteIfDifferent(string targetFile, string content)
        {
            if (System.IO.File.Exists(targetFile))
            {
                var txt = System.IO.File.ReadAllText(targetFile);
                if (txt != content)
                    System.IO.File.WriteAllText(targetFile, content);
            }
            else
                System.IO.File.WriteAllText(targetFile, content);
        }

        public static string ToPrettyString(string inStr)
        {
            // turn under-scores into spaces my_name_is_bob
            inStr = inStr.Replace('_', ' ');

            // trim AFTER under-scores to spaces because of `myVariable_`
            inStr = inStr.Trim();

            if (char.IsLower(inStr[0]))
                inStr = char.ToUpper(inStr[0]) + inStr.Substring(1);

            inStr = Regex.Replace(inStr, @"([A-Z0-9]+)", " $1").Trim();

            return inStr;
        }

        /// <summary>
        /// Partition a source file into blocks contained withing BEGIN_PARSE and END_PARSE
        /// 
        /// BEGIN_PARSE
        /// 
        /// ... code ...
        /// 
        /// END_PARSE
        /// 
        /// </summary>
        /// <param name="srcStr">string to partition</param>
        /// <returns></returns>
        public static string PartitionCode(string srcStr)
        {
            string openBlock = "BEGIN_PARSE";
            string closeBlock = "END_PARSE";
            int idx = srcStr.IndexOf(openBlock);
            if (idx == -1)
                return srcStr;
            int endIdx = srcStr.IndexOf(closeBlock);

            StringBuilder output = new StringBuilder();
            output.AppendLine(srcStr.Substring(idx + openBlock.Length, endIdx - idx + openBlock.Length));
           
            while ((idx = srcStr.IndexOf(openBlock, idx + 1)) != -1)
            {
                if ((endIdx = srcStr.IndexOf(closeBlock, endIdx+1)) != -1)
                    output.AppendLine(srcStr.Substring(idx + openBlock.Length, endIdx - idx + openBlock.Length));
            }

            return output.ToString();
        }

        /// <summary>
        /// Takes the input lines and returns a single block of code with everything deeper than 2 levels removed.
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string Minimalize(string[] source)
        {
            DepthScanner scan = new DepthScanner();
            scan.Process(string.Join("\n", source));
            //+1 namespace
            //+1 type

            List<string> taken = new List<string>();
            for (int i = 0; i < source.Length; ++i)
            {
                if (scan.GetBraceDepth(i) <= 2)
                    taken.Add(source[i]);
            }
            return string.Join("\n", taken);
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static string RangeSplice(this string[] l, int start)
        {
            return string.Join("\r\n", l.SubArray(start, l.Length - start));
        }
    }
}
