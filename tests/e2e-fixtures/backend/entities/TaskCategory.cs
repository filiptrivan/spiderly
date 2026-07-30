using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace __APP_NAME__.Business.Entities
{
    [SpiderlyEntity]
    [DoNotAuthorize]
    public class TaskCategory : ReadonlyObject<byte>
    {
        [DisplayName]
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [UIControlType(nameof(UIControlTypeCodes.ColorPicker))]
        [Required]
        [StringLength(10, MinimumLength = 4)]
        public string Color { get; set; } = null!;

        [UIDoNotGenerate]
        public virtual List<ProjectTask> ProjectTasks { get; } = new();

        [UIDoNotGenerate]
        public virtual List<TaskComment> TaskComments { get; } = new();

        [UIDoNotGenerate]
        public virtual List<TaskAttachment> TaskAttachments { get; } = new();
    }
}
