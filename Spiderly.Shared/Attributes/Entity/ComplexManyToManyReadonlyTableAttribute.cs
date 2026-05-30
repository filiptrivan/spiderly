namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Renders a complex many-to-many relationship as a read-only table in the generated UI. Use this for
    /// junction entities that have additional fields when the parent form should display the related rows
    /// without allowing edits from that screen.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ComplexManyToManyReadonlyTableAttribute : Attribute
    {

    }
}
