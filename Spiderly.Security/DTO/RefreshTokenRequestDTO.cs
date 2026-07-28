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
        // Nullable: absence is an expected, handled state (browser cache cleared / cookie expired) that must
        // reach the service's guard for the friendly "expired" error, not be 400'd by implicit-required binding.
        public string? RefreshToken { get; set; }
        public string? BrowserId { get; set; }
    }
}
