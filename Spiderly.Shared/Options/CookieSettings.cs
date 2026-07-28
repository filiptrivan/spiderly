using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared
{
    /// <summary>
    /// Cookie attributes applied to auth cookies. Bound from the <c>AppSettings:Spiderly.Shared</c>
    /// configuration section and injected into the cookie service as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    /// <remarks>
    /// Named <c>CookieSettings</c> rather than <c>CookieOptions</c> to avoid colliding with
    /// <see cref="Microsoft.AspNetCore.Http.CookieOptions"/>, which the cookie service constructs.
    /// </remarks>
    public class CookieSettings
    {
        /// <summary>Domain attribute for auth cookies; when unset or empty the cookie is host-only.</summary>
        public string? CookieDomain { get; set; }

        /// <summary>SameSite mode applied to auth cookies.</summary>
        public SameSiteMode CookieSameSite { get; set; } = SameSiteMode.None;
    }
}
