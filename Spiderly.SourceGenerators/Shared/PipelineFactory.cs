using Microsoft.CodeAnalysis;
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
            return MatchesCategories(classDeclaration, categories) ? classDeclaration : null;
        }

        private static bool MatchesCategories(ClassDeclarationSyntax classDeclaration, List<ClassCategoryCodes> categories)
        {
            string namespaceName = classDeclaration.GetNamespace();
            if (namespaceName == null)
                return false;

            if (namespaceName.EndsWith($".GeneratorSettings"))
                return true;

            if (categories.Any(category => namespaceName.EndsWith($".{category}")))
                return true;

            // Attribute-enrolled categories accept classes regardless of namespace — the semantic
            // pass (HasSpiderlyXxxAttribute() in each generator) filters by attribute name.
            if (HasAttributeEnrolledCategory(categories) && HasClassificationAttribute(classDeclaration))
                return true;

            return false;
        }

        private static bool HasAttributeEnrolledCategory(List<ClassCategoryCodes> categories)
        {
            foreach (ClassCategoryCodes category in categories)
            {
                if (category == ClassCategoryCodes.Entities ||
                    category == ClassCategoryCodes.DTO ||
                    category == ClassCategoryCodes.Controllers)
                    return true;
            }
            return false;
        }

        private static bool HasClassificationAttribute(ClassDeclarationSyntax classDeclaration)
        {
            foreach (AttributeListSyntax attrList in classDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attr in attrList.Attributes)
                {
                    string name = attr.Name.ToString();
                    if (name == "SpiderlyEntity" || name == "SpiderlyEntityAttribute" ||
                        name == "SpiderlyDTO" || name == "SpiderlyDTOAttribute" ||
                        name == "SpiderlyController" || name == "SpiderlyControllerAttribute")
                        return true;
                }
            }
            return false;
        }

        public static bool IsEnumSyntaxTargetForGeneration(SyntaxNode node)
        {
            if (node is EnumDeclarationSyntax enumDeclaration)
            {
                string namespaceName = enumDeclaration.GetNamespace();

                if (namespaceName != null && (namespaceName.EndsWith(".Enums") || namespaceName.EndsWith(".GeneratorSettings")))
                    return true;
            }

            return false;
        }

        public static EnumDeclarationSyntax GetEnumSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)context.Node;

            string namespaceName = enumDeclaration
               .Ancestors()
               .OfType<NamespaceDeclarationSyntax>()
               .Select(ns => ns.Name.ToString())
               .FirstOrDefault();

            if (namespaceName != null && (namespaceName.EndsWith(".Enums") || namespaceName.EndsWith(".GeneratorSettings")))
                return enumDeclaration;

            return null;
        }

        public static bool IsSyntaxTargetForGenerationEveryClass(SyntaxNode node)
        {
            if (node is ClassDeclarationSyntax classDeclaration)
            {
                string namespaceName = classDeclaration.GetNamespace();

                if (namespaceName != null)
                    return true;
            }

            return false;
        }

        public static ClassDeclarationSyntax GetSemanticTargetForGenerationEveryClass(GeneratorSyntaxContext context)
        {
            ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)context.Node;

            string namespaceName = classDeclaration.GetNamespace();

            if (namespaceName != null)
                return classDeclaration;

            return null;
        }

        #endregion

        #region Pipeline

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
