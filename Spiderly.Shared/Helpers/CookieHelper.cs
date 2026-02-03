using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Helpers
{
    public static class CookieHelper
    {
        public static CookieOptions GetCookieOptions(int expirationMinutes, bool httpOnly)
        {
            return new CookieOptions
            {
                HttpOnly = httpOnly,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
            };
        }

        public static CookieOptions GetExpiredCookieOptions(bool httpOnly)
        {
            return new CookieOptions
            {
                HttpOnly = httpOnly,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            };
        }

        public static void ClearCookie(IResponseCookies cookies, string key, bool httpOnly)
        {
            cookies.Append(key, string.Empty, GetExpiredCookieOptions(httpOnly));
        }
    }
}
