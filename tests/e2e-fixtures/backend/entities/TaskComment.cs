using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace __APP_NAME__.Business.Entities
{
    [SpiderlyEntity]
    public class TaskComment : BusinessObject<long>
    {
        [UIDoNotGenerate]
        [Required]
        public int OrderNumber { get; set; }

        [DisplayName]
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; } = null!;

        [CascadeDelete]
        [WithMany(nameof(ProjectTask.TaskComments))]
        public virtual ProjectTask ProjectTask { get; set; } = null!;

        [SetNull]
        [UIControlType(nameof(UIControlTypeCodes.Autocomplete))]
        [WithMany(nameof(User.TaskComments))]
        public virtual User? Author { get; set; }

        // Nullable, not '= null!': no [Required], so this is an OPTIONAL relationship and its FK column
        // is nullable. Annotating the navigation non-nullable makes EF infer a required relationship
        // instead, and a comment saved without a category then writes 0 rather than NULL — a foreign-key
        // violation. The annotation is not cosmetic here; it is schema.
        [UIControlType(nameof(UIControlTypeCodes.Dropdown))]
        [WithMany(nameof(TaskCategory.TaskComments))]
        public virtual TaskCategory? Category { get; set; }
    }
}
