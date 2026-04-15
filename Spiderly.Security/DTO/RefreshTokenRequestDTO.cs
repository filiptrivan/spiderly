using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class RefreshTokenRequestDTO
    {
        public string RefreshToken { get; set; }
        public string BrowserId { get; set; }
    }
}
