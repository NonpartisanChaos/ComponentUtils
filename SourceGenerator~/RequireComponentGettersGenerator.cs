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

        foreach (var info in receiver.ClassInfoByFullName.Values) {
            var hasNamespace = info.ClassNamespace.Length > 0;

            //if there are any diagnostic issues, report them and maybe skip generating for this class
            if (info.Diagnostics.Count > 0) {
                var shouldSkip = false;
                foreach (var diagnostic in info.Diagnostics) {
                    shouldSkip |= diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.IsWarningAsError;
                    context.ReportDiagnostic(diagnostic);
                }

                if (shouldSkip) {
                    continue;
                }
            }

            //any type that has a ComponentTypeArgument requires:
            // - a generated [RequireComponent(...)] attribute
            // - a custom generated getter
            // - no conflicting auto-named property getters
            info.ComponentTypes.RemoveWhere(t => info.ComponentTypeArguments.ContainsKey(t));

            var source = $@"
{GenerateAllUsingSource(info.ClassDeclaration)}

{(hasNamespace ? $"namespace {info.ClassNamespace} {{" : "")}
{GenerateAllNamedRequireComponentSource(info.ComponentTypeArguments)}
public partial class {info.ClassName} {{
{(info.GettersArguments.HasValue ? GenerateAllDefaultGettersSource(info.GettersArguments.Value, info.ComponentTypes) : "")}
{GenerateAllNamedGettersSource(info.ComponentTypeArguments)}
}}
{(hasNamespace ? "}" : "")}
";

            context.AddSource($"{info.ClassName}.Getters.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generate code that exactly duplicates all using statements from the specified class' file.
    /// This handles namespaces, aliases and fully-qualified names.
    /// </summary>
    private static string GenerateAllUsingSource(ClassDeclarationSyntax classDeclaration) {
        var root = classDeclaration.SyntaxTree.GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().OrderBy(u => u.SpanStart);

        return string.Join(Environment.NewLine, usings.Select(u => u.ToString()));
    }

    /// <summary>
    /// Generate code that adds a cached property with the specified visibility, type, and name.
    /// </summary>
    private static string GenerateGetterSource(string visibility, string typeName, string generatedPropertyName) {
        //"MyCoolComponent" -> "_myCoolComponent"
        var cacheVarName = $"_{char.ToLowerInvariant(generatedPropertyName[0])}{generatedPropertyName.Substring(1)}";

        return $@"
    private {typeName} {cacheVarName};
    {visibility} {typeName} {generatedPropertyName} => {cacheVarName} ??= GetComponent<{typeName}>();
";
    }

    private string GenerateAllDefaultGettersSource(in RequireComponentGettersArguments arguments, IEnumerable<string> componentTypes) {
        var sb = new StringBuilder();
        foreach (var componentType in componentTypes) {
            //automatically generate a name based on the type name, trimming the namespace
            //"Some.Name.Space.MyCoolComponent" -> "MyCoolComponent"
            var generatedPropertyName = componentType.Substring(componentType.LastIndexOf('.') + 1);

            sb.Append(GenerateGetterSource(arguments.Visibility, componentType, generatedPropertyName));
        }

        return sb.ToString();
    }

    private string GenerateAllNamedGettersSource(Dictionary<string, RequireComponentGetterArguments> typeArguments) {
        var sb = new StringBuilder();
        foreach (var entry in typeArguments) {
            var componentType = entry.Key;
            var arguments = entry.Value;

            sb.Append(GenerateGetterSource(arguments.Visibility, componentType, arguments.Name));
        }

        return sb.ToString();
    }

    private string GenerateAllNamedRequireComponentSource(Dictionary<string, RequireComponentGetterArguments> typeArguments) {
        var sb = new StringBuilder();
        foreach (var componentType in typeArguments.Keys) {
            sb.Append($"[RequireComponent(typeof({componentType}))]\n");
        }

        return sb.ToString();
    }

    private struct RequireComponentGettersArguments {
        public string Visibility { get; set; }
    }

    private struct RequireComponentGetterArguments {
        public string Name { get; set; }
        public string Visibility { get; set; }
    }

    /// <summary>
    /// Holds all RequireComponent, RequireComponentGetter and RequireComponentGetters info for a single class.
    /// </summary>
    private class ClassInfo {
        public ClassDeclarationSyntax ClassDeclaration { get; }
        public string ClassNamespace { get; }
        public string ClassName { get; }

        //all RequireComponent types for all attributes on the class
        public HashSet<string> ComponentTypes { get; }

        //only relevant if [RequireComponentGetters] is present
        public RequireComponentGettersArguments? GettersArguments { get; set; }

        //only relevant if one or more [RequireComponentGetter(...)] is present
        public Dictionary<string, RequireComponentGetterArguments> ComponentTypeArguments { get; } = new();

        public List<Diagnostic> Diagnostics { get; } = new();

        public ClassInfo(ClassDeclarationSyntax classDeclaration, string classNamespace, string className) {
            ClassDeclaration = classDeclaration;
            ClassNamespace = classNamespace;
            ClassName = className;

            ComponentTypes = GetRequireComponentTypes(classDeclaration);
        }

        private static HashSet<string> GetRequireComponentTypes(ClassDeclarationSyntax classDeclaration) {
            //unique types because RequireComponent may be specified any number of times with the same types
            var componentTypes = new HashSet<string>();

            //get all arguments to RequireComponent() attributes
            var requireComponentArgs = classDeclaration.AttributeLists.SelectMany(l => l.Attributes)
                                                       .Where(a => a.Name.ToString() == "RequireComponent")
                                                       .SelectMany(a => a.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>());

            //find all typeof(__) expressions in arguments and extract the type
            foreach (var arg in requireComponentArgs) {
                if (arg.Expression is TypeOfExpressionSyntax typeOf) {
                    componentTypes.Add(typeOf.Type.ToString());
                }
            }

            return componentTypes;
        }
    }

    private class SyntaxReceiver : ISyntaxReceiver {
        public Dictionary<string, ClassInfo> ClassInfoByFullName { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            //only consider attributes attached to a class
            if (syntaxNode is AttributeSyntax attribute
                && attribute.Parent is AttributeListSyntax attributeList
                && attributeList.Parent is ClassDeclarationSyntax classDeclaration) {
                if (attribute.Name.ToString() == "RequireComponentGetters") {
                    //[RequireComponentGetters] attribute
                    var info = GetOrCreateClassInfo(classDeclaration);
                    AddRequireComponentGettersArguments(info, attribute);
                } else if (attribute.Name.ToString() == "RequireComponentGetter") {
                    //[RequireComponentGetter(...)] attribute
                    var info = GetOrCreateClassInfo(classDeclaration);
                    AddRequireComponentGetterInfo(info, attribute);
                }
            }
        }

        private ClassInfo GetOrCreateClassInfo(ClassDeclarationSyntax classDeclaration) {
            var classNamespace = GetFullNamespace(classDeclaration);
            var className = classDeclaration.Identifier.Text;
            var classFullName = $"{classNamespace}.{className}";

            if (!ClassInfoByFullName.TryGetValue(classFullName, out var info)) {
                info = new ClassInfo(classDeclaration, classNamespace, className);
                ClassInfoByFullName.Add(classFullName, info);
            }

            return info;
        }

        private void AddRequireComponentGettersArguments(ClassInfo info, AttributeSyntax attribute) {
            //1st argument "visibility"
            var visibilityArgument = GetAttributeArgumentByNameOrIndex(attribute, "visibility", 0);

            //NOTE - default value must exactly match RequireComponentGettersAttribute argument default
            var visibility = visibilityArgument?.Expression is LiteralExpressionSyntax literalExpression ? literalExpression.Token.ValueText : "public";

            info.GettersArguments = new RequireComponentGettersArguments {
                Visibility = visibility,
            };
        }

        private void AddRequireComponentGetterInfo(ClassInfo info, AttributeSyntax attribute) {
            //1st argument "type" (required)
            var typeArgument = GetAttributeArgumentByNameOrIndex(attribute, "type", 0);
            if (typeArgument == null || typeArgument.Expression is not TypeOfExpressionSyntax typeOfExpression) {
                var desc = new DiagnosticDescriptor(
                    id: "RCG001",
                    title: "Malformed RequireComponentGetter type argument",
                    messageFormat: "Type argument must be a typeof() expression: {0}",
                    category: "ComponentUtils",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true);
                info.Diagnostics.Add(Diagnostic.Create(desc, attribute.GetLocation(), attribute.ToString()));
                return;
            }

            //2nd argument "name" (required)
            var nameArgument = GetAttributeArgumentByNameOrIndex(attribute, "name", 1);
            if (nameArgument == null || nameArgument.Expression is not LiteralExpressionSyntax nameLiteralExpression) {
                var desc = new DiagnosticDescriptor(
                    id: "RCG002",
                    title: "Malformed RequireComponentGetter type argument",
                    messageFormat: "Name argument must be a literal string: {0}",
                    category: "ComponentUtils",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true);
                info.Diagnostics.Add(Diagnostic.Create(desc, attribute.GetLocation(), attribute.ToString()));
                return;
            }

            //3rd argument "visibility" (optional)
            var visibilityArgument = GetAttributeArgumentByNameOrIndex(attribute, "visibility", 2);

            //required argument values
            var type = typeOfExpression.Type.ToString();
            var name = nameLiteralExpression.Token.ValueText;

            //optional argument values
            //NOTE - default value must exactly match RequireComponentGetterAttribute argument default
            var visibility = visibilityArgument?.Expression is LiteralExpressionSyntax literalExpression ? literalExpression.Token.ValueText : "public";

            //add info
            info.ComponentTypeArguments[type] = new RequireComponentGetterArguments {
                Name = name,
                Visibility = visibility,
            };
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

        private static AttributeArgumentSyntax? GetAttributeArgumentByNameOrIndex(AttributeSyntax attribute, string name, int index) {
            //find the argument by name
            var arg = attribute.ArgumentList?.Arguments.FirstOrDefault(a => a.NameColon?.Name.ToString() == name);

            //couldn't find the argument by name, try by index
            //NOTE - named arguments cannot come before indexed arguments, so searching by name first is always correct
            if (arg == null && attribute.ArgumentList?.Arguments.Count > index) {
                arg = attribute.ArgumentList.Arguments[index];
            }

            return arg;
        }
    }
}
}