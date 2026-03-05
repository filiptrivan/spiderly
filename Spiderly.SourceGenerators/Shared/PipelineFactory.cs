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

        public static IncrementalValuesProvider<ClassDeclarationSyntax> GetClassIncrementalValuesProvider(SyntaxValueProvider syntaxValueProvider, List<NamespaceExtensionCodes> namespaceExtensions)
        {
            return syntaxValueProvider
                .CreateSyntaxProvider(
                   predicate: (s, _) => IsClassSyntaxTargetForGeneration(s, namespaceExtensions),
                   transform: (ctx, _) => GetClassSemanticTargetForGeneration(ctx, namespaceExtensions))
                .Where(static c => c is not null);
        }

        public static bool IsClassSyntaxTargetForGeneration(SyntaxNode node, List<NamespaceExtensionCodes> namespaceExtensions)
        {
            if (node is ClassDeclarationSyntax classDeclaration)
            {
                string namespaceName = classDeclaration.GetNamespace();

                if (namespaceName != null && (namespaceExtensions.Any(namespaceExtension => namespaceName.EndsWith($".{namespaceExtension}")) || namespaceName.EndsWith($".GeneratorSettings")))
                    return true;
            }

            return false;
        }

        public static ClassDeclarationSyntax GetClassSemanticTargetForGeneration(GeneratorSyntaxContext context, List<NamespaceExtensionCodes> namespaceExtensions)
        {
            ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)context.Node;

            string namespaceName = classDeclaration.GetNamespace();

            if (namespaceName != null && (namespaceExtensions.Any(namespaceExtension => namespaceName.EndsWith($".{namespaceExtension}")) || namespaceName.EndsWith($".GeneratorSettings")))
                return classDeclaration;

            return null;
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
            List<NamespaceExtensionCodes> syntaxNamespaces,
            List<NamespaceExtensionCodes> referencedNamespaces)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = GetClassIncrementalValuesProvider(context.SyntaxProvider, syntaxNamespaces);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = ReferencedAssemblyAnalyzer.GetIncrementalValueProviderClassesFromReferencedAssemblies(context, referencedNamespaces);

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
            List<NamespaceExtensionCodes> syntaxNamespaces,
            List<NamespaceExtensionCodes> referencedNamespaces)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = GetClassIncrementalValuesProvider(context.SyntaxProvider, syntaxNamespaces);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = ReferencedAssemblyAnalyzer.GetIncrementalValueProviderClassesFromReferencedAssemblies(context, referencedNamespaces);

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
