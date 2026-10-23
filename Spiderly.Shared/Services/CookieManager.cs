using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Spiderly.Shared.Services
{
    /// <summary>
    /// Builds <see cref="CookieOptions"/> with the configured SameSite/Domain attributes and clears
    /// cookies. Injectable replacement for the former static <c>CookieHelper</c> — depends on
    /// <see cref="CookieSettings"/> rather than the global mutable <c>SettingsProvider</c> static.
    /// Registered as a singleton (stateless aside from the injected settings).
    /// </summary>
    /// <remarks>
    /// Cookie expiry is computed from the machine's clock, not the app's <see cref="TimeProvider"/> that Spiderly's
    /// stamps and token checks read: the client judges a cookie's <c>Expires</c> by its own clock, so under a test's
    /// fake clock a real client (a browser, an <c>HttpClient</c>'s cookie container) would drop or keep the cookie at
    /// the wrong time. The token inside the cookie is still judged by the app's clock, on the server.
    /// </remarks>
    public class CookieManager
    {
        private readonly CookieSettings _cookieSettings;

        public CookieManager(IOptions<CookieSettings> options)
        {
            _cookieSettings = options.Value;
        }

        /// <summary>Applies the configured SameSite (or an override) and Domain to <paramref name="options"/>.</summary>
        public void ApplyCookieSettings(CookieOptions options, SameSiteMode? sameSiteOverride = null)
        {
            options.SameSite = sameSiteOverride ?? _cookieSettings.CookieSameSite;

            if (!string.IsNullOrEmpty(_cookieSettings.CookieDomain))
                options.Domain = _cookieSettings.CookieDomain;
        }

        /// <summary>Builds secure cookie options expiring <paramref name="expirationMinutes"/> from now.</summary>
        public CookieOptions GetCookieOptions(int expirationMinutes, bool httpOnly)
        {
            CookieOptions options = new()
            {
                HttpOnly = httpOnly,
                Secure = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
            };

            ApplyCookieSettings(options);
            return options;
        }

        /// <summary>Builds secure cookie options already expired (used to delete a cookie).</summary>
        public CookieOptions GetExpiredCookieOptions(bool httpOnly)
        {
            CookieOptions options = new()
            {
                HttpOnly = httpOnly,
                Secure = true,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            };

            ApplyCookieSettings(options);
            return options;
        }

        /// <summary>Deletes a cookie by appending an already-expired one with matching attributes.</summary>
        public void ClearCookie(IResponseCookies cookies, string key, bool httpOnly)
        {
            cookies.Append(key, string.Empty, GetExpiredCookieOptions(httpOnly));
        }
    }
}
