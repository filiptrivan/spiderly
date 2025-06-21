using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies custom mapping configuration when projecting an entity to its DTO. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [ProjectToDTO(".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)")]
    /// public class Achievement : BusinessObject&lt;long&gt;
    /// {
    ///     // Properties
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ProjectToDTOAttribute : Attribute
    {
        /// <param name="customMapper">The custom mapping expression, e.g., <i>".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)"</i></param>
        public ProjectToDTOAttribute(string customMapper) { }
    }
}
