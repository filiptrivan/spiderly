using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyProperty
    {
        /// <summary>
        /// The property's C# type, parsed once into <see cref="SpiderlyTypeRef"/>. Assign a raw type string
        /// (<c>Type = "List&lt;Foo&gt;"</c>) — it converts implicitly. Read structured parts
        /// (<c>Type.CoreName</c>, <c>Type.Name</c>, <c>Type.IsNullable</c>, <c>Type.IsCollection</c>) instead of
        /// re-parsing the string by hand; use <c>Type.Raw</c> when the verbatim type string is needed (emission,
        /// literal type-name dispatch). One parser is the single source of truth, so call sites can't drift.
        /// </summary>
        public SpiderlyTypeRef Type { get; set; } = null!;
        public string Name { get; set; } = null!;

        /// <summary>
        /// Location of the property identifier in source, used to anchor Roslyn diagnostics.
        /// Null for synthesized properties (DTO-generated, base-class stubs).
        /// </summary>
        public Location? Location { get; set; }

        /// <summary>
        /// input: public string Name { get; set; } = "Filip"
        /// output: "Filip"
        /// <para>Null when the property declares no initializer.</para>
        /// </summary>
        public string? StringValue { get; set; }

        public string? EntityName { get; set; } // TODO FT: Add to every case, you didn't finished this, but it works for now.
        public bool IsSaveBodyMainDTO { get; set; }

        /// <summary>The property's XML doc summary; null when it carries none.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// True when this property's type is a C# enum decorated with <c>[SpiderlyEnum]</c>.
        /// Set during class analysis when an enum-name set is supplied; defaults to false otherwise.
        /// Generators consult this to short-circuit M2O classification (an enum is a scalar value, not a navigation property).
        /// </summary>
        public bool IsEnum { get; set; }

        /// <summary>
        /// True when this property is the principal-side inverse navigation of a one-to-one — a bare reference
        /// nav whose target entity declares a <c>[WithOne]</c> pointing back at it. Computed once (cross-entity)
        /// when the class graph is built; defaults to false. Generators consult it via
        /// <c>IsManyToOneType()</c>, which returns false for it: the principal inverse is M2O-shaped by accident
        /// of classification but owns no FK / DTO column (the FK lives on the dependent side), so it must produce
        /// no M2O save / mapper / autocomplete / FK-validation code.
        /// </summary>
        public bool IsOneToOnePrincipalInverseNav { get; set; }

        /// <summary>
        /// Set on generated DTO properties only, from <see cref="SpiderlyDTOColumn.IsRequired"/> — the
        /// entity-property → DTO-column mapping is where requiredness is decided, and it does not survive
        /// as an attribute (a DTO column can be synthesized from a navigation, so it has no declaring
        /// property of its own to carry <c>[Required]</c>). Drives the DTO's emitted nullability.
        /// Always false for entity properties; ask <c>IsEffectivelyRequired()</c> for those.
        /// </summary>
        public bool IsRequired { get; set; }

        public List<SpiderlyAttribute> Attributes { get; set; } = new();
    }
}
