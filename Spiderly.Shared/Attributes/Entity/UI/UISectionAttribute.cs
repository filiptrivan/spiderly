using System;

namespace Spiderly.Shared.Attributes.Entity.UI
{
    /// <summary>
    /// <b>Usage:</b> Groups the property into a named section (card) on the generated details page.
    /// All properties sharing the same section name render together inside one panel, in the order the
    /// properties are declared; sections themselves are ordered by first appearance (the position of the
    /// first property that declares the section). Properties without this attribute collapse into a single
    /// implicit, headerless section positioned by the same first-appearance rule — so a newly added
    /// property always shows up automatically, either in its declared section or the implicit one. <br/> <br/>
    ///
    /// The argument is a Transloco translation key used as the section header (e.g. <i>"Security"</i>
    /// resolves through <c>t('Security')</c>), matching how other generated panel titles are translated. <br/> <br/>
    ///
    /// <b>Backward compatibility:</b> if no property on the entity declares this attribute, the details
    /// page renders as before (a single panel with one grid). Sectioning activates only when at least one
    /// property is annotated. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     // No section -> implicit headerless section
    ///     public string Name { get; set; }
    ///
    ///     [UISection("Security")]
    ///     public string Password { get; set; } // Goes to "Security" section
    ///
    ///     [UISection("Preferences")]
    ///     public bool ReceiveNotifications { get; set; } // Goes to "Preferences" section
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UISectionAttribute : Attribute
    {
        /// <param name="sectionName">Transloco translation key used as the section's header.</param>
        public UISectionAttribute(string sectionName) { }
    }
}
