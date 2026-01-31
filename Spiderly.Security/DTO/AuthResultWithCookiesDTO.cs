namespace Spiderly.Security.DTO
{
    public class AuthResultWithCookiesDTO
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        public string AccessToken { get; set; }
    }
}
