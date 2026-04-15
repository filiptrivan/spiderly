using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class ReferencedAssemblyAnalyzer
    {
        public static IncrementalValueProvider<List<SpiderlyClass>> GetIncrementalValueProviderClassesFromReferencedAssemblies(IncrementalGeneratorInitializationContext context, List<ClassCategoryCodes> categories)
        {
            return context.CompilationProvider
                .Select((compilation, _) =>
                {
                    List<SpiderlyClass> classes = new();

                    foreach (IAssemblySymbol referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
                    {
                        classes.AddRange(GetClassesFromReferencedAssemblies(referencedAssembly.GlobalNamespace, categories));
                    }

                    return classes
                        .OrderBy(c => c.Name)
                        .ToList();
                });
        }

        private static List<SpiderlyClass> GetClassesFromReferencedAssemblies(INamespaceSymbol namespaceSymbol, List<ClassCategoryCodes> categories)
        {
            List<SpiderlyClass> classes = new();

            List<INamedTypeSymbol> types = namespaceSymbol.GetTypeMembers()
                .Where(type => type.TypeKind == TypeKind.Class && IsRequestedClass(type, categories))
                .OrderBy(type => type.Name)
                .ToList();

            foreach (INamedTypeSymbol type in types)
            {
                List<SpiderlyAttribute> attributes = GetAttributesFromReferencedAssemblies(type);

                SpiderlyClass spiderClass = new SpiderlyClass
                {
                    Name = type.Name,
                    Namespace = GetFullNamespace(type),
                    BaseType = type.BaseType?.TypeToDisplayString() == "object" ? null : type.BaseType?.TypeToDisplayString(),
                    IsAbstract = type.IsAbstract,
                    ControllerName = attributes.Where(x => x.Name == "Controller").Select(x => x.Value).SingleOrDefault() ?? type.Name,
                    Properties = GetPropertiesFromReferencedAssemblies(type),
                    Attributes = attributes,
                    Methods = GetMethodsOfCurrentClassFromReferencedAssemblies(type),
                };

                classes.Add(spiderClass);
            }

            foreach (INamespaceSymbol nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                classes.AddRange(GetClassesFromReferencedAssemblies(nestedNamespace, categories));
            }

            return classes;
        }

        private static bool IsRequestedClass(INamedTypeSymbol type, List<ClassCategoryCodes> categories)
        {
            System.Collections.Immutable.ImmutableArray<AttributeData> attrs = default;

            foreach (ClassCategoryCodes category in categories)
            {
                string markerAttribute = PipelineFactory.GetMarkerAttributeName(category);
                bool match = markerAttribute != null
                    ? HasAttributeByName(ref attrs, type, markerAttribute + "Attribute")
                    : GetFullNamespace(type).EndsWith($".{category}");

                if (match)
                    return true;
            }

            return false;
        }

        private static bool HasAttributeByName(ref System.Collections.Immutable.ImmutableArray<AttributeData> attrs, INamedTypeSymbol type, string attributeTypeName)
        {
            if (attrs.IsDefault)
                attrs = type.GetAttributes();
            return attrs.Any(a => a.AttributeClass?.Name == attributeTypeName);
        }

        private static string GetFullNamespace(INamedTypeSymbol symbol)
        {
            Stack<string> namespaces = new Stack<string>();
            INamespaceSymbol currentNamespace = symbol.ContainingNamespace;

            while (currentNamespace != null && !currentNamespace.IsGlobalNamespace)
            {
                namespaces.Push(currentNamespace.Name);
                currentNamespace = currentNamespace.ContainingNamespace;
            }

            return string.Join(".", namespaces);
        }

        private static List<SpiderlyProperty> GetPropertiesFromReferencedAssemblies(INamedTypeSymbol type)
        {
            List<SpiderlyProperty> properties = new();

            while (type != null)
            {
                foreach (IPropertySymbol propertySymbol in type.GetMembers().OfType<IPropertySymbol>())
                {
                    if (propertySymbol.ExplicitInterfaceImplementations.Any())
                        continue;

                    if (propertySymbol.Type.TypeKind == TypeKind.Interface)
                        continue;

                    SpiderlyProperty property = new SpiderlyProperty
                    {
                        Type = propertySymbol.Type.TypeToDisplayString(),
                        Name = propertySymbol.Name,
                        EntityName = type.Name,
                        Attributes = GetAttributesFromReferencedAssemblies(propertySymbol),
                    };

                    properties.Add(property);
                }

                type = type.BaseType;
            }

            return properties.OrderBy(x => x.Name).ToList();
        }

        private static List<SpiderlyAttribute> GetAttributesFromReferencedAssemblies(ISymbol symbol)
        {
            List<SpiderlyAttribute> attributes = [];

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                string attributeName = attribute.AttributeClass.Name?.Replace("Attribute", "");

                string argumentValue = null;

                if (attribute.ConstructorArguments.Length > 0)
                {
                    if (attributeName == "StringLength")
                    {
                        List<string> parts = new List<string>
                        {
                            attribute.ConstructorArguments[0].Value?.ToString() // Max length
                        };

                        var minLengthArg = attribute.NamedArguments.FirstOrDefault(na => na.Key == "MinimumLength");

                        if (minLengthArg.Key != null)
                        {
                            parts.Add($"MinimumLength={minLengthArg.Value.Value}");
                        }

                        argumentValue = string.Join(", ", parts);
                    }
                    else
                    {
                        argumentValue = attribute.ConstructorArguments.Length > 0
                        ?
                        string.Join(", ", attribute.ConstructorArguments.Select(arg =>
                        {
                            try
                            {
                                return arg.Value?.ToString();
                            }
                            catch (Exception)
                            {
                                return arg.Values.FirstOrDefault().Value?.ToString();
                            }
                        }))
                        : null; // FT: Doing this because of Range(0, 5) (long tail because of null pointer exception)
                    }
                }

                argumentValue = ClassAnalyzer.GetFormatedAttributeValue(argumentValue);

                SpiderlyAttribute spiderAttribute = new SpiderlyAttribute
                {
                    Name = attributeName,
                    Value = argumentValue
                };

                attributes.Add(spiderAttribute);
            }

            return attributes.OrderBy(x => x.Name).ToList();
        }

        /// <summary>
        /// Cant get method Body and method DescendantNodes from referenced assemblies
        /// </summary>
        private static List<SpiderlyMethod> GetMethodsOfCurrentClassFromReferencedAssemblies(INamedTypeSymbol type)
        {
            List<SpiderlyMethod> methods = [];

            foreach (IMethodSymbol methodSymbol in type.GetMembers().OfType<IMethodSymbol>())
            {
                SpiderlyMethod method = new SpiderlyMethod
                {
                    Name = methodSymbol.Name,
                    ReturnType = methodSymbol.ReturnType.ToString(),
                    Attributes = GetAttributesFromReferencedAssemblies(methodSymbol),
                };

                methods.Add(method);
            }

            return methods.OrderBy(x => x.Name).ToList();
        }

        public static List<string> GetClassesUsings(IEnumerable<SpiderlyClass> classes)
        {
            return classes
                .Select(x => $"using {x.Namespace};")
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }
    }
}
