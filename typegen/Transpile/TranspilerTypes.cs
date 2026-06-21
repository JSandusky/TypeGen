using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace typegen.Transpile
{
    public class SourceEmission
    { 
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    public class TranspileType
    { 
        public System.Reflection.TypeInfo TypeInfo {  get; set; }
        public List<SyntaxNode> ClassDecl { get; private set;} = new List<SyntaxNode>();
        
        public List<SourceEmission> Emission { get; private set; } = new List<SourceEmission>();

        public TranspileType(System.Reflection.TypeInfo compiledType, SyntaxNode syntaxNode)
        {
            TypeInfo = compiledType;
            ClassDecl.Add(syntaxNode);
        }
    }

    public class TranspileFile
    {
        public List<TranspileType> Transpiling { get; private set; } = new List<TranspileType>();
    }

    public struct NamespaceMapping
    {
        public string Namespace;
        public string IncludeFile;
        public string RenameAs;
        public bool Ignore;
    }

    public class AssemblyCompilation
    {
        public Assembly Assembly { get;set;}
        public List<SyntaxTree> SyntaxTrees { get;private set;} = new List<SyntaxTree>();
        public List<string> Sources { get;private set;} = new List<string>();
    }

    public class TranspileFileTarget
    {
        public string FileName { get; set; }
        public StringBuilder Header {  get; private set;} = new StringBuilder();
        public StringBuilder Source  { get; private set; } = new StringBuilder();
    }
}
