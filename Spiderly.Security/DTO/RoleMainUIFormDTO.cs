using Spiderly.Shared.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Security.DTO
{
    public partial class RoleMainUIFormDTO
    {
        public List<NamebookDTO<long>> UsersNamebookDTOList { get; set; }
    }
}
