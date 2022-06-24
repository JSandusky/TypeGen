using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public static class Extensions
    {

        public static string Unquote(this string str)
        {
            var trim = str.Trim();
            if (trim.StartsWith("\"") && trim.EndsWith("\""))
                return trim.Substring(1, trim.Length - 2).Replace("``", "\"");
            return str;
        }

        /// Grabs a string while tracking scope depth of { and (
        /// This allows grabbing a struct declaration such as { 0.5f, 0.15f }
        public static string ToStringUntil(this STB.Lexer lexer, long[] codes)
        {
            string ss = "";
            STB.Lexer lex = new STB.Lexer("");
            lexer.SaveState(lex);
            int scopeDepth = 0;
            do
            {
                if (lexer.token == '{' || lexer.token == '(')
                    ++scopeDepth;

                if (scopeDepth == 0)
                {
                    foreach (long l in codes)
                        if (lexer.token == l)
                            return ss;
                }

                if (lexer.token == '}' || lexer.token == ')')
                    --scopeDepth;

                ss += lexer.string_value;
                lexer.GetToken();
            } while (true);
        }
    }
}
