using Spiderly.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Security.Interfaces
{
    public interface IUser : IBusinessObject<long>
    {
        public string Email { get; set; }

        public bool? HasLoggedInWithGoogleAsExternalProvider { get; set; }

        public bool? IsDisabled { get; set; }

        IReadOnlyCollection<IRole> Roles { get; }
    }
}
