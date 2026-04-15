using System;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Marks a C# enum or a class-based enum (static class of string constants) as a Spiderly enum.
    /// Source generators enroll enums carrying this attribute when emitting Angular enum definitions. <br/> <br/>
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
