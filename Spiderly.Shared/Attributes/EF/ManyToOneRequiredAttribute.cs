using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Specifies that a <i>many-to-one</i> relationship property is required and should trigger cascade delete.
    /// This attribute generates both backend and frontend validation rules to ensure the relationship
    /// is always present. <br/> <br/>
    /// 
    /// <b>When applied to a property:</b>
    /// - Enforces that the parent entity cannot exist without this relationship <br/>
    /// - Implements cascade delete behavior <br/>
    /// - Generates required validation rules for both backend and frontend <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [ManyToOneRequired]
    ///     [WithMany(nameof(Gender.Users))]
    ///     public virtual Gender Gender { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ManyToOneRequiredAttribute : Attribute
    {
    }
}
