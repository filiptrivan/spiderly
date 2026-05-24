using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies that a property should be excluded from the generated Excel export
    /// (the <c>Export{Entity}ListToExcel</c> column set), while remaining present in the DTO and
    /// the rest of the API/UI. Use it for internal/technical columns that are noise in a
    /// human-readable sheet (raw foreign-key ids, gateway correlation data, sync plumbing, etc.).
    /// <para>
    /// Place it on the property that produces the column: a scalar property excludes the matching
    /// DTO column; a many-to-one navigation property excludes the generated <c>{Nav}DisplayName</c>
    /// (and the synthesized <c>{Nav}Id</c> when there is no explicit foreign-key scalar).
    /// </para>
    /// <para>
    /// Differs from <see cref="ExcludeFromDTOAttribute"/>, which removes the property from the DTO
    /// entirely (no API exposure at all). This one only hides the column from the Excel export.
    /// </para>
    /// <para>
    /// Placement controls which column disappears, so for a many-to-one you can drop just the raw id
    /// while keeping the human-readable name (or vice-versa):
    /// <code>
    /// [ExcludeFromExcelExport]            // hides the "OrderStatusId" column...
    /// public byte OrderStatusId { get; set; }
    ///
    /// [WithMany(nameof(OrderStatus.Orders))]
    /// public virtual OrderStatus OrderStatus { get; set; }   // ...while "OrderStatusDisplayName" stays
    /// </code>
    /// Putting it on the navigation property instead (<c>OrderStatus</c>) hides
    /// <c>OrderStatusDisplayName</c>, and also <c>OrderStatusId</c> only when no explicit foreign-key
    /// scalar like the one above is declared.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcludeFromExcelExportAttribute : Attribute
    {
    }
}
