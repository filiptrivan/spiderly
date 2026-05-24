namespace Spiderly.SourceGenerators.Models
{
    /// <summary>
    /// The kind of read-DTO column a single entity property expands into. Lets callers branch on
    /// the scalar/navigation/blob case without re-deriving it from the column name.
    /// </summary>
    public enum SpiderlyDTOColumnKind
    {
        Scalar,
        ManyToOneDisplayName,
        ManyToOneId,
        OneToManyCommaSeparated,
        OneToManyDTOList,
        BlobData,
        BlobValue,
    }

    /// <summary>
    /// One column a single entity property contributes to its generated read DTO. Produced by
    /// <see cref="Shared.SpiderlyClassFactory.GetDTOColumns"/> — the single source of truth for the
    /// entity-property → DTO-column mapping. The DTO generator turns these into DTO properties; the
    /// Excel-export exclusion generator matches them by <see cref="Name"/>, so the two can't drift.
    /// </summary>
    public class SpiderlyDTOColumn
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public SpiderlyDTOColumnKind Kind { get; set; }

        /// <summary>Set only for <see cref="SpiderlyDTOColumnKind.Scalar"/> columns; null otherwise.</summary>
        public string Description { get; set; }

        /// <summary>True only for <see cref="SpiderlyDTOColumnKind.Scalar"/> columns whose source is a <c>[SpiderlyEnum]</c>.</summary>
        public bool IsEnum { get; set; }
    }
}
