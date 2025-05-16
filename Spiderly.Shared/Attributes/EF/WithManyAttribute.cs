using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Specifies the collection navigation property name in a related entity for establishing
    /// a bidirectional relationship in Entity Framework. <br/> <br/>
    /// 
    /// <b>Purpose:</b> This attribute is used to define the inverse navigation property in a relationship,
    /// enabling proper relationship configuration and navigation in both directions. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class Course : BusinessObject&lt;long&gt; 
    /// {
    ///     public virtual List&lt;Student&gt; Students { get; set; } = new();
    /// }
    /// 
    /// public class Student : BusinessObject&lt;long&gt;
    /// {
    ///     [WithMany(nameof(Course.Students))]
    ///     public virtual Course Course { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class WithManyAttribute : Attribute
    {
        public string WithMany { get; set; }

        /// <param name="withMany">The name of the collection navigation property in the related entity.</param>
        public WithManyAttribute(string withMany) 
        {
            WithMany = withMany;    
        }
    }
}
