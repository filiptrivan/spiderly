namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Implemented by a recipient that has an email address. This is the Email channel's <i>recipient</i>
    /// capability interface — for a <c>Notify(recipient, …)</c> send, <see cref="EmailChannel"/> reads the address
    /// from here; a recipient that does not implement it is skipped by the Email channel.
    /// </summary>
    public interface IEmailRecipient
    {
        /// <summary>The recipient's email address.</summary>
        string EmailAddress { get; }
    }
}
