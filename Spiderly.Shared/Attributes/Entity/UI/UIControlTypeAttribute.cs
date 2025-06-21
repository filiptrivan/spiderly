namespace Spiderly.Shared.Attributes.Entity.UI
{
    /// <summary>
    /// <b>Usage:</b> Specifies the UI control type for a property. <br/> <br/>
    /// 
    /// If not specified, the control type is automatically determined based on the property type: <br/>
    /// - <i>string</i>: <i>TextBox</i> (or <i>TextArea</i> if <i>[StringLength]</i> value is large) <br/>
    /// - <i>int/long</i>: <i>Number</i> <br/>
    /// - <i>decimal</i>: <i>Decimal</i> <br/>
    /// - <i>bool</i>: <i>CheckBox</i> <br/>
    /// - <i>DateTime</i>: <i>Calendar</i> <br/>
    /// - <i>many-to-one</i>: <i>Autocomplete</i> <br/> <br/>
    /// 
    /// <b>Example:</b>
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
