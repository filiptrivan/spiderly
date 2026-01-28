namespace Spiderly.Security.DTO
{
    public class GoogleOAuthStateDTO
    {
        public string ReturnUrl { get; set; }
        public string BrowserId { get; set; }
        public string Nonce { get; set; }
    }

    public class GoogleTokenResponseDTO
    {
        public string access_token { get; set; }
        public string id_token { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
        public string scope { get; set; }
    }
}
