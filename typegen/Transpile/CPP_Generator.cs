using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace typegen.Transpile
{
    public class CPP_Generator : TranspilerGenerator
    {
        TranspileFileTarget target;
        List<TranspileFile> files;

        public override void ExportType(TranspileFileTarget target, TranspileType type)
        {
            this.target = target;
            if (type.TypeInfo.IsEnum)
                ExportEnum(target, type);
            else
                ExportStructOrClass(target, type);
        }

        void ExportEnum(TranspileFileTarget target, TranspileType type)
        {
            target.Header.AppendLine($"enum {type.TypeInfo.Name} {{");
            
            var enumMembers = type.ClassDecl[0].ChildNodes().Where(c => c.Kind() == SyntaxKind.EnumMemberDeclaration);
            if (enumMembers != null)
            {
                foreach (var enumMem in enumMembers)
                {
                    string enumValueName = ((EnumMemberDeclarationSyntax)enumMem).Identifier.ValueText;
                    if (enumMem.ChildNodes().Count() > 0)
                        target.Header.AppendLine($"    {enumValueName}{enumMem.ChildNodes().First().GetText().ToString()},");
                    else
                        target.Header.AppendLine($"    {enumValueName},");
                }
            }

            target.Header.AppendLine("};");
            target.Header.AppendLine("");
        }

        void ExportStructOrClass(TranspileFileTarget target, TranspileType type)
        {

            if (type.TypeInfo.IsValueType)
            {
                if (type.TypeInfo.BaseType != typeof(System.ValueType))
                    target.Header.AppendLine($"struct {type.TypeInfo.Name} : public {type.TypeInfo.BaseType.Name}");
                else
                    target.Header.AppendLine($"struct {type.TypeInfo.Name}");
            }
            else
            { 
                if (type.TypeInfo.BaseType != typeof(object))
                    target.Header.AppendLine($"class {type.TypeInfo.Name} : public {type.TypeInfo.BaseType.Name}");
                else
                    target.Header.AppendLine($"class {type.TypeInfo.Name}");
            }

            foreach (var implements in type.TypeInfo.ImplementedInterfaces)
                target.Header.AppendLine($"    , {implements.Name}");

            target.Header.AppendLine("{");

            bool wroteFields = false;
            foreach (var classDecl in type.ClassDecl)
            {
                var fields = classDecl.ChildNodes().Where(c => c.Kind() == SyntaxKind.FieldDeclaration);
                
                // empty line
                if (wroteFields && fields != null && fields.Count() > 0)
                    target.Header.AppendLine("");

                foreach (var field in fields)
                {
                    FieldDeclarationSyntax f = field as FieldDeclarationSyntax;
                    Write(f.ChildNodes().First() as VariableDeclarationSyntax);
                    wroteFields = true;
                }
            }

            bool wroteProperties = false;
            foreach (var classDecl in type.ClassDecl)
            {
                var properties = classDecl.ChildNodes().Where(c => c.Kind() == SyntaxKind.PropertyDeclaration);

                if (wroteProperties && properties != null && properties.Count() > 0)
                    target.Header.AppendLine($"");
            }

            foreach (var classDecl in type.ClassDecl)
            {
                var methods = classDecl.ChildNodes().Where(c => c.Kind() == SyntaxKind.MethodDeclaration);
            }

            target.Header.AppendLine("};");
        }

        void Write(VariableDeclarationSyntax varDecl)
        {
            if (varDecl == null)
                return;

            var parts = varDecl.ChildNodes().ToArray();
            for (int i = 0; i < parts.Length; ++i)
            {
                if (parts[i].GetType() == typeof(IdentifierNameSyntax))
                {
                    IdentifierNameSyntax s = parts[i] as IdentifierNameSyntax;
                    target.Header.Append($"    {s.GetText().ToString().Trim()}");
                }
                else if (parts[i].GetType() == typeof(PredefinedTypeSyntax))
                {
                    PredefinedTypeSyntax s = parts[i] as PredefinedTypeSyntax;
                    target.Header.Append($"    {s.GetText().ToString().Trim()}");
                }
                else if (parts[i].GetType() == typeof(QualifiedNameSyntax))
                {
                    QualifiedNameSyntax s = parts[i] as QualifiedNameSyntax;
                    target.Header.Append($"    {s.GetText().ToString().Replace(".","::").Trim()}");
                }
                else if (parts[i].GetType() == typeof(VariableDeclaratorSyntax))
                {
                    VariableDeclaratorSyntax s = parts[i] as VariableDeclaratorSyntax;
                    target.Header.Append($" {s.GetText().ToString().Replace(".","::").Trim()};\r\n");
                }
            }
            
        }
    }
}
