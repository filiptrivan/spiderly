using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// e.g.
    /// [ProjectToDTO(".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)")]
    /// public class Achievement : BusinessObject<long>
    /// {
    ///     ...
    /// }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ProjectToDTOAttribute : Attribute
    {
        /// <param name="customMapper">e.g. ".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)"</param>
        public ProjectToDTOAttribute(string customMapper) { }
    }
}
