using Spiderly.Security.Interfaces;

namespace Spiderly.Security.DTO
{
    /// <summary>
    /// The 2 main reasons why we use refresh token:
    /// 1. The sys admin can delete refresh token from the db/cache
    /// 2. We delete the old refresh token from the same browser, so the user can not use app from the multiple (defined) number of browsers
    /// https://stackoverflow.com/questions/38986005/what-is-the-purpose-of-a-refresh-token
    /// </summary>
    public class RefreshTokenDTO : IExpirableToken
    {
        public const string UserIdIndex = nameof(UserId);

        public long UserId { get; set; }
        public string IpAddress { get; set; }
        public string BrowserId { get; set; }
        public string TokenString { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}