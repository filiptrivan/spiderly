using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// If you put this attribute on the many to one property whole parent entity will be deleted if the entity from
    /// many to one relationship get deleted
    /// e.g. user class has property gender and some of the users have saved as male in database, 
    /// if male gender is deleted, all users that were male are also deleted
    /// </summary>
    public class CascadeDeleteAttribute : Attribute
    {
    }
}
