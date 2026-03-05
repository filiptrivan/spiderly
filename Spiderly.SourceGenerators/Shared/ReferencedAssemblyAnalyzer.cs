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
        public static IncrementalValueProvider<List<SpiderlyClass>> GetIncrementalValueProviderClassesFromReferencedAssemblies(IncrementalGeneratorInitializationContext context, List<NamespaceExtensionCodes> namespaceExtensions)
        {
            return context.CompilationProvider
                .Select((compilation, _) =>
                {
                    List<SpiderlyClass> classes = new();

                    foreach (IAssemblySymbol referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
                    {
                        classes.AddRange(GetClassesFromReferencedAssemblies(referencedAssembly.GlobalNamespace, namespaceExtensions));
                    }

                    return classes
                        .OrderBy(c => c.Name)
                        .ToList();
                });
        }

        private static List<SpiderlyClass> GetClassesFromReferencedAssemblies(INamespaceSymbol namespaceSymbol, List<NamespaceExtensionCodes> namespaceExtensions)
        {
            List<SpiderlyClass> classes = new();

            List<INamedTypeSymbol> types = namespaceSymbol.GetTypeMembers()
                .Where(type => type.TypeKind == TypeKind.Class &&
                       namespaceExtensions.Any(namespaceExtension => GetFullNamespace(type).EndsWith($".{namespaceExtension}")) &&
                       (!GetFullNamespace(type).EndsWith($".{NamespaceExtensionCodes.Entities}") || IsSpiderlyEntity(type)))
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
                classes.AddRange(GetClassesFromReferencedAssemblies(nestedNamespace, namespaceExtensions));
            }

            return classes;
        }

        private static bool IsSpiderlyEntity(INamedTypeSymbol type)
        {
            INamedTypeSymbol current = type.BaseType;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                string name = current.OriginalDefinition?.Name;
                if (name == "BusinessObject" || name == "ReadonlyObject")
                    return true;
                current = current.BaseType;
            }

            // M2M join entities inherit directly from object but carry Spiderly attributes (e.g. [M2M]).
            // Third-party entities (e.g. Hangfire) have no Spiderly attributes.
            return type.GetAttributes().Any(attr =>
                attr.AttributeClass?.ContainingAssembly?.Name?.StartsWith("Spiderly") == true);
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

        public static List<string> GetEntityClassesUsings(List<SpiderlyClass> referencedProjectEntities)
        {
            List<string> namespaces = referencedProjectEntities
                .Where(x => x.Namespace.EndsWith(".Entities"))
                .Select(x => $"using {x.Namespace};")
                .OrderBy(x => x)
                .Distinct()
                .ToList();

            return namespaces;
        }

        public static List<string> GetDTOClassesUsings(List<SpiderlyClass> referencedProjectEntities)
        {
            List<string> namespaces = referencedProjectEntities
                .Where(x => x.Namespace.EndsWith(".Entities"))
                .Select(x => $"using {x.Namespace.Replace(".Entities", ".DTO")};")
                .OrderBy(x => x)
                .Distinct()
                .ToList();

            return namespaces;
        }
    }
}
