namespace Spiderly.Security.DTO
{
    public class JwtAuthResultDTO
    {
        public long UserId { get; set; }
        public string AccessToken { get; set; }
        public RefreshTokenDTO Token { get; set; }
    }
}
