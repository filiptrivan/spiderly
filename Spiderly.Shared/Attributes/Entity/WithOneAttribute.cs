using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Declares a one-to-one relationship. Place on the <b>dependent</b> (foreign-key-holding)
    /// side's single-valued reference navigation. Its presence designates this side as the dependent;
    /// the other side is the principal. <br/><br/>
    ///
    /// <b>Required vs optional:</b> add <c>[Required]</c> to make the dependent's FK non-nullable
    /// ("dependent must have a principal"). Omit it for an optional 1-1 (nullable FK, many NULLs allowed).
    /// The schema cannot enforce "principal must have a dependent" — that direction is always 0..1. <br/><br/>
    ///
    /// <b>Unidirectional:</b> use the parameterless constructor when the principal has no back-navigation. <br/><br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class Conversation : BusinessObject&lt;long&gt;
    /// {
    ///     public long? OwningTaskItemId { get; set; }          // explicit FK (recommended for code-managed)
    ///     [WithOne(nameof(TaskItem.Conversation))]
    ///     [CascadeDelete]
    ///     public virtual TaskItem OwningTaskItem { get; set; }
    /// }
    ///
    /// public class TaskItem : BusinessObject&lt;long&gt;
    /// {
    ///     public virtual Conversation Conversation { get; set; } // principal side, no attribute
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class WithOneAttribute : Attribute
    {
        /// <summary>The name of the inverse single-valued navigation on the principal entity, or null for a unidirectional 1-1.</summary>
        public string WithOne { get; set; }

        /// <param name="withOne">The name of the inverse navigation on the principal entity. Omit for unidirectional.</param>
        public WithOneAttribute(string withOne = null)
        {
            WithOne = withOne;
        }
    }
}
