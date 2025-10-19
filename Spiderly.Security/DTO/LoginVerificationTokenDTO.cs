namespace Spiderly.Security.DTO
{
    public class LoginVerificationTokenDTO
    {
        public string Email { get; set; }
        public string BrowserId { get; set; }
        public DateTime ExpireAt { get; set; }
    }
}
