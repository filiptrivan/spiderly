namespace Spiderly.Security.DTO
{
    /// <summary>
    /// Intentionally lowercase property names because of cookie JSON parsing on the frontend
    /// </summary>
    public class AuthResultWithCookiesDTO
    {
        public long userId { get; set; }
        public string email { get; set; }
        public DateTime accessTokenExpiresAt { get; set; }
    }
}
