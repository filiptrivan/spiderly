namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Generates an editable list UI for complex many-to-many relationships (junction tables with additional fields).
    /// Shows ALL entities from the "other side" with editable junction fields. No add/remove/reorder controls.
    /// <br/><br/>
    /// <b>Warning:</b> This loads all "other side" entities into the form. Suitable for small sets (e.g., 3 warehouses),
    /// not for large sets (e.g., thousands of entities).
    /// </summary>
    /// <example>
    /// <code>
    /// // On ProductVariant entity:
    /// [ComplexManyToManyList]
    /// public virtual List&lt;ProductVariantWarehouse&gt; ProductVariantWarehouses { get; } = new();
    ///
    /// // ProductVariantWarehouse (junction entity with [M2M]):
    /// [M2MWithMany(nameof(ProductVariant.ProductVariantWarehouses))]
    /// public virtual ProductVariant ProductVariant { get; set; }
    /// [M2MWithMany(nameof(Warehouse.ProductVariantWarehouses))]
    /// public virtual Warehouse Warehouse { get; set; }
    /// [GreaterThanOrEqualTo(0)]
    /// public int Stock { get; set; }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class ComplexManyToManyListAttribute : Attribute
    {

    }
}
