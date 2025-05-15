using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies a custom controller name for an entity, overriding the default naming convention. <br/>
    /// This attribute allows grouping multiple related entities under a single controller. <br/> <br/>
    /// <b>Default Behavior:</b> <br/>
    /// Controllers are named as '{EntityName}Controller' <br/> <br/>
    /// <b>This attribute allows you to:</b> <br/>
    /// - Specify a custom controller name <br/>
    /// - Group multiple related entities under one controller <br/> <br/>
    /// <b>Example usage:</b> <br/>
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
