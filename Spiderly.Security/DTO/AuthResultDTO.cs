namespace Spiderly.Security.DTO
{
    public class AuthResultDTO
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
