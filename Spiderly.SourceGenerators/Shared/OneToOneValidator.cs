using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Compile-time validation for [WithOne] one-to-one declarations (SPIDERLY019-022).
    /// Throws <see cref="SpiderlyGenerationException"/> carrying a located SPIDERLY### diagnostic
    /// so build output surfaces the exact entity and property where the 1-1 contract is broken
    /// (instead of a CS8785 "generator failed" stack trace).
    /// </summary>
    public static class OneToOneValidator
    {
        public static void ValidateEntity(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            foreach (SpiderlyProperty nav in entity.Properties.Where(p => p.IsOneToOneType()))
            {
                string targetTypeName = nav.Type.Raw;

                // SPIDERLY022 — self-referential 1-1 unsupported
                if (targetTypeName == entity.Name)
                    throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneSelfReferential,
                        nav.Location ?? entity.Location, entity.Name, nav.Name);

                SpiderlyClass principal = allEntities.FirstOrDefault(c => c.Name == targetTypeName);
                if (principal == null)
                    continue; // type from another project; EF resolves at runtime

                string inverseName = nav.GetWithOneInverseName(); // null => unidirectional, nothing to check
                if (inverseName != null)
                {
                    SpiderlyProperty inverse = principal.Properties.FirstOrDefault(p => p.Name == inverseName);

                    // SPIDERLY020 — declared inverse nav must exist and be single-valued of this entity's type
                    if (inverse == null || inverse.Type.Raw != entity.Name)
                        throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneInverseNavNotFound,
                            nav.Location ?? entity.Location, inverseName, entity.Name, nav.Name, principal.Name);

                    // SPIDERLY019 — both sides carry [WithOne]
                    if (inverse.HasWithOneAttribute())
                        throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneOnBothSides,
                            nav.Location ?? entity.Location, entity.Name, nav.Name, principal.Name, inverse.Name);

                    // SPIDERLY021 — [Required] on the principal-side nav is unenforceable
                    if (inverse.IsEffectivelyRequired())
                        throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneRequiredOnPrincipal,
                            inverse.Location ?? principal.Location, principal.Name, inverse.Name);
                }
            }
        }
    }
}
