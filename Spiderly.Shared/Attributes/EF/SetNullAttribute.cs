namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies that the property should be set to null when the parent entity is deleted. <br/>
    /// Apply this attribute to a many-to-one relationship property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SetNullAttribute : Attribute
    {

    }
}
