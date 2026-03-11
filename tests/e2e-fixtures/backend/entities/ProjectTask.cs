using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace __APP_NAME__.Business.Entities
{
    [DisplayName("Project.Name")]
    public class ProjectTask : BusinessObject<long>
    {
        [UIDoNotGenerate]
        [Required]
        public int OrderNumber { get; set; }

        [DisplayName]
        [Required]
        [StringLength(300, MinimumLength = 1)]
        public string Title { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.TextArea))]
        [StringLength(2000, MinimumLength = 1)]
        public string Description { get; set; }

        [Required]
        [Precision(18, 2)]
        [GreaterThanOrEqualTo(0)]
        public decimal EstimatedHours { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.Calendar))]
        public DateTime? DueDate { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.CheckBox))]
        public bool? IsCompleted { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.TextBlock))]
        [StringLength(70, MinimumLength = 1)]
        public string CreatedByUserEmail { get; set; }

        [ExcludeFromDTO]
        [StringLength(5000, MinimumLength = 1)]
        public string InternalNotes { get; set; }

        [CascadeDelete]
        [WithMany(nameof(Project.ProjectTasks))]
        public virtual Project Project { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.Dropdown))]
        [WithMany(nameof(TaskCategory.ProjectTasks))]
        public virtual TaskCategory TaskCategory { get; set; }

        [SetNull]
        [UIControlType(nameof(UIControlTypeCodes.Autocomplete))]
        [WithMany(nameof(User.AssignedTasks))]
        public virtual User AssignedTo { get; set; }

        [UIDoNotGenerate]
        public virtual List<TaskComment> TaskComments { get; } = new();
    }
}
