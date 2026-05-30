using System;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// Enrolls a hand-written ASP.NET Core controller in the Spiderly pipeline so the framework can discover
    /// its actions and expose them to generated client/UI code. Use it on custom controllers that should be
    /// treated as part of the Spiderly API surface.
    /// <br/> <br/>
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
