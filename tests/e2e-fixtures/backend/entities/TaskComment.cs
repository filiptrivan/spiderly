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
        [DisplayName]
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; }

        [CascadeDelete]
        [WithMany(nameof(ProjectTask.TaskComments))]
        public virtual ProjectTask ProjectTask { get; set; }

        [SetNull]
        [UIControlType(nameof(UIControlTypeCodes.Autocomplete))]
        [WithMany(nameof(User.TaskComments))]
        public virtual User Author { get; set; }
    }
}
