using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Generates a string property in the DTO containing comma-separated 
    /// display names for a collection property in the entity. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class Project : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string Name { get; set; }
    ///     
    ///     [GenerateCommaSeparatedDisplayName]
    ///     public virtual List&lt;User&gt; TeamMembers { get; set; } = new();
    /// }
    /// 
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string Email { get; set; }
    /// }
    /// 
    /// // In the UI table, the TeamMembers column will show:
    /// // "john@example.com, jane@example.com, bob@example.com"
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class GenerateCommaSeparatedDisplayNameAttribute : Attribute
    {
        public GenerateCommaSeparatedDisplayNameAttribute() { }
    }
}
