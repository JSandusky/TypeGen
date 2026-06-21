using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class UrhoGen
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

            // enums only first
            foreach (Type t in types)
            {
                if (t.IsEnum)
                {
                    //WriteEnumTables(source, t);
                    source.AppendLine("");
                }
                else
                {

                }
            }

            // classes and structs
            foreach (Type t in types)
            {
                if (!t.IsEnum)
                {

                }
            }
        }
    }
}
