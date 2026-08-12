using Spiderly.Security.Interfaces;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    /// <summary>
    /// The 2 main reasons why we use refresh token:
    /// 1. The sys admin can delete refresh token from the db/cache
    /// 2. We delete the old refresh token from the same browser, so the user can not use app from the multiple (defined) number of browsers
    /// https://stackoverflow.com/questions/38986005/what-is-the-purpose-of-a-refresh-token
    /// </summary>
    [SpiderlyDTO]
    public class RefreshTokenDTO : IExpirableToken
    {
        public const string UserIdIndex = nameof(UserId);

        public long UserId { get; set; }
        public string? IpAddress { get; set; }
        public string? BrowserId { get; set; }
        public string TokenString { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Set when this token has been rotated away: it holds the token string that replaced it, and
        /// <see cref="ExpiresAt"/> has been shortened to the end of the grace window. A request still
        /// carrying it (one the browser composed before the rotation's cookie arrived) is answered with the
        /// successor instead of being rejected. <c>null</c> on a live token.
        /// See <see cref="AuthPolicyOptions.RefreshTokenGraceSeconds"/>.
        /// </summary>
        public string? SupersededByTokenString { get; set; }
    }
}
