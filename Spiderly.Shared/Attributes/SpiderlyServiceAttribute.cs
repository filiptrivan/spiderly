using System;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Marks a hand-written entity service as a Spiderly service. Source generators enroll
    /// classes carrying this attribute when composing DI registration and dependency lookups. The class
    /// is still expected to extend the generated <c>{Entity}ServiceGenerated</c> base. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// [SpiderlyService]
    /// public class ProductService : ProductServiceGenerated
    /// {
    ///     // Lifecycle hooks, custom methods
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SpiderlyServiceAttribute : Attribute
    {
    }
}
