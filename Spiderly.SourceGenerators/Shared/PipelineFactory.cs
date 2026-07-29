using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class PipelineFactory
    {
        #region Syntax and Semantic targets

        public static IncrementalValuesProvider<ClassDeclarationSyntax> GetClassIncrementalValuesProvider(SyntaxValueProvider syntaxValueProvider, List<ClassCategoryCodes> categories)
        {
            return syntaxValueProvider
                .CreateSyntaxProvider(
                   predicate: (s, _) => IsClassSyntaxTargetForGeneration(s, categories),
                   transform: (ctx, _) => GetClassSemanticTargetForGeneration(ctx, categories))
                .Where(static c => c is not null);
        }

        public static bool IsClassSyntaxTargetForGeneration(SyntaxNode node, List<ClassCategoryCodes> categories)
        {
            return node is ClassDeclarationSyntax classDeclaration && MatchesCategories(classDeclaration, categories);
        }

        public static ClassDeclarationSyntax GetClassSemanticTargetForGeneration(GeneratorSyntaxContext context, List<ClassCategoryCodes> categories)
        {
            ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)context.Node;
            // Standard incremental-generator "transform" idiom: every caller of this method immediately
            // filters the resulting provider via `.Where(c => c is not null)`, so a null result here is
            // never dereferenced. Keeping the signature non-null avoids rippling a nullable annotation
            // through the incremental-generator generic pipeline types that consume it.
            return MatchesCategories(classDeclaration, categories) ? classDeclaration : null!;
        }

        private static bool MatchesCategories(ClassDeclarationSyntax classDeclaration, List<ClassCategoryCodes> categories)
        {
            foreach (ClassCategoryCodes category in categories)
            {
                if (HasAttributeByName(classDeclaration, GetMarkerAttributeName(category)))
                    return true;
            }

            // A hand-written `partial class {Entity}DTO` that extends a generated DTO is enrolled even without
            // [SpiderlyDTO], so the members it adds aren't silently dropped from codegen (spiderly#258). Scoped
            // to partial *DTO declarations; GetDTOClasses further narrows the merge to names that match a
            // generated DTO, so a standalone unmarked DTO still requires the marker.
            if (categories.Contains(ClassCategoryCodes.DTO) && IsPartialDtoClass(classDeclaration))
                return true;

            return false;
        }

        private static bool IsPartialDtoClass(ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Identifier.Text.EndsWith(Helpers.DTONamespaceEnding)
                && classDeclaration.IsPartial();
        }

        public static string GetMarkerAttributeName(ClassCategoryCodes category) => category switch
        {
            ClassCategoryCodes.Entities => "SpiderlyEntity",
            ClassCategoryCodes.DTO => "SpiderlyDTO",
            ClassCategoryCodes.Controllers => "SpiderlyController",
            ClassCategoryCodes.DataMappers => "SpiderlyDataMapper",
            ClassCategoryCodes.Services => "SpiderlyService",
            ClassCategoryCodes.Enums => "SpiderlyEnum",
            _ => throw new System.ArgumentOutOfRangeException(nameof(category), category, "No marker attribute is defined for this category."),
        };

        private static bool HasAttributeByName(ClassDeclarationSyntax classDeclaration, string attributeName)
        {
            string attributeNameWithSuffix = attributeName + "Attribute";
            foreach (AttributeListSyntax attrList in classDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attr in attrList.Attributes)
                {
                    string name = attr.Name.ToString();
                    if (name == attributeName || name == attributeNameWithSuffix)
                        return true;
                }
            }
            return false;
        }

        public static bool IsEnumSyntaxTargetForGeneration(SyntaxNode node)
        {
            return node is EnumDeclarationSyntax enumDeclaration
                && HasEnumAttribute(enumDeclaration, "SpiderlyEnum");
        }

        public static EnumDeclarationSyntax GetEnumSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)context.Node;
            // Same incremental-generator idiom as GetClassSemanticTargetForGeneration above: the caller
            // (NgEnumsGenerator) immediately filters via `.Where(c => c is not null)`.
            return IsEnumSyntaxTargetForGeneration(enumDeclaration) ? enumDeclaration : null!;
        }

        /// <summary>
        /// Collects the type names of every <c>[SpiderlyEnum]</c>-decorated enum in the current compilation,
        /// already projected to <see cref="ImmutableArray{T}"/> so consumers don't re-project per <c>Execute</c>.
        /// Generators that emit code for entity properties feed this into <see cref="SpiderlyClassFactory.GetSpiderlyClasses"/>
        /// so enum-typed properties get <c>SpiderlyProperty.IsEnum = true</c> instead of being misclassified as M2O navigation.
        /// </summary>
        public static IncrementalValueProvider<ImmutableArray<string>> GetSpiderlyEnumNamesProvider(SyntaxValueProvider syntaxValueProvider)
        {
            return syntaxValueProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsEnumSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => GetEnumSemanticTargetForGeneration(ctx))
                .Where(static c => c is not null)
                .Collect()
                .Select(static (enums, _) => enums.Select(e => e.Identifier.Text).ToImmutableArray());
        }

        private static bool HasEnumAttribute(EnumDeclarationSyntax enumDeclaration, string attributeName)
        {
            string attributeNameWithSuffix = attributeName + "Attribute";
            foreach (AttributeListSyntax attrList in enumDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attr in attrList.Attributes)
                {
                    string name = attr.Name.ToString();
                    if (name == attributeName || name == attributeNameWithSuffix)
                        return true;
                }
            }
            return false;
        }

        #endregion

        #region Pipeline

        /// <summary>
        /// The consumer compilation's nullable-reference-types context (the <c>&lt;Nullable&gt;</c> project
        /// setting), for the C#-emitting generators to key their output on: explicit <c>#nullable disable</c>
        /// + nullable-oblivious types for an oblivious consumer, <c>#nullable enable</c> + propagated
        /// annotations once the consumer turns NRT on. Combine it onto a pipeline the same way
        /// <see cref="GetSpiderlyEnumNamesProvider"/> is combined — only the generators that need it pay for it.
        /// <para>
        /// Cheap to depend on despite reading <c>CompilationProvider</c> (which changes on every keystroke):
        /// the selected value is a plain enum, so the default comparer collapses it back to <c>Cached</c>
        /// unless the project's <c>&lt;Nullable&gt;</c> setting itself changed. No custom comparer needed —
        /// unlike the referenced-class list, whose <c>List&lt;T&gt;</c> reference equality forces one.
        /// </para>
        /// </summary>
        public static IncrementalValueProvider<NullableContextOptions> GetNullableContextProvider(IncrementalGeneratorInitializationContext context)
        {
            return context.CompilationProvider.Select(static (compilation, _) =>
                compilation.Options is CSharpCompilationOptions csharpOptions
                    ? csharpOptions.NullableContextOptions
                    : NullableContextOptions.Disable);
        }

        /// <summary>
        /// Creates a standard generator pipeline: namespace-filtered class declarations + referenced assemblies + spiderly config.
        /// </summary>
        public static IncrementalValueProvider<((ImmutableArray<ClassDeclarationSyntax> Classes, List<SpiderlyClass> ReferencedClasses), SpiderlyConfig Config)> CreatePipeline(
            IncrementalGeneratorInitializationContext context,
            List<ClassCategoryCodes> syntaxCategories,
            List<ClassCategoryCodes> referencedCategories)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = GetClassIncrementalValuesProvider(context.SyntaxProvider, syntaxCategories);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = ReferencedAssemblyAnalyzer.GetIncrementalValueProviderClassesFromReferencedAssemblies(context, referencedCategories);

            IncrementalValueProvider<SpiderlyConfig> config = context.GetSpiderlyConfig();

            return classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(config);
        }

        /// <summary>
        /// Creates a generator pipeline with callingPath: namespace-filtered class declarations + referenced assemblies + callingPath + spiderly config.
        /// </summary>
        public static IncrementalValueProvider<(((ImmutableArray<ClassDeclarationSyntax> Classes, List<SpiderlyClass> ReferencedClasses), string CallingPath), SpiderlyConfig Config)> CreatePipelineWithCallingPath(
            IncrementalGeneratorInitializationContext context,
            List<ClassCategoryCodes> syntaxCategories,
            List<ClassCategoryCodes> referencedCategories)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = GetClassIncrementalValuesProvider(context.SyntaxProvider, syntaxCategories);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = ReferencedAssemblyAnalyzer.GetIncrementalValueProviderClassesFromReferencedAssemblies(context, referencedCategories);

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();
            IncrementalValueProvider<SpiderlyConfig> config = context.GetSpiderlyConfig();

            return classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory)
                .Combine(config);
        }

        #endregion
    }
}
