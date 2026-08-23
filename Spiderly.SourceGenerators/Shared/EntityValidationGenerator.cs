using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Runs every entity-level Spiderly diagnostic (<see cref="SpiderlyEntityValidator"/>) and emits NO
    /// source. It lives here rather than under <c>Net/</c> or <c>Angular/</c> because it has no output
    /// target — those folders are grouped by what a generator emits, and this one emits nothing.
    /// <para>
    /// Deliberately NOT gated by <c>SpiderlyConfig.IsGeneratorEnabled</c>. That switch exists so a consumer
    /// can decline generated ARTIFACTS they do not want; declining an artifact must not also decline the
    /// checks that say their entities are well-formed. Hosting these diagnostics inside
    /// <c>MapperGenerator</c> meant exactly that — see <see cref="SpiderlyEntityValidator"/> for the three
    /// measured holes that motivated the move.
    /// </para>
    /// <para>
    /// One diagnostic per bad entity per build, and a bad entity never suppresses the others: the validator
    /// throws on its first finding and this catches per entity and continues.
    /// </para>
    /// </summary>
    [Generator]
    public class EntityValidationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Entities only: this generator reads nothing else, and collecting DataMappers as well (which
            // MapperGenerator must, and which this was copied from) would re-validate every entity on any
            // edit to a mapper file.
            var combined = PipelineFactory.CreatePipeline(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities });

            var combinedWithEnums = combined
                .Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider))
                .Combine(PipelineFactory.GetNullableContextProvider(context));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var ((combinedSource, enumNames), nullableContext) = source;
                var ((classes, referencedClasses), _) = combinedSource;
                Execute(classes, referencedClasses, enumNames, nullableContext, spc);
            });
        }

        private static void Execute(
            IList<ClassDeclarationSyntax> classes,
            List<SpiderlyClass> referencedProjectClasses,
            ImmutableArray<string> spiderlyEnumNames,
            NullableContextOptions nullableContext,
            SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();

            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                try
                {
                    SpiderlyEntityValidator.Validate(entity, currentProjectEntities, nullableContext);
                }
                catch (SpiderlyGenerationException ex)
                {
                    context.ReportDiagnostic(ex.Diagnostic);
                }
            }

            // SPIDERLY030 is model-wide rather than per-entity (it compares blob key prefixes
            // ACROSS entities), so it runs once after the loop instead of inside it. It is hosted
            // here for the same two reasons as everything above: a consumer disabling the Services
            // generator must not also disable the check, and reporting rather than throwing
            // mid-emission keeps one bad prefix from suppressing every generated service and
            // burying the diagnostic under cascading CS0246s.
            // Referenced-project entities are included: a prefix collision across the project
            // boundary is still a collision, and both sides' blobs are cleaned by prefix listing.
            try
            {
                BlobKeyPrefixValidator.Validate(currentProjectEntities
                    .Concat(referencedProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()))
                    .ToList());
            }
            catch (SpiderlyGenerationException ex)
            {
                context.ReportDiagnostic(ex.Diagnostic);
            }
        }
    }
}
