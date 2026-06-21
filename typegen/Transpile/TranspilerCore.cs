using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace typegen.Transpile
{
    public class TranspilerCore
    {
        List<TranspileType> transpilationTypes = new List<TranspileType>();
        List<NamespaceMapping> namespaceMappings = new List<NamespaceMapping>();

        public NamespaceMapping GetNamespace(string ns)
        {
            foreach (var n in namespaceMappings)
                if (n.Namespace == ns)
                    return n;

            return new NamespaceMapping { Namespace = ns, RenameAs = ns, Ignore = false, IncludeFile = $"#include <{ns}.h>" };
        }

        void CompileTypeList(AssemblyCompilation compilation)
        {
            foreach (var type in compilation.Assembly.DefinedTypes)
            {
                bool foundThisType = false;

                foreach (var tree in compilation.SyntaxTrees)
                {
                    SyntaxNode foundSyntax = null;
                    
                    if (type.IsEnum)
                        foundSyntax = SelectClassRoot(type, tree.GetRoot(), null, type.Namespace, false, true);
                    else if (type.IsValueType && !type.IsPrimitive)
                        foundSyntax = SelectClassRoot(type, tree.GetRoot(), null, type.Namespace, true, false);
                    else
                        foundSyntax = SelectClassRoot(type, tree.GetRoot(), null, type.Namespace, false, false);

                    if (foundSyntax != null)
                    {
                        var foundExisting = FindType(type.FullName);
                        if (foundExisting != null)
                            foundExisting.ClassDecl.Add(foundSyntax as ClassDeclarationSyntax);
                        else
                        {
                            TranspileType tgt = new TranspileType(type, foundSyntax);
                            foundThisType = true;
                            transpilationTypes.Add(tgt);
                        }
                    }
                }

                if (!foundThisType)
                    throw new Exception("Failed to find syntax for type");
            }
        }

        SyntaxNode SelectClassRoot(Type forType, SyntaxNode node, string activeNS, string withNamespace, bool isStruct, bool isEnum)
        {
            if (node.GetType() == typeof(NamespaceDeclarationSyntax))
                activeNS = node.ChildNodes().First().GetText().ToString().Trim();

            if (isStruct && node.GetType() == typeof(StructDeclarationSyntax) && activeNS == withNamespace)
            {
                if (((StructDeclarationSyntax)node).Identifier.ValueText == forType.Name)
                    return node;
                return null;
            }

            if (isEnum && node.GetType() == typeof(EnumDeclarationSyntax) && activeNS == withNamespace)
            {
                if (((EnumDeclarationSyntax)node).Identifier.ValueText == forType.Name)
                    return node;
                return null;
            }

            if (!isStruct && !isEnum && node.GetType() == typeof(ClassDeclarationSyntax) && activeNS == withNamespace)
            {
                if (((ClassDeclarationSyntax)node).Identifier.ValueText == forType.Name)
                    return node;
                return null;
            }

            foreach (var child in node.ChildNodes())
            {
                var retVal = SelectClassRoot(forType, child, activeNS, withNamespace, isStruct, isEnum);
                if (retVal != null)
                    return retVal;
            }
            return null;
        }

        TranspileType FindType(string name)
        {
            foreach (var t in transpilationTypes)
                if (t.TypeInfo.Name == name)
                    return t;
            return null;
        }

        public void CompileAssembly(string[] sourceCodes)
        {
            List<SyntaxTree> trees = new List<SyntaxTree>();

            foreach (var src in sourceCodes)
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(src);
                if (syntaxTree != null)
                    trees.Add(syntaxTree);
                else
                    throw new Exception("Failed to compile file");
            }

            Assembly[] references = new[] {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(List<int>).Assembly,
                typeof(GenAttr.AsStructAttribute).Assembly
            };

            var mrefs = references.Select(a => MetadataReference.CreateFromFile(a.Location));
            var compilation = CSharpCompilation.Create(System.IO.Path.GetRandomFileName(), trees.ToArray(), mrefs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (result.Success)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Console.WriteLine("\rCompilation successful\r\n");
                    Assembly compiled = Assembly.Load(ms.ToArray());

                    AssemblyCompilation compResult = new AssemblyCompilation();
                    compResult.Assembly = compiled;
                    compResult.SyntaxTrees.AddRange(trees);
                    compResult.Sources.AddRange(sourceCodes);

                    CompileTypeList(compResult);
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

        public void GenerateCode(TranspilerGenerator generator)
        {
            TranspileFileTarget tgt = new TranspileFileTarget();
            tgt.FileName = "Generated";

            foreach (TranspileType exportTarget in transpilationTypes)
            {
                generator.ExportType(tgt, exportTarget);
            }

            System.IO.File.WriteAllText(tgt.FileName + ".h", tgt.Header.ToString());
        }
    }
}
