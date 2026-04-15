using System;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Marks a class as a Spiderly custom controller. Source generators only enroll classes carrying this attribute. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// [SpiderlyController]
    /// [ApiController]
    /// public class StorefrontController : ControllerBase { }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SpiderlyControllerAttribute : Attribute
    {
    }
}
