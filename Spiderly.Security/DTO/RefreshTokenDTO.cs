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
        /// Marks a token that has been rotated away, with <see cref="ExpiresAt"/> shortened to the end of its
        /// grace window. Exactly one token per (user, browser) is not superseded, so the token that replaced
        /// this one is found by looking that live one up rather than by storing a pointer to it here.
        /// See <see cref="AuthPolicyOptions.RefreshTokenGraceSeconds"/> for why the record is kept at all.
        /// </summary>
        public bool IsSuperseded { get; set; }
    }
}
