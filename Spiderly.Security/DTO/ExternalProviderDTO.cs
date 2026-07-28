using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class ExternalProviderDTO
    {
        /// <summary>The provider code the client authenticated with (e.g. "google"). Routes the id token to the matching validator.</summary>
        public string Provider { get; set; } = null!;
        public string IdToken { get; set; } = null!;
        public string? BrowserId { get; set; }
    }
}
