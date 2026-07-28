using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class UserBaseDTO
    {
        public string Email { get; set; } = null!;
        public long Id { get; set; }
    }
}
