using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// By default all entities are authorized, if you don't want to authorize CRUD operations for some entity
    /// assign this attribute to it. 
    /// This attribute is usefull for testing purposes, be carefull when using it in production.
    /// </summary>
    public class DoNotAuthorizeAttribute : Attribute
    {
        
    }
}
