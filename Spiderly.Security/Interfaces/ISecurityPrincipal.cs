using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Interfaces
{
    /// <summary>
    /// The authorization root: anything that can hold <see cref="IRole"/>s and be permission-checked —
    /// a human <see cref="IUser"/>, a machine/service account, an AI agent, and so on. Authorization
    /// resolves permissions against this contract rather than a concrete user type, so an application
    /// may have a single principal kind (just <c>User</c>) or many, without changing the authorization core.
    /// </summary>
    public interface ISecurityPrincipal : IBusinessObject<long>
    {
        /// <summary>
        /// When <c>true</c>, the principal is blocked from authenticating and authorizing.
        /// Treat <c>null</c> as not disabled.
        /// </summary>
        bool? IsDisabled { get; set; }

        /// <summary>
        /// The roles granted to this principal. The union of their permissions forms the
        /// principal's effective rights.
        /// </summary>
        IReadOnlyCollection<IRole> Roles { get; }
    }
}
