using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Disables authorization checks for CRUD operations on the decorated entity.
    /// By default, all entities require authorization for CRUD operations. <br/> <br/>
    /// 
    /// <b>Warning:</b> This attribute bypasses security checks and should be used with extreme caution.
    /// It is primarily intended for testing purposes and should generally be avoided in production environments.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DoNotAuthorizeAttribute : Attribute
    {
    }
}
