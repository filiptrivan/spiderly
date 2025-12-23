namespace Spiderly.Security.Interfaces
{
    public interface IExpirableToken
    {
        DateTime ExpireAt { get; }
    }
}
