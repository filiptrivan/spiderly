using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Marks a class for custom object mapping implementation. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [CustomMapper]
    /// public static partial class Mapper
    /// {
    ///     // Custom mapping methods
    /// }
    /// </code> <br/>
    /// 
    /// <b>Note:</b> Use this attribute when you need to implement specialized mapping logic
    /// that cannot be handled by the default mapping configuration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomMapperAttribute : Attribute
    {
        public CustomMapperAttribute()
        {

        }
    }
}
