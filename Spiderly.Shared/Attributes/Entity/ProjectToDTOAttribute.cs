using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Adds a custom Mapster projection expression to the generated entity-to-DTO mapping. Use it when a DTO
    /// property must be populated from a nested value, computed expression, or other projection that Spiderly
    /// cannot infer from the entity shape alone.
    /// <br/> <br/>
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
        /// <param name="customMapper">The Mapster projection expression appended to the generated mapping configuration.</param>
        public ProjectToDTOAttribute(string customMapper) { }
    }
}
