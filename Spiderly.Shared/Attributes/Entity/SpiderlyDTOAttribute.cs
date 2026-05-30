using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Enrolls a hand-written DTO class in the Spiderly pipeline. Use it for DTOs that are not generated from
    /// an entity but still need to be visible to generated API clients, metadata, or mapping conventions.
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
