namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// Specifies the UI control type for a property. <br/> <br/>
    /// If not specified, the control type is automatically determined based on the property type: <br/>
    /// - string: TextBox (or TextArea if [StringLength] value is large) <br/>
    /// - int/long: Number <br/>
    /// - decimal: Decimal <br/>
    /// - bool: CheckBox <br/>
    /// - DateTime: Calendar <br/> <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [UIControlType(nameof(UIControlTypeCodes.Dropdown))]
    ///     public UserType Type { get; set; }
    ///     
    ///     [UIControlType(nameof(UIControlTypeCodes.TextArea))]
    ///     public string Description { get; set; }
    ///     
    ///     // Automatically becomes a TextBox
    ///     public string Name { get; set; }
    ///     
    ///     // Automatically becomes a Number
    ///     public int Age { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UIControlTypeAttribute : Attribute
    {
        public UIControlTypeAttribute(string typeName) { }
    }
}
