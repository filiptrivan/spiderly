using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace __APP_NAME__.Business.Entities
{
    [Index(nameof(Email), IsUnique = true)]
    [SpiderlyEntity]
    public class User : BusinessObject<long>, IUser
    {
        [UIDoNotGenerate]
        [DisplayName]
        [Email]
        [StringLength(70, MinimumLength = 5)]
        [Required]
        public string Email { get; set; }

        public bool? HasLoggedInWithGoogleAsExternalProvider { get; set; }

        public bool? IsDisabled { get; set; }

        public virtual List<Role> Roles { get; } = new(); // M2M
        IReadOnlyCollection<IRole> IUser.Roles => Roles;

        [UIDoNotGenerate]
        public virtual List<Project> Projects { get; } = new(); // M2M

        [UIDoNotGenerate]
        public virtual List<ProjectTask> AssignedTasks { get; } = new();

        [UIDoNotGenerate]
        public virtual List<TaskComment> TaskComments { get; } = new();
    }
}
