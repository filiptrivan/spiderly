using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    // FT: For now we are not doing anything with Provider because we only have Google
    // FT: I think there is no need for any validation on IdToken/BrowserId, the code will not brake, and we are not saving the data in the database
    [SpiderlyDTO]
    public class ExternalProviderDTO
    {
        //public string Provider { get; set; }
        public string IdToken { get; set; }
        public string BrowserId { get; set; }
    }
}
