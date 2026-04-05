using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public int StatusCode { get; set; } = StatusCodes.Status401Unauthorized;

        public UnauthorizedException() : base("You don't have the necessary rights to perform the operation.") { }

        public UnauthorizedException(string message) : base(message) { }
    }
}