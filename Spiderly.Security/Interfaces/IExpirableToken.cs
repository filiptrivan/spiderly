namespace Spiderly.Security.Interfaces
{
    public interface IExpirableToken
    {
        DateTime ExpiresAt { get; }
    }
}
