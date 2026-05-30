namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Configures a many-to-one relationship so the decorated navigation is cleared when the referenced
    /// parent entity is deleted. Use it when child records may remain valid without their previous parent.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SetNullAttribute : Attribute
    {

    }
}
