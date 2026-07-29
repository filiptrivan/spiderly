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
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public SpiderlyDTOColumnKind Kind { get; set; }

        /// <summary>Set only for <see cref="SpiderlyDTOColumnKind.Scalar"/> columns; null otherwise.</summary>
        public string? Description { get; set; }

        /// <summary>True only for <see cref="SpiderlyDTOColumnKind.Scalar"/> columns whose source is a <c>[SpiderlyEnum]</c>.</summary>
        public bool IsEnum { get; set; }

        /// <summary>
        /// The column is guaranteed to carry a value — the DTO emits it non-nullable (a reference type
        /// additionally gets <c>= null!</c>). Derived from <c>[Required]</c>, which is already the signal
        /// EF turns into NOT NULL, Swashbuckle turns into a required schema member, and FluentValidation
        /// turns into <c>.NotEmpty()</c>.
        /// <para>
        /// Deliberately false for <see cref="SpiderlyDTOColumnKind.ManyToOneDisplayName"/> and
        /// <see cref="SpiderlyDTOColumnKind.OneToManyCommaSeparated"/> even when the source navigation is
        /// required: those project a value out of a DIFFERENT entity, so deriving requiredness would make
        /// adding <c>[Required]</c> over there a silent wire-contract change here, with nothing to see at
        /// the edit site. <see cref="SpiderlyDTOColumnKind.ManyToOneId"/> has no such problem — the FK is
        /// this entity's own column, and <c>ForeignKeyValidator</c> already fails the build when its
        /// nullability disagrees with the navigation's requiredness.
        /// </para>
        /// </summary>
        public bool IsRequired { get; set; }
    }
}
