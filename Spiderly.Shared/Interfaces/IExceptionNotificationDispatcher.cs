namespace Spiderly.Shared.Interfaces
{
    public interface IExceptionNotificationDispatcher
    {
        void DispatchUnhandledException(long? userId, bool isProduction, Exception ex);
    }
}
