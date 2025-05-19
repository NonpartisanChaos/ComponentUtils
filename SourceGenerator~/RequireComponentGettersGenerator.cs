using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ComponentUtils {
[Generator]
public class RequireComponentGettersGenerator : ISourceGenerator {
    public void Initialize(GeneratorInitializationContext context) {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context) {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver) {
            return;
        }

        foreach (var info in receiver.Infos) {
            var hasNamespace = info.ClassNamespace.Length > 0;

            var source = $@"
{AllUsingSource(info.ClassDeclaration)}

{(hasNamespace ? $"namespace {info.ClassNamespace} {{" : "")}
public partial class {info.ClassName} {{
{AllGettersSource(info.Arguments, info.ComponentTypes)}
}}
{(hasNamespace ? "}" : "")}
";
            context.AddSource($"{info.ClassName}.Getters.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private string AllUsingSource(ClassDeclarationSyntax classDeclaration) {
        //exactly duplicate all using statements from the original class' file
        //this handles namespaces, aliases and fully-qualified names
        var root = classDeclaration.SyntaxTree.GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().OrderBy(u => u.SpanStart);

        return string.Join(Environment.NewLine, usings.Select(u => u.ToString()));
    }

    private string AllGettersSource(in RequireComponentGettersArguments arguments, IEnumerable<string> componentTypes) {
        var sb = new StringBuilder();
        foreach (var componentType in componentTypes) {
            sb.Append(GetterSource(arguments, componentType));
        }

        return sb.ToString();
    }

    private string GetterSource(in RequireComponentGettersArguments arguments, string typeName) {
        //trim namespace ("Some.Name.Space.MyCoolComponent" -> "MyCoolComponent")
        var propertyName = typeName.Substring(typeName.LastIndexOf('.') + 1);

        //"_myCoolComponent"
        var cacheVarName = $"_{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";

        return $@"
    private {typeName} {cacheVarName};
    {arguments.Visibility} {typeName} {propertyName} => {cacheVarName} ??= GetComponent<{typeName}>();
";
    }

    private struct RequireComponentGettersArguments {
        public string Visibility { get; set; }
    }

    private struct RequireComponentGettersInfo {
        public RequireComponentGettersArguments Arguments { get; set; }
        public ClassDeclarationSyntax ClassDeclaration { get; set; }
        public string ClassNamespace { get; set; }
        public string ClassName { get; set; }
        public HashSet<string> ComponentTypes { get; set; }
    }

    private class SyntaxReceiver : ISyntaxReceiver {
        public List<RequireComponentGettersInfo> Infos { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            if (syntaxNode is AttributeSyntax attribute
                && attribute.Name.ToString() == "RequireComponentGetters"
                && attribute.Parent is AttributeListSyntax attributeList
                && attributeList.Parent is ClassDeclarationSyntax classDeclaration) {
                Infos.Add(new RequireComponentGettersInfo {
                    Arguments = GetAttributeArguments(attribute),
                    ClassDeclaration = classDeclaration,
                    ClassNamespace = GetFullNamespace(classDeclaration),
                    ClassName = classDeclaration.Identifier.Text,
                    ComponentTypes = GetRequireComponentTypes(classDeclaration),
                });
            }
        }

        private RequireComponentGettersArguments GetAttributeArguments(AttributeSyntax attribute) {
            //NOTE - must exactly match RequireComponentGettersAttribute argument defaults
            var visibility = "public";

            //for now there's only one argument: visibility
            if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literalExpression) {
                visibility = literalExpression.Token.ValueText;
            }

            return new RequireComponentGettersArguments {
                Visibility = visibility,
            };
        }

        private HashSet<string> GetRequireComponentTypes(ClassDeclarationSyntax classDeclaration) {
            //unique types because RequireComponent may be specified any number of times with the same types
            var componentTypes = new HashSet<string>();

            //get all arguments to RequireComponent() attributes
            var requireComponentArgs = classDeclaration.AttributeLists.SelectMany(l => l.Attributes)
                                                       .Where(a => a.Name.ToString() == "RequireComponent")
                                                       .SelectMany(a => a.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>());

            //find all typeof(__) expressions in arguments and extract the type
            foreach (var arg in requireComponentArgs) {
                if (arg.Expression is TypeOfExpressionSyntax typeOf) {
                    //TODO does this need to be the fully-qualified name?
                    componentTypes.Add(typeOf.Type.ToString());
                }
            }

            return componentTypes;
        }

        private static string GetFullNamespace(SyntaxNode node) {
            var parts = new List<string>();
            var current = node.Parent;
            while (current is BaseNamespaceDeclarationSyntax namespaceDeclaration) {
                parts.Add(namespaceDeclaration.Name.ToString());
                current = namespaceDeclaration.Parent;
            }
            parts.Reverse();

            return string.Join(".", parts);
        }
    }
}
}