using System;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// Enrolls a C# enum, or a static class that represents string enum values, for Spiderly enum generation.
    /// Decorated types are exported to the generated Angular contracts so client code can use the same named
    /// values as the backend.
    /// <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// [SpiderlyEnum]
    /// public enum StatusCodes
    /// {
    ///     Active,
    ///     Inactive,
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class)]
    public class SpiderlyEnumAttribute : Attribute
    {
    }
}
