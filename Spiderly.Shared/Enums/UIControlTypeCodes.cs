using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Enums
{
    /// <summary>
    /// Defines the UI control types used by the Angular code generator to render form fields.
    /// Each value maps to a spiderly-* Angular component built on PrimeNG.
    /// <para>
    /// Most types are automatically picked based on the property type, but you can override them
    /// with the <c>[UIControlType]</c> attribute:
    /// <code>[UIControlType(nameof(UIControlTypeCodes.TextArea))]</code>
    /// </para>
    /// </summary>
    public enum UIControlTypeCodes
    {
        /// <summary>
        /// Numeric input for decimal/floating-point values. Renders <c>spiderly-number</c> with fraction digits enabled.
        /// <para>Auto-detected for: <c>decimal</c>, <c>decimal?</c>, <c>float</c>, <c>float?</c>, <c>double</c>, <c>double?</c></para>
        /// <example>
        /// Example:
        /// <code>
        /// [Precision(18, 2)]
        /// public decimal Price { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Decimal,

        /// <summary>
        /// File upload control. Renders <c>spiderly-file</c> with support for image preview and dimension validation.
        /// <para>Auto-detected for: properties decorated with the <c>[BlobName]</c> attribute.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [BlobName]
        /// [ImageWidth(300)]
        /// [ImageHeight(300)]
        /// public string ProfilePictureName { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        File,

        /// <summary>
        /// Single-selection dropdown list. Renders <c>spiderly-dropdown</c>.
        /// <para>Must be set explicitly via attribute. Commonly used for many-to-one navigation properties
        /// when you want a dropdown instead of the default autocomplete.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.Dropdown))]
        /// public virtual Category Category { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Dropdown,

        /// <summary>
        /// Multi-line text input. Renders <c>spiderly-textarea</c> at full width.
        /// <para>Must be set explicitly via attribute. Useful for longer plain-text content.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.TextArea))]
        /// [StringLength(2000, MinimumLength = 1)]
        /// public string Description { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        TextArea,

        /// <summary>
        /// Single-selection with search/autocomplete capability. Renders <c>spiderly-autocomplete</c>.
        /// <para>Auto-detected for: many-to-one navigation properties (the generator creates a search method for this field).</para>
        /// <example>
        /// Example:
        /// <code>
        /// public virtual City City { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Autocomplete,

        /// <summary>
        /// Single-line text input. Renders <c>spiderly-textbox</c>.
        /// <para>Auto-detected for: <c>string</c> properties (default for strings).</para>
        /// <example>
        /// Example:
        /// <code>
        /// [StringLength(100, MinimumLength = 1)]
        /// public string FirstName { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        TextBox,

        /// <summary>
        /// Boolean toggle control. Renders <c>spiderly-checkbox</c>.
        /// <para>Auto-detected for: <c>bool</c>, <c>bool?</c></para>
        /// <example>
        /// Example:
        /// <code>
        /// public bool IsActive { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        CheckBox,

        /// <summary>
        /// Date/time picker. Renders <c>spiderly-calendar</c> with optional time selection.
        /// <para>Auto-detected for: <c>DateTime</c>, <c>DateTime?</c></para>
        /// <example>
        /// Example:
        /// <code>
        /// public DateTime? StartDate { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Calendar,

        /// <summary>
        /// Numeric input for whole numbers. Renders <c>spiderly-number</c> without fraction digits.
        /// <para>Auto-detected for: <c>int</c>, <c>int?</c>, <c>long</c>, <c>long?</c>, <c>byte</c>, <c>byte?</c></para>
        /// <example>
        /// Example:
        /// <code>
        /// public int Quantity { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Integer,

        /// <summary>
        /// Visual color picker. Renders <c>spiderly-colorpicker</c> with optional hex text input.
        /// <para>Must be set explicitly via attribute. Stores the color as a hex string.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.ColorPicker))]
        /// [StringLength(10, MinimumLength = 1)]
        /// public string BackgroundColor { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        ColorPicker,

        /// <summary>
        /// Rich text HTML editor. Renders <c>spiderly-editor</c> (Quill-based) at full width.
        /// <para>Must be set explicitly via attribute. The value is stored as HTML.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.Editor))]
        /// [StringLength(10000, MinimumLength = 1)]
        /// public string Content { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Editor,

        /// <summary>
        /// Multi-selection with search/autocomplete capability. Renders <c>spiderly-multiautocomplete</c> at full width.
        /// <para>Used for many-to-many relationships where items are selected via search.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
        /// public virtual List&lt;ArticleTag&gt; ArticleTags { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        MultiAutocomplete,

        /// <summary>
        /// Multi-selection dropdown list. Renders <c>spiderly-multiselect</c> at full width.
        /// <para>Used for many-to-many relationships where all options are shown in a dropdown.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
        /// public virtual List&lt;UserRole&gt; UserRoles { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        MultiSelect,

        /// <summary>
        /// Masked password input. Renders <c>spiderly-password</c> with optional strength indicator.
        /// <para>Must be set explicitly via attribute.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.Password))]
        /// [StringLength(100, MinimumLength = 1)]
        /// public string PasswordHash { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        Password,

        /// <summary>
        /// Read-only text display. Renders <c>spiderly-textblock</c> (non-editable).
        /// <para>Must be set explicitly via attribute. Use for fields that should be visible but not editable.</para>
        /// <example>
        /// Example:
        /// <code>
        /// [UIControlType(nameof(UIControlTypeCodes.TextBlock))]
        /// public string CreatedByUser { get; set; }
        /// </code>
        /// </example>
        /// </summary>
        TextBlock
    }
}

