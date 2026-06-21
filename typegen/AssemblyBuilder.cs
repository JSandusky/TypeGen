using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace typegen
{
    public class AssemblyBuilder
    {
        static void Print(SyntaxNode node, int depth)
        {
            Console.WriteLine(GeneralUtility.Indent(depth) + node.Kind().ToString());
            //if (node.Kind() == SyntaxKind.FieldDeclaration)
            //{
            //    Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax n = node as Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax;
            //    Console.WriteLine(n.ChildNodes().Last().ChildNodes().Last().GetText());
            //}
            //else
            {
                if (node.Kind() == SyntaxKind.NamespaceDeclaration)
                    Console.WriteLine(node.ChildNodes().First().GetText());
                foreach (var n in node.ChildNodes())
                    Print(n, depth + 1);
            }
        }

        public static Assembly Compile(string code)
        {
            Transpile.TranspilerCore core = new Transpile.TranspilerCore();
            core.CompileAssembly(new string[] {  code });
            core.GenerateCode(new Transpile.CPP_Generator());

            Console.WriteLine("\nStarting compiler...");

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

            Assembly[] references = new[] {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(List<int>).Assembly,
                typeof(GenAttr.AsStructAttribute).Assembly
            };

            Print(syntaxTree.GetRoot(), 0);

            var mrefs = references.Select(a => MetadataReference.CreateFromFile(a.Location));
            var compilation = CSharpCompilation.Create(Path.GetRandomFileName(), new[] { syntaxTree }, mrefs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (result.Success)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Console.WriteLine("\rCompilation successful\r\n");
                    return Assembly.Load(ms.ToArray());
                }
                else
                {
                    foreach (Diagnostic diag in result.Diagnostics)
                    {
                        if (diag.IsWarningAsError || diag.Severity == DiagnosticSeverity.Error)
                            Console.WriteLine(string.Format("Line: {1}, {0}\r\n", diag.GetMessage(), diag.Location.GetLineSpan().StartLinePosition.Line));
                    }

                    throw new InvalidOperationException(string.Join("\n", result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Id}: {d.GetMessage()}")));
                }
            }
        }

        class TypeRecord
        {
            public Type type_;
            public List<Type> references_ = new List<Type>();
        }

        /// <summary>
        /// The purpose of this function is to attempt sorting types by dependency (via depth) to accomodate C++'s
        /// requirements for knowing about a type during compilation.
        /// </summary>
        public static List<Type> SortedTypes(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();

            List<TypeRecord> records = new List<TypeRecord>();
            for (int i = 0; i < types.Length; ++i)
            {
                TypeRecord rec = new TypeRecord { type_ = types[i] };
                records.Add(rec);
            }

            for (int i = 0; i < records.Count; ++i)
            {
                TypeRecord rec = records[i];

                // are we a class or struct
                if (rec.type_.IsStructOrClass())
                {
                    FieldInfo[] fields = rec.type_.GetFields();
                    for (int f = 0; f < fields.Length; ++f)
                    {
                        FieldInfo fld = fields[f];
                        if (fld.FieldType.IsStructOrClass())
                            rec.references_.Add(fld.FieldType);
                    }
                }
            }

            records.Sort((lhs, rhs) => {

                // if either one is 0 then we prioritize that (enums will end up being here)
                if (lhs.references_.Count == 0 || rhs.references_.Count == 0)
                    return lhs.references_.Count.CompareTo(rhs.references_.Count);

                // who needs to go before whom?
                if (lhs.references_.Contains(rhs.type_))
                    return 1;
                if (rhs.references_.Contains(lhs.type_))
                    return -1;

                // we are now either in trouble or okay
                return 0;
            });

            // convert into ordered type-list
            List<Type> ret = new List<Type>();
            for (int i = 0; i < records.Count; ++i)
                ret.Add(records[i].type_);

            return ret;
        }
    }
}
