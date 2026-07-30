using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Compile-time validation for [GenerateCommaSeparatedDisplayName] (SPIDERLY029).
    /// <para>
    /// Lives here rather than in <c>PaginatedResultGenerator</c>, which is where the shape actually breaks,
    /// for the reason <see cref="SpiderlyEntityValidator"/> spells out: a diagnostic hosted by an artifact
    /// generator is switchable by one line of <c>.spiderly/config.json</c>, is only reached if emission
    /// happens to walk that far, and — because the diagnostic is delivered by THROWING — aborts the
    /// generator's whole <c>Execute</c>. That last one is not theoretical: raised from the generator, one
    /// bad property on one entity stopped <c>PaginatedResultGenerator.generated.cs</c> being emitted at all,
    /// so every generated service lost <c>Build()</c> and the consumer got a wall of CS0103 instead of the
    /// located error. Here it is caught per entity and reported, and the rest of the file still generates.
    /// </para>
    /// </summary>
    public static class CommaSeparatedDisplayNameValidator
    {
        public static void ValidateEntity(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasGenerateCommaSeparatedDisplayNameAttribute()))
            {
                SpiderlyClass child = Helpers.GetEntityByPropertyType(property, allEntities);

                if (child == null)
                    continue; // Type from another project; nothing to resolve against here.

                // The generated table filter matches the collection by child Id
                // (PaginatedResultGenerator.GetCaseForEnumerable emits values.Contains(x.Id)), so a child
                // with no primary key cannot support the attribute at all.
                if (child.GetIdTypeOrNull(allEntities) == null)
                    throw SpiderlyDiagnostics.Create(
                        SpiderlyDiagnostics.CommaSeparatedDisplayNameOverKeylessJunction,
                        property.Location ?? entity.Location,
                        entity.Name, property.Name, child.Name);
            }
        }
    }
}
