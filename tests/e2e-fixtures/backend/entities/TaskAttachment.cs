using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace __APP_NAME__.Business.Entities
{
    /// <summary>
    /// Exists for the two <c>[ForeignKey]</c> resolution branches, which had no fixture coverage at all:
    /// <c>Extensions.ResolveForeignKeyName</c> has three paths and only the conventional <c>{Nav}Id</c> one was
    /// exercised. The renamed-scalar branch matters most — a <c>[ForeignKey]</c>-renamed scalar used to be
    /// addressed as <c>{Nav}Id</c> in generated DTO access and was therefore unreachable, a real bug fixed
    /// blind because nothing compiled this shape.
    /// </summary>
    [SpiderlyEntity]
    public class TaskAttachment : BusinessObject<long>
    {
        [DisplayName]
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string FileName { get; set; } = null!;

        // Branch 1: [ForeignKey] on the NAVIGATION names the foreign-key property. Also the only fixture
        // foreign key onto a ReadonlyObject with a byte id.
        public byte? CategoryKey { get; set; }

        [UIControlType(nameof(Spiderly.Shared.Enums.UIControlTypeCodes.Dropdown))]
        [ForeignKey(nameof(CategoryKey))]
        [WithMany(nameof(TaskCategory.TaskAttachments))]
        public virtual TaskCategory? Category { get; set; }

        // Branch 2: [ForeignKey(nameof(Nav))] on the SCALAR, whose name deliberately does NOT follow the
        // {Nav}Id convention — that mismatch is what made the bug above invisible.
        [ForeignKey(nameof(UploadedBy))]
        public long? UploaderKey { get; set; }

        [SetNull]
        [UIControlType(nameof(Spiderly.Shared.Enums.UIControlTypeCodes.Autocomplete))]
        [WithMany(nameof(User.TaskAttachments))]
        public virtual User? UploadedBy { get; set; }
    }
}
