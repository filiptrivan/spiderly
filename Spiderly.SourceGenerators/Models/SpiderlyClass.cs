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
        /// The entity's primary-key type, resolved ONCE by <see cref="Shared.SpiderlyClassFactory"/> instead
        /// of re-walked at each of the ~39 sites that used to ask. Set only for <c>[SpiderlyEntity]</c>
        /// classes; left null on DTOs, controllers, services and mappers, which have no key.
        /// <para>
        /// On an entity, <c>null</c> means exactly one thing: a KEYLESS many-to-many junction. A class that
        /// is merely missing its <c>BusinessObject&lt;T&gt;</c> base does not land here as null — it fails
        /// resolution with SPIDERLY010 at construction. Keeping "legitimately keyless" and "malformed"
        /// distinguishable is the whole point: a null that meant either would leave every consumer guessing,
        /// which is the bug this property exists to retire.
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
