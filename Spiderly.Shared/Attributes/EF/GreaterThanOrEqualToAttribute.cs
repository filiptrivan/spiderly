using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Set this attribute to the numeric properties only. 
    /// It is used for server and client validation.
    /// </summary>
    public class GreaterThanOrEqualToAttribute : Attribute
    {
        /// <param name="number">e.g. If you put the number 10 the propertys value must be greater or equal to 10.</param>
        public GreaterThanOrEqualToAttribute(int number) { }
    }
}
