using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Implements cascade delete behavior in <i>many-to-one</i> relationships. When the referenced entity is deleted,
    /// all entities that reference it will automatically be deleted as well. <br/> <br/>
    /// 
    /// <b>This attribute is useful when:</b>
    /// - Child entities should not exist without their parent<br/> <br/>
    /// 
    /// <b>Example:</b>
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
