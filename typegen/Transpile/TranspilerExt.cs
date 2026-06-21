using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen.Transpile
{
    public static class TranspilerExt
    {
        public static bool HasModifier(this SyntaxNode node, string text)
        {
            if (node.GetType() == typeof(ClassDeclarationSyntax))
                return ((ClassDeclarationSyntax)node).Modifiers.Any(c => c.ValueText == text);
            else if (node.GetType() == typeof(StructDeclarationSyntax))
                return ((StructDeclarationSyntax)node).Modifiers.Any(c => c.ValueText == text);
            else if (node.GetType() == typeof(FieldDeclarationSyntax))
                return ((FieldDeclarationSyntax)node).Modifiers.Any(c => c.ValueText == text);
            else if (node.GetType() == typeof(PropertyDeclarationSyntax))
                return ((PropertyDeclarationSyntax)node).Modifiers.Any(c => c.ValueText == text);
            else if (node.GetType() == typeof(MethodDeclarationSyntax))
                return ((MethodDeclarationSyntax)node).Modifiers.Any(c => c.ValueText == text);
            return true;
        }

        public static bool IsPublic(this SyntaxNode node) { return node.HasModifier("public"); }

        public static bool IsProtected(this SyntaxNode node) { return node.HasModifier("protected"); }

        public static bool IsPrivate(this SyntaxNode node) { return node.HasModifier("private"); }

        public static bool IsStatic(this SyntaxNode node) { return node.HasModifier("static"); }
    }
}
