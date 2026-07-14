using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Marks a property as <b>server-owned</b>: the client may read it, but can never
    /// write it (mirrors OpenAPI <c>readOnly</c>). The property stays in the read <c>{Entity}DTO</c>
    /// — so it's still returned by GET endpoints and shown in the list table — but the generator
    /// closes the write path for it: no inbound FluentValidation rule is emitted, and the generated
    /// DTO&#8594;entity Mapster config <c>.Ignore()</c>s it, so a crafted payload can't tamper with it.
    /// It also gets no editable control in the generated details form. <br/> <br/>
    ///
    /// Use for values only backend code assigns — usage counters, denormalized aggregates, computed
    /// timestamps. Combine with <c>[Required]</c> to document the non-null DB column without
    /// generating an unsatisfiable inbound rule (the form never sends it). This differs from
    /// <c>[UIDoNotGenerate]</c> (hidden from the form but still writable and validated) and
    /// <c>[ExcludeFromDTO]</c> (dropped from every DTO, unreadable). <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class DiscountCode : BusinessObject&lt;int&gt;
    /// {
    ///     public string Code { get; set; }
    ///
    ///     [ReadOnly]
    ///     [Required]
    ///     public int TimesUsed { get; set; } // only backend code increments it; client reads only
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ReadOnlyAttribute : Attribute
    {
    }
}
