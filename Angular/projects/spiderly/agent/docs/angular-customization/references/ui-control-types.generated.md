<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# UI control types

Defines the UI control types used by the Angular code generator to render form fields. Each value maps to a spiderly-* Angular component built on PrimeNG. Most types are automatically picked based on the property type, but you can override them with the [UIControlType] attribute:

| Name | Description |
| --- | --- |
| `Decimal` | Numeric input for decimal/floating-point values. Renders spiderly-number with fraction digits enabled. Auto-detected for: decimal, decimal?, float, float?, double, double? |
| `File` | File upload control. Renders spiderly-file with support for image preview and dimension validation. Auto-detected for: properties decorated with any subclass of StorageAttribute (e.g. [DiskStorage], [S3PublicStorage], [S3PrivateStorage]). |
| `Dropdown` | Single-selection dropdown list. Renders spiderly-dropdown. Must be set explicitly via attribute. Commonly used for many-to-one navigation properties when you want a dropdown instead of the default autocomplete. |
| `TextArea` | Multi-line text input. Renders spiderly-textarea at full width. Must be set explicitly via attribute. Useful for longer plain-text content. |
| `Autocomplete` | Single-selection with search/autocomplete capability. Renders spiderly-autocomplete. Auto-detected for: many-to-one navigation properties (the generator creates a search method for this field). |
| `TextBox` | Single-line text input. Renders spiderly-textbox. Auto-detected for: string properties (default for strings). |
| `CheckBox` | Boolean toggle control. Renders spiderly-checkbox. Auto-detected for: bool, bool? |
| `Calendar` | Date/time picker. Renders spiderly-calendar with optional time selection. Auto-detected for: DateTime, DateTime? |
| `Integer` | Numeric input for whole numbers. Renders spiderly-number without fraction digits. Auto-detected for: int, int?, long, long?, byte, byte? |
| `ColorPicker` | Visual color picker. Renders spiderly-colorpicker with optional hex text input. Must be set explicitly via attribute. Stores the color as a hex string. |
| `Editor` | Rich text HTML editor. Renders spiderly-editor (Quill-based) at full width. Must be set explicitly via attribute. The value is stored as HTML. |
| `Markdown` | Markdown editor. Renders spiderly-markdown at full width — a plain textarea with a live "Preview" tab. The value is stored as raw Markdown text. Must be set explicitly via attribute. Pasting an image uploads it (when the property has an [S3PublicStorage] attribute) and inserts a standard ![](url) link; the preview is rendered with marked and is approximate vs. a consuming storefront's renderer. |
| `MultiAutocomplete` | Multi-selection with search/autocomplete capability. Renders spiderly-multiautocomplete at full width. Used for many-to-many relationships where items are selected via search. |
| `MultiSelect` | Multi-selection dropdown list. Renders spiderly-multiselect at full width. Used for many-to-many relationships where all options are shown in a dropdown. |
| `Password` | Masked password input. Renders spiderly-password with optional strength indicator. Must be set explicitly via attribute. |
