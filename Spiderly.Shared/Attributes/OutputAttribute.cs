using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Specifies the output configuration for the Source Generator. <br/> <br/>
    /// 
    /// <b>Note:</b> This is a temporary solution and may be replaced in future versions.
    /// </summary>
    public class OutputAttribute : Attribute
    {
        public OutputAttribute(string output) { }
    }
}
