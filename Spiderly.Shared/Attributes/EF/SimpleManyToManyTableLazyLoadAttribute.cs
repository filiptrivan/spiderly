namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies that a table items for the many-to-many relationship administration 
    /// should be loaded lazily (on-demand) rather than eagerly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SimpleManyToManyTableLazyLoadAttribute : Attribute
    {
        public SimpleManyToManyTableLazyLoadAttribute() { }
    }
}
