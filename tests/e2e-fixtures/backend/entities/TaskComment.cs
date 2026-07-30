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

        // Nullable for the same reason as Category below: no [Required], so the FK column is nullable.
        [CascadeDelete]
        [WithMany(nameof(ProjectTask.TaskComments))]
        public virtual ProjectTask? ProjectTask { get; set; }

        [SetNull]
        [UIControlType(nameof(UIControlTypeCodes.Autocomplete))]
        [WithMany(nameof(User.TaskComments))]
        public virtual User? Author { get; set; }

        // Nullable because it carries no [Required]. Annotating it non-nullable once broke every comment
        // insert with a foreign-key violation; that cause was an ordering bug in
        // ConfigureManyToOneRelationships (.IsRequired before .HasForeignKey), since fixed. SPIDERLY028
        // still rejects the annotation, for the remaining reason: over a nullable FK it would have EF
        // materialize null into a non-nullable property.
        [UIControlType(nameof(UIControlTypeCodes.Dropdown))]
        [WithMany(nameof(TaskCategory.TaskComments))]
        public virtual TaskCategory? Category { get; set; }
    }
}
