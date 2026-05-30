using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Disables generated authorization checks for CRUD operations on the decorated entity. By default,
    /// Spiderly protects generated entity operations with authorization requirements.
    /// <br/> <br/>
    /// 
    /// <b>Warning:</b> This attribute bypasses security checks and should be used with extreme caution.
    /// It is primarily intended for testing purposes and should generally be avoided in production environments.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DoNotAuthorizeAttribute : Attribute
    {
    }
}
