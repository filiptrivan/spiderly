using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Marks a hand-written DTO class for inclusion in the Spiderly pipeline.
    /// Generated DTOs (<i>{Entity}DTO</i>, <i>{Entity}SaveBodyDTO</i>, <i>{Entity}MainUIFormDTO</i>) do not need this attribute. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// [SpiderlyDTO]
    /// public class GenerateApiKeyResponseDTO { }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SpiderlyDTOAttribute : Attribute
    {
    }
}
