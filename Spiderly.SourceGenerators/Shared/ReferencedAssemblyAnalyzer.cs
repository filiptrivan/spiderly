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
        /// <summary>
        /// Renders a type symbol from a referenced assembly into the same string the in-project
        /// syntax path produces (<see cref="Extensions.GetBaseType"/> / <c>TypeSyntax.ToString()</c>):
        /// unqualified name, C# keyword primitives (<c>long</c>, not <c>Int64</c>), generic arguments
        /// intact (<c>BusinessObject&lt;long&gt;</c>, <c>List&lt;Foo&gt;</c>), nullable value types as
        /// <c>long?</c>. Keeping both paths aligned lets everything downstream
        /// (<see cref="Extensions.GetIdType"/>, <c>ExtractTypeFromGenericType</c>, …) be path-agnostic.
        /// <para>
        /// This replaces the old <c>symbol.ToString().Split('.').Last().Replace("&gt;", "")</c> munging,
        /// which dropped the closing generic bracket (turning <c>BusinessObject&lt;long&gt;</c> into the
        /// malformed <c>BusinessObject&lt;long</c> and triggering a false SPIDERLY018) and collapsed
        /// <c>List&lt;Foo&gt;</c> to <c>Foo</c> because the fully-qualified <c>System.Collections.Generic.List</c>
        /// failed the bare-<c>List</c> collection check.
        /// </para>
        /// <para>
        /// Nullable <i>reference</i> annotations are intentionally NOT emitted (no
        /// <c>IncludeNullableReferenceTypeModifier</c>): the old <c>ToString()</c> path stripped them, so a
        /// nullable navigation <c>Brand?</c> rendered as <c>Brand</c>, and many downstream lookups match an entity
        /// name against the raw property type by exact equality (<c>x.Name == property.Type.Raw</c>). Emitting the
        /// <c>?</c> would turn those into <c>Single(...)</c> "no matching element" crashes. Nullable <i>value</i>
        /// types (<c>long?</c>) are unaffected — that is intrinsic to <see cref="System.Nullable{T}"/> rendering,
        /// not the NRT modifier — so primary-key / scalar handling stays correct.
        /// </para>
        /// </summary>
        private static readonly SymbolDisplayFormat ReferencedTypeDisplayFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private static string? ToDisplayName(ITypeSymbol type) => type?.ToDisplayString(ReferencedTypeDisplayFormat);

        public static IncrementalValueProvider<List<SpiderlyClass>> GetIncrementalValueProviderClassesFromReferencedAssemblies(IncrementalGeneratorInitializationContext context, List<ClassCategoryCodes> categories)
        {
            return context.CompilationProvider
                .Select((compilation, _) => GetClassesFromCompilation(compilation, categories))
                .WithComparer(ReferencedSpiderlyClassListComparer.Instance);
        }

        /// <summary>
        /// Builds <see cref="SpiderlyClass"/> models for every <paramref name="categories"/>-matching class in the
        /// referenced assemblies of <paramref name="compilation"/> (the "metadata path" — entities living in a
        /// referenced project rather than the one being compiled). Pulled out of the incremental pipeline lambda so
        /// this path is directly unit-testable; it's the path that produced the SPIDERLY018 false-positive when an
        /// entity's generic base (<c>BusinessObject&lt;long&gt;</c>) was mangled during symbol-to-string conversion.
        /// </summary>
        public static List<SpiderlyClass> GetClassesFromCompilation(Compilation compilation, List<ClassCategoryCodes> categories)
        {
            List<SpiderlyClass> classes = new();

            foreach (IAssemblySymbol referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                classes.AddRange(GetClassesFromReferencedAssemblies(referencedAssembly.GlobalNamespace, categories));
            }

            return classes
                .OrderBy(c => c.Name)
                .ToList();
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
                    BaseType = type.BaseType == null || type.BaseType.SpecialType == SpecialType.System_Object
                        ? null
                        : ToDisplayName(type.BaseType),
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
                if (HasAttributeByName(ref attrs, type, PipelineFactory.GetMarkerAttributeName(category) + "Attribute"))
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

        private static List<SpiderlyProperty> GetPropertiesFromReferencedAssemblies(INamedTypeSymbol? type)
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
                        // IPropertySymbol.Type is never null, so ToDisplayName's defensive `?.` always succeeds here.
                        Type = SpiderlyTypeRef.Parse(ToDisplayName(propertySymbol.Type))!,
                        Name = propertySymbol.Name,
                        EntityName = type.Name,
                        IsEnum = IsSpiderlyEnumType(propertySymbol.Type),
                        Attributes = GetAttributesFromReferencedAssemblies(propertySymbol),
                    };

                    properties.Add(property);
                }

                type = type.BaseType;
            }

            return properties.OrderBy(x => x.Name).ToList();
        }

        // Mirrors ClassAnalyzer.GetPropsOfCurrentClass's enum tagging for properties
        // sourced from a referenced assembly: a property whose underlying type is an
        // enum decorated with [SpiderlyEnum] is a scalar value, not a M2O navigation.
        // Without this flag, generators that skip enum-typed properties (autocomplete
        // / dropdown controller methods, DTO M2O field synthesis) misclassify them
        // and ControllerGenerator throws SPIDERLY011 trying to resolve the "entity".
        // Handles both <c>EventKind</c> and <c>EventKind?</c> shapes.
        private static bool IsSpiderlyEnumType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named
                && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                && named.TypeArguments.Length == 1)
            {
                type = named.TypeArguments[0];
            }

            if (type.TypeKind != TypeKind.Enum)
                return false;

            return type.GetAttributes().Any(a => a.AttributeClass?.Name == "SpiderlyEnumAttribute");
        }

        private static List<SpiderlyAttribute> GetAttributesFromReferencedAssemblies(ISymbol symbol)
        {
            List<SpiderlyAttribute> attributes = [];

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                string? attributeName = attribute.AttributeClass?.Name?.Replace("Attribute", "");

                string? argumentValue = null;

                if (attribute.ConstructorArguments.Length > 0)
                {
                    if (attributeName == "StringLength")
                    {
                        List<string?> parts = new List<string?>
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
                    // TODO(nrt): AttributeClass can be null for an erroneous/broken attribute reference
                    // (Roslyn's documented behavior for a symbol-resolution failure), which would make
                    // attributeName null here too. Pre-existing gap, not fixing under this task.
                    Name = attributeName!,
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
                    ReturnType = ToDisplayName(methodSymbol.ReturnType)!, // IMethodSymbol.ReturnType is never null.
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
