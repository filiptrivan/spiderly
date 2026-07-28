using System.Diagnostics.CodeAnalysis;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Resolves the current request's principal kind to its <see cref="IPrincipalPermissionResolver"/>.
    /// An application may register a single principal kind (the common case — just <c>User</c>) or many
    /// (e.g. <c>User</c> + <c>ServiceAccount</c>); the authorization core dispatches through this registry
    /// rather than hard-coding a single principal type.
    /// </summary>
    public interface IPrincipalRegistry
    {
        /// <summary>True when no principal kind has been registered.</summary>
        bool IsEmpty { get; }

        /// <summary>
        /// The kind assumed when a request carries no <c>principal_kind</c> claim. This is the single
        /// registered kind when exactly one is registered; <c>null</c> when zero or more than one are
        /// registered (the claim is then required to disambiguate).
        /// </summary>
        string? DefaultKind { get; }

        /// <summary>
        /// Resolves a kind to its permission resolver. A <c>null</c>/empty <paramref name="kind"/> falls back
        /// to <see cref="DefaultKind"/>. Returns <c>false</c> (rather than throwing) when the kind cannot be
        /// resolved — unknown kind, or no claim while multiple kinds are registered — so the authorization
        /// layer can fail closed (deny) instead of surfacing a server error for an unrecognized caller.
        /// </summary>
        bool TryResolve(string? kind, [NotNullWhen(true)] out IPrincipalPermissionResolver? resolver);

        /// <summary>
        /// Whether <paramref name="kind"/> is a human principal kind. A <c>null</c>/empty kind falls back to
        /// <see cref="DefaultKind"/>. Fails <b>closed</b>: an unresolvable kind is not human, so an identity-scoped
        /// operation refuses rather than guessing.
        /// </summary>
        bool IsHuman(string? kind);
    }
}
