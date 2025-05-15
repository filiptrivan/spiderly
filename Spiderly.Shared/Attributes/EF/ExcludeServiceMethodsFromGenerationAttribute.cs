using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Prevents the generation of standard business service methods for the decorated property
    /// in the BusinessServiceGenerated class. <br/> <br/>
    /// <b>Use this attribute when you want to:</b> <br/>
    /// - Implement custom business logic instead of using generated methods <br/>
    /// - Override the default generated behavior with your own implementation <br/>
    /// - Exclude specific properties from the standard service method generation <br/> <br/>
    /// The property will still be part of the entity, but no service methods will be
    /// generated for it in the BusinessServiceGenerated class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcludeServiceMethodsFromGenerationAttribute : Attribute
    {
        public ExcludeServiceMethodsFromGenerationAttribute() { }
    }
}
