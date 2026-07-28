using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Models
{
    /// <summary>
    /// A group of details-page field blocks produced from the <c>[UIDetailsGroup]</c> attribute.
    /// </summary>
    public class DetailsFieldGroup
    {
        /// <summary>
        /// Transloco translation key used as the group's panel header, or <c>null</c> for the
        /// implicit (headerless) group that collects ungrouped properties.
        /// </summary>
        public string? TranslationKey { get; set; }

        /// <summary>
        /// Pre-rendered HTML blocks for the properties belonging to this group, in declaration order.
        /// </summary>
        public List<string> Blocks { get; set; } = new();
    }
}
