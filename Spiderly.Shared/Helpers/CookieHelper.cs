using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Helpers
{
    public static class CookieHelper
    {
        public static void ApplyCookieSettings(CookieOptions options, SameSiteMode? sameSiteOverride = null)
        {
            options.SameSite = sameSiteOverride ?? SettingsProvider.Current.CookieSameSite;

            if (!string.IsNullOrEmpty(SettingsProvider.Current.CookieDomain))
                options.Domain = SettingsProvider.Current.CookieDomain;
        }

        public static CookieOptions GetCookieOptions(int expirationMinutes, bool httpOnly)
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

        public static CookieOptions GetExpiredCookieOptions(bool httpOnly)
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

        public static void ClearCookie(IResponseCookies cookies, string key, bool httpOnly)
        {
            cookies.Append(key, string.Empty, GetExpiredCookieOptions(httpOnly));
        }
    }
}
