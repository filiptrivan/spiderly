using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Exceptions
{
    public class ExpiredVerificationException : Exception
    {
        public int StatusCode { get; set; } = StatusCodes.Status400BadRequest;

        // Constructor
        public ExpiredVerificationException() : base(SharedTerms.ExpiredVerificationCodeException) { }

        // Constructor with message
        public ExpiredVerificationException(string message) : base(message) { }
    }
}
