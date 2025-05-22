namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Specifies that the property should be set to <i>null</i> when the parent entity is deleted. <br/>
    /// Apply this attribute to a <i>many-to-one</i> relationship property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SetNullAttribute : Attribute
    {

    }
}
