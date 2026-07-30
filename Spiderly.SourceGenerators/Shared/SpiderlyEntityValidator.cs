using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// The single entry point for validating one entity's shape. Every entity-level Spiderly diagnostic
    /// runs from here, exactly once per entity, and is driven by
    /// <see cref="EntityValidationGenerator"/> — never from an artifact generator.
    /// <para>
    /// That separation is the point. These diagnostics used to be called from <c>MapperGenerator</c>, where
    /// three things went wrong, each measured before this was written:
    /// <list type="number">
    /// <item><description>They sat behind <c>IsGeneratorEnabled(nameof(MapperGenerator))</c>, so one line in
    /// <c>.spiderly/config.json</c> silently disabled every entity diagnostic Spiderly has.</description></item>
    /// <item><description>Two of the three were reached only through <c>GetToDTOConfig</c>, which
    /// early-returns on <c>HasCustomPair</c> — so hand-writing all three <c>*ToDTOConfig</c> pairs exempted
    /// an entity from foreign-key and one-to-one validation entirely.</description></item>
    /// <item><description>That helper runs three times per entity, so both ran 3x. Note this redundancy was
    /// what kept (2) from firing on a single custom pair: collapsing it to once per entity would have WIDENED
    /// that hole had validation stayed inside the mapper.</description></item>
    /// </list>
    /// A correctness diagnostic must not be switchable by a codegen toggle, skippable by hand-writing a
    /// mapper, or priced per artifact.
    /// </para>
    /// <para>
    /// Throws <see cref="SpiderlyGenerationException"/> on the FIRST finding, so an entity reports one
    /// diagnostic per build even when it has several problems. Callers catch per entity and continue, so one
    /// bad entity never hides the rest of them.
    /// </para>
    /// </summary>
    public static class SpiderlyEntityValidator
    {
        /// <param name="entity">The entity to validate.</param>
        /// <param name="entities">Entities the cross-entity checks resolve against (FK and one-to-one shapes
        /// read the other side of a relationship).</param>
        /// <param name="nullableContext">The consumer compilation's nullable context. Annotation-based checks
        /// are skipped entirely when annotations are disabled — see
        /// <see cref="NullabilityValidator"/>.</param>
        public static void Validate(SpiderlyClass entity, List<SpiderlyClass> entities, NullableContextOptions nullableContext)
        {
            NullabilityValidator.ValidateEntity(entity, nullableContext);
            ForeignKeyValidator.ValidateEntity(entity, entities);
            OneToOneValidator.ValidateEntity(entity, entities);
            CommaSeparatedDisplayNameValidator.ValidateEntity(entity, entities);
        }
    }
}
