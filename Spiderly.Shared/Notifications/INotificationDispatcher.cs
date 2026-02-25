namespace Spiderly.Shared.Notifications
{
    public interface INotificationDispatcher
    {
        void DispatchUnhandledException(long? userId, bool isProduction, Exception ex);
    }
}
