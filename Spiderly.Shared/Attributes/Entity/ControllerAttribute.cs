using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies a custom controller name for an entity, overriding the default naming convention.
    /// This attribute allows grouping multiple related entities under a single controller. <br/> <br/>
    /// 
    /// <b>Default behavior without 'Controller' attribute:</b> Controllers are named as <i>'{EntityName}Controller'</i> <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [Controller("SecurityController")]
    /// public class User { }
    /// 
    /// [Controller("SecurityController")]
    /// public class Role { }
    /// 
    /// [Controller("SecurityController")]
    /// public class Permission { }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ControllerAttribute : Attribute
    {
        /// <param name="controllerName">The custom name for the controller (e.g., "SecurityController").</param>
        public ControllerAttribute(string controllerName) 
        {
            
        }
    }
}
