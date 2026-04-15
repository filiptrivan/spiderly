using System;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Marks a class as a hand-written Spiderly data mapper. Source generators enroll classes
    /// carrying this attribute when composing Mapster configuration with user-provided overrides. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// [SpiderlyDataMapper]
    /// public static partial class Mapper
    /// {
    ///     // Custom mapping methods
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SpiderlyDataMapperAttribute : Attribute
    {
    }
}
