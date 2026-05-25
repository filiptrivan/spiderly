namespace Spiderly.Security.Interfaces
{
    /// <summary>
    /// Contract for the entity that links a user to an external identity provider login.
    /// One row per <c>(Provider, ProviderKey)</c> — a user may have several (Google, Microsoft, …).
    /// The concrete entity is scaffolded into the consumer app (like <see cref="IUser"/>); the security
    /// layer operates on it generically via the <c>TUserExternalLogin</c> type parameter.
    /// </summary>
    public interface IUserExternalLogin
    {
        /// <summary>The id of the user this external login belongs to.</summary>
        long UserId { get; set; }

        /// <summary>The external provider code (e.g. <c>"google"</c>).</summary>
        string Provider { get; set; }

        /// <summary>The provider's stable, immutable user identifier (the OIDC <c>sub</c> claim).</summary>
        string ProviderKey { get; set; }
    }
}
