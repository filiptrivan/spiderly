namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Optional app-supplied decoration for outbound email headers. Every
    /// <see cref="IEmailingService"/> implementation consults it once per message, so a consumer can
    /// stamp a header on <b>every</b> send — including the ones it never calls itself, such as the
    /// framework's own verification email — without threading a parameter through each send site.
    ///
    /// <para>Register one implementation to enable it; with none registered no headers are sent and
    /// the payload is unchanged. Returning <c>null</c> (or an empty map) for a recipient declines
    /// decoration for that message.</para>
    ///
    /// <para>Typical use is a <c>List-Unsubscribe</c> / <c>List-Unsubscribe-Post</c> pair pointing at
    /// the app's own opt-out endpoint, so the mailbox provider's unsubscribe button reaches the app
    /// rather than the email provider's suppression list.</para>
    /// <example>
    /// <code>
    /// services.AddScoped&lt;IOutboundEmailHeaderProvider, UnsubscribeHeaderProvider&gt;();
    /// </code>
    /// </example>
    /// </summary>
    public interface IOutboundEmailHeaderProvider
    {
        /// <summary>
        /// Headers to add to the message addressed to <paramref name="recipient"/>, or <c>null</c> to
        /// add none. Implementations must not throw — a decoration failure must not lose the email.
        /// </summary>
        IDictionary<string, string>? HeadersFor(string recipient);
    }
}
