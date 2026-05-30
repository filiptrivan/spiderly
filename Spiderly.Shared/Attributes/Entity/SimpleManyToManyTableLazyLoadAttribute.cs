namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Loads the generated simple many-to-many administration table on demand instead of embedding all table
    /// rows in the initial form payload. Use it for relationship tables where eager loading would make the
    /// generated UI too heavy.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SimpleManyToManyTableLazyLoadAttribute : Attribute
    {
        public SimpleManyToManyTableLazyLoadAttribute() { }
    }
}
