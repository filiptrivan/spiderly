using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public int StatusCode { get; set; } = StatusCodes.Status401Unauthorized;

        // Constructor
        public UnauthorizedException() : base(SharedTerms.UnauthorizedAccessExceptionMessage) { }

        // Constructor with message
        public UnauthorizedException(string message) : base(message) { }
    }
}