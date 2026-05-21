using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Interfaces
{
    public interface IRole : IBusinessObject<int>
    {
        public string Name { get; set; }

        IReadOnlyCollection<IUser> Users { get; }

        IReadOnlyCollection<IPermission> Permissions { get; }
    }
}