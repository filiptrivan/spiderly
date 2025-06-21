namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies that a table items for the <i>many-to-many</i> relationship administration 
    /// should be loaded lazily (on-demand) rather than eagerly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SimpleManyToManyTableLazyLoadAttribute : Attribute
    {
        public SimpleManyToManyTableLazyLoadAttribute() { }
    }
}
