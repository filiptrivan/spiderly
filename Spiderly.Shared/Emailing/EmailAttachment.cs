namespace Spiderly.Shared.Emailing
{
    /// <summary>
    /// Binary attachment for transactional emails. Content must be base64-encoded.
    /// </summary>
    public class EmailAttachment
    {
        public string Name { get; set; }
        public string ContentBase64 { get; set; }
        public string ContentType { get; set; }

        public static EmailAttachment FromBytes(string name, byte[] bytes, string contentType = "application/octet-stream")
        {
            return new EmailAttachment
            {
                Name = name,
                ContentBase64 = Convert.ToBase64String(bytes),
                ContentType = contentType,
            };
        }
    }
}
