using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Exceptions
{
    public class ExpiredVerificationException : Exception
    {
        public int StatusCode { get; set; } = StatusCodes.Status400BadRequest;

        public ExpiredVerificationException() : base("Your verification code has expired. Please request a new code to continue.") { }

        public ExpiredVerificationException(string message) : base(message) { }
    }
}
