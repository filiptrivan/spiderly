using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Exceptions
{
    public class BusinessExceptionWithoutLog : ExceptionWithoutLog
    {
        public int StatusCode { get; set; } = StatusCodes.Status400BadRequest;

        public BusinessExceptionWithoutLog(string message) : base(message) { }
    }
}
