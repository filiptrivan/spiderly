using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Set to the many to one property to perform a `cascade` delete.
    /// We also use it to generate required validations (Backend and Frontend). 
    /// The parent entity cannot exist without the property which has this attribute.
    /// e.g.
    /// public class User : BusinessObject<long>
    /// {
    ///     [ManyToOneRequired]
    ///     [WithMany(nameof(Gender.Users))]
    ///     public virtual Gender Gender { get; set; }
    /// }
    /// </summary>
    public class ManyToOneRequiredAttribute : Attribute
    {
        
    }
}
