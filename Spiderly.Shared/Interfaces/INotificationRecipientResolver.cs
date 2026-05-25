namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Reloads a recipient by its <see cref="INotificationRecipient.NotificationRecipientId"/> at delivery time.
    /// The framework ships no implementation — only the consuming app knows how to load its users/recipients — so
    /// register one in DI if you use <see cref="INotifier.Notify"/> (dynamic recipients). Apps that only use
    /// <see cref="INotifier.NotifyAdmins"/> need not register a resolver.
    /// </summary>
    public interface INotificationRecipientResolver
    {
        /// <summary>Loads the recipient with the given id, or returns <c>null</c> if it no longer exists.</summary>
        Task<INotificationRecipient> ResolveAsync(long recipientId);
    }
}
