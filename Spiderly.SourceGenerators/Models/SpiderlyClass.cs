using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyClass
    {
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;

        /// <summary>
        /// Location of the class identifier in source, used to anchor Roslyn diagnostics.
        /// Null for classes reconstructed from referenced assemblies — callers must fall back to <see cref="Location.None"/>.
        /// </summary>
        public Location? Location { get; set; }

        /// <summary>
        /// Here is only one base type, no interfaces. Null when the class declares no base type.
        /// </summary>
        public string? BaseType { get; set; }

        /// <summary>
        /// The entity's primary-key type, resolved by <see cref="Shared.SpiderlyClassFactory"/> for every
        /// <c>[SpiderlyEntity]</c> class. Left null on DTOs, controllers, services and mappers, which have no
        /// key — so read it only on an entity.
        /// <para>
        /// The point is MEANING, not caching. On an entity, <c>null</c> means exactly one thing: a KEYLESS
        /// many-to-many junction. A class merely missing its <c>BusinessObject&lt;T&gt;</c> base never lands
        /// here as null — it fails resolution with SPIDERLY010 during construction. That is what lets a
        /// caller branch on the null instead of choosing between two similarly-named accessors, one of which
        /// throws on a shape that legitimately reaches it.
        /// </para>
        /// <para>
        /// Do NOT read this as a performance cache. Resolution was measured at roughly 6 allocations and no
        /// list scan (entities derive from <c>BusinessObject&lt;T&gt;</c> directly, so the base-chain walk
        /// never iterates), i.e. tens of microseconds per compilation across every generator. Eager
        /// resolution is deliberate anyway: it is what makes the null unambiguous above, and it costs the
        /// generators that never ask one resolution per entity.
        /// </para>
        /// </summary>
        public string? IdType { get; set; }

        public bool IsAbstract { get; set; }

        /// <summary>Set only for controller classes; null otherwise.</summary>
        public string? ControllerName { get; set; }

        /// <summary>
        /// For the DTO classes
        /// </summary>
        public bool IsGenerated { get; set; }

        /// <summary>The class's XML doc summary; null when it carries none.</summary>
        public string? Description { get; set; }

        public List<SpiderlyProperty> Properties { get; set; } = new();

        public List<SpiderlyAttribute> Attributes { get; set; } = new();

        public List<SpiderlyMethod> Methods { get; set; } = new();
    }
}
