using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spiderly.Shared.DTO;

namespace Spiderly.Security.DTO
{
    public partial class RoleSaveBodyDTO
    {
        public List<NamebookDTO<long>> SelectedUsersNamebookDTOList { get; set; }
    }
}
