namespace Spiderly.Shared.Emailing
{
    /// <summary>
    /// Structured "From" address for transactional emails.
    /// </summary>
    /// <example>
    /// new EmailSender { Email = "noreply@dcksrbija.rs", Name = "DCK Srbija" };
    /// </example>
    public class EmailSender
    {
        public string Email { get; set; } = null!;
        public string? Name { get; set; }
    }
}
