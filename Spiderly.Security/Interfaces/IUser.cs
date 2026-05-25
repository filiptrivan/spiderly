using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Interfaces
{
    public interface IUser : IBusinessObject<long>
    {
        public string Email { get; set; }

        public bool? IsDisabled { get; set; }

        IReadOnlyCollection<IRole> Roles { get; }
    }
}
