using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace __APP_NAME__.Business.Entities
{
    [M2M]
    public class ProjectMember : BusinessObject<long>
    {
        [M2MWithMany(nameof(Project.Members))]
        public virtual Project Project { get; set; }

        [M2MWithMany(nameof(User.Projects))]
        public virtual User User { get; set; }
    }
}
