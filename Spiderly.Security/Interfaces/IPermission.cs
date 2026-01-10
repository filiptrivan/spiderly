using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Interfaces
{
    public interface IPermission : IReadonlyObject<int>
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Code { get; set; }

        IReadOnlyCollection<IRole> Roles { get; }
    }
}
