using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies custom mapping configuration when projecting an entity to its DTO. <br/> <br/>
    /// <b>Example:</b> <br/>
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
        /// <param name="customMapper">The custom mapping expression, e.g., <b>".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)"</b></param>
        public ProjectToDTOAttribute(string customMapper) { }
    }
}
