namespace Spiderly.Shared.Interfaces
{
    public interface INotificationDispatcher
    {
        void DispatchUnhandledException(long? userId, Exception ex);
        void DispatchSecurityEvent(string eventType, string debounceKey, string message);
    }
}
