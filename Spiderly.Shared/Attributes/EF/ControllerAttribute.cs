using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    public class ControllerAttribute : Attribute
    {
        /// <summary>
        /// Set this attribute on the entities for which you do not want the controller to be called {entityName}Controller, 
        /// but to give it a custom name and possibly group more entities to that controller
        /// e.g. [Controller("SecurityController")], and you could assign this name to all entities that are related to
        /// security (User, Role, Permission).
        /// </summary>
        public ControllerAttribute(string controllerName) 
        {
            
        }
    }
}
