using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Implements cascade delete behavior in many-to-one relationships. When the referenced entity is deleted,
    /// all entities that reference it will automatically be deleted as well.<br/><br/>
    /// This attribute is useful when:<br/>
    /// - Child entities should not exist without their parent<br/>
    /// - You want cascade delete but don't need the strict validation of [ManyToOneRequired]<br/><br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// public class Comment : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName] 
    ///     public string Text { get; set; }
    ///     
    ///     [CascadeDelete] // When the Post is deleted, all its Comments will be deleted
    ///     [WithMany(nameof(Post.Comments))]
    ///     public virtual Post Post { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class CascadeDeleteAttribute : Attribute
    {
    }
}
