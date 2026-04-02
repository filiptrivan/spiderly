using Microsoft.Extensions.Localization;

namespace Spiderly.Shared.Localization
{
    /// <summary>
    /// No-op <see cref="IStringLocalizer"/> that returns the key itself as the value.
    /// Registered as the default so the app works without translations configured.
    /// </summary>
    public class PassthroughStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: true);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }
}
