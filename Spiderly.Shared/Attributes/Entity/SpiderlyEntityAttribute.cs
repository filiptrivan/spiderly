using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Marks a class as a Spiderly entity. Source generators only enroll classes carrying this attribute. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// [SpiderlyEntity]
    /// public class Product : BusinessObject&lt;long&gt; { }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SpiderlyEntityAttribute : Attribute
    {
    }
}
