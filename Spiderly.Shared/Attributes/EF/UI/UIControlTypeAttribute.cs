namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// Using it to specify the right control type on the UI (e.g. UIControlType(nameof(UIControlTypeCodes.Dropdown)))
    /// If you don't specify it we will guess the correct type:
    /// - string -> TextBox (TextArea if you put big [StringLength(...) value])
    /// - int/long -> Number
    /// - decimal -> Decimal
    /// - bool -> CheckBox
    /// - DateTime -> Calendar
    /// </summary>
    public class UIControlTypeAttribute : Attribute
    {
        public UIControlTypeAttribute(string typeName) { }
    }
}
