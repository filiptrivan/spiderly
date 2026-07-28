using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class EmailVerifyUIDTO
    {
        public string Subject { get; set; } = null!;

        public string Body { get; set; } = null!;
    }

}
