using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class SimpleSaveResultDTO
    {
        /// <summary>
        /// The new id to reroute on the frontend.
        /// </summary>
        public string Id { get; set; } = null!;
    }
}
