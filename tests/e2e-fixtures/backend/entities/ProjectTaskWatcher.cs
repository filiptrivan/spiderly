using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace __APP_NAME__.Business.Entities
{
    [M2M]
    [SpiderlyEntity]
    public class ProjectTaskWatcher : BusinessObject<long>
    {
        [M2MWithMany(nameof(ProjectTask.Watchers))]
        public virtual ProjectTask ProjectTask { get; set; } = null!;

        [M2MWithMany(nameof(User.WatchedTasks))]
        public virtual User User { get; set; } = null!;
    }
}
