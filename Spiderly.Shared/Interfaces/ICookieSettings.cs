using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the cookie attributes applied to auth cookies. Implemented by
    /// <see cref="Settings"/> and injected into the cookie service, so cookie handling depends on
    /// configuration passed in rather than the global mutable <c>SettingsProvider</c> static.
    /// </summary>
    public interface ICookieSettings
    {
        /// <summary>Domain attribute for auth cookies; when empty the cookie is host-only.</summary>
        string CookieDomain { get; }

        /// <summary>SameSite mode applied to auth cookies.</summary>
        SameSiteMode CookieSameSite { get; }
    }
}
