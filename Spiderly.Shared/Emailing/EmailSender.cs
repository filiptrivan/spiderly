namespace Spiderly.Shared.Emailing
{
    /// <summary>
    /// Structured "From" address for transactional emails.
    /// </summary>
    /// <example>
    /// new EmailSender { Email = "noreply@example.com", Name = "Example Shop" };
    /// </example>
    public class EmailSender
    {
        public string Email { get; set; } = null!;
        public string? Name { get; set; }
    }
}
