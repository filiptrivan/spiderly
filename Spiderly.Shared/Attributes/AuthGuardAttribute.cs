using Microsoft.AspNetCore.Authorization;
using Spiderly.Shared.Authorization;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// The single declaration that protects an endpoint. Bare, it requires an authenticated principal; with a
    /// permission code, it additionally requires the principal to hold that permission:
    /// <code>
    /// [AuthGuard]                     // any authenticated principal — identity-scoped endpoints ("my orders")
    /// [AuthGuard("UpdateProduct")]    // authenticated AND holds UpdateProduct
    /// </code>
    /// Applies to a controller class as well as an action; ASP.NET Core combines class- and action-level
    /// requirements (AND), and <c>[AllowAnonymous]</c> opts an action out.
    /// </summary>
    /// <remarks>
    /// <para><b>Why one attribute.</b> This used to be two: <c>[AuthGuard]</c> (an action filter doing
    /// authentication plus the CSRF header check) and the now-deleted <c>HasPermissionAttribute</c>
    /// (authorization). They were <b>co-required</b> on every protected endpoint but independently forgettable,
    /// and the failure was one-directional: <c>[AuthGuard]</c> reads as "protected", so it was always written,
    /// while a missing permission attribute compiled, passed review, and looked deliberate. A consumer audit found
    /// ~54 admin endpoints authenticated but unauthorized, and <b>zero</b> with the opposite mistake. This is the
    /// same class of drift the "bundle co-required registrations behind one call" rule already covers for DI
    /// (see <see cref="PermissionHandlerRegistrationGuard"/>), applied one level up, to the attribute pair.</para>
    /// <para><b>Why it can be one attribute now.</b> The two differed in kind only because this one carried CSRF:
    /// authorization is a policy marker, CSRF is per-request work, so merging them would have meant one class
    /// being both a marker and a filter. CSRF moved to <c>SpiderlyCsrfMiddleware</c> (where every major framework
    /// puts it — Django, Rails, Laravel, Spring, and ASP.NET Core's own <c>AutoValidateAntiforgeryToken</c>
    /// guidance: apply broadly, because per-action opt-in leaves endpoints unprotected by mistake). With CSRF
    /// gone, authentication was already the platform's job, so this is now plain
    /// <c>[Authorize]</c>/<c>[Authorize(Policy = "perm:&lt;code&gt;")]</c> sugar — one attribute, permission in
    /// parens, exactly the shape ASP.NET Core itself uses.</para>
    /// <para>Deriving from <see cref="AuthorizeAttribute"/> (rather than hand-rolling a filter) is what keeps
    /// class+action composition, <c>[AllowAnonymous]</c>, <c>RequireAuthorization()</c> on minimal APIs, and
    /// policy visibility in OpenAPI working for free.</para>
    /// <para>One permission per attribute. Stack two to require both (ASP.NET ANDs them). "Either A or B" needs a
    /// real composite policy and is deliberately not modelled here.</para>
    /// </remarks>
    public sealed class AuthGuardAttribute : AuthorizeAttribute
    {
        /// <summary>Requires an authenticated principal, with no specific permission.</summary>
        public AuthGuardAttribute()
        {
        }

        /// <summary>Requires an authenticated principal holding <paramref name="permissionCode"/>.</summary>
        /// <param name="permissionCode">
        /// The permission code the caller must hold (e.g. <c>UpdateProduct</c>, or a generated CRUD code). Passed
        /// as a string because it is an attribute argument: permission codes are exposed as static properties, not
        /// constants, so <c>nameof</c>/constant folding is unavailable. The <c>perm:</c> policy name is built via
        /// <see cref="SpiderlyAuthorizationPolicies.ForPermission"/>, keeping the convention in one place.
        /// </param>
        public AuthGuardAttribute(string permissionCode)
            : base(SpiderlyAuthorizationPolicies.ForPermission(permissionCode))
        {
            PermissionCode = permissionCode;
        }

        /// <summary>The required permission code, or <c>null</c> when this guard only requires authentication.</summary>
        public string PermissionCode { get; }
    }
}
