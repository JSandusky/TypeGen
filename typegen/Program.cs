using CommandLine;
using CommandLine.Text;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using STB;

namespace typegen
{
    public enum Generator
    {
        CPPSerial, // emit serialization
        XMacro,
        TaggedData,
        UrhoAttributes,
        ASBindings,
        DataFields,
        VariantCall,
        VariantMath
    }

    public class CmdLineOptions
    {
        [Option('g', "gen", Required = true, HelpText = "Generator to use (CPP, XMacro, XMacroDef, CPPStringStream)")]
        public Generator gen { get; set; }

        [Option('f', "file", Required = false, HelpText = "Specifies the file to compile and reflect")]
        public string file { get; set; }

        [Option('c', "config-file", Required = false, HelpText = "Specifies the config file to load for processing")]
        public string configFile { get; set; }

        [Option('o', "output", Default="generated", Required = false, HelpText = "Output file to write to excluding extension")]
        public string output { get; set; }

        [Option("postunderscore", Default = false, HelpText = "Append a trailing underscore to memeber names")]
        public bool postfixUnderscore { get; set; } = false;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CmdLineOptions>(args)
                .WithParsed(o => Execute(o));
        }    

        static int Execute(CmdLineOptions options)
        {
            ReflectionScanner scanner = new ReflectionScanner();
            scanner.APIDeclarations.Add("URHO3D_API");
            scanner.IncludePrivateMembers = true;

            if (!String.IsNullOrEmpty(options.file))
            {
                scanner.Scan(System.IO.File.ReadAllText(options.file));
            }
            else if (!String.IsNullOrEmpty(options.configFile))
            {
                String[] lines = System.IO.File.ReadAllLines(options.configFile);
                foreach (string str in lines)
                {
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        if (str.StartsWith("//"))
                            continue;
                        try
                        {
                            scanner.Scan(System.IO.File.ReadAllText(str));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
            }
            
            scanner.ConcludeScanning();

            DatabaseGenerators writer = new DatabaseGenerators();

            if (options.gen == Generator.TaggedData)
            { 
                StringBuilder sb = new StringBuilder();
                writer.WriteTaggedData(sb, scanner);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.UrhoAttributes)
            { 
                StringBuilder sb = new StringBuilder();
                writer.BindUrhoAttributes(sb, scanner);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.CPPSerial)
            {
                StringBuilder sb = new StringBuilder();
                writer.WriteSerialization(sb, scanner);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.ASBindings)
            {
                StringBuilder sb = new StringBuilder();
                writer.BindScript(sb, scanner);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.VariantCall)
            {
                StringBuilder sb = new StringBuilder();
                writer.BindVariantCalls(sb, scanner.database);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.DataFields)
            {
                StringBuilder sb = new StringBuilder();
                writer.WriteDataFields(sb, scanner.database);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.VariantMath)
            {
                StringBuilder sb = new StringBuilder();
                writer.WriteVariantMath(sb, scanner.database);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else if (options.gen == Generator.XMacro)
            {
                StringBuilder sb = new StringBuilder();
                writer.WriteXMacros(sb, scanner.database);
                System.IO.File.WriteAllText(options.output, sb.ToString());
            }
            else
                Console.WriteLine("Unimplemented generator specified");

            return 0;
        }
    }
}
