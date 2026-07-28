using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Spiderly.Shared.Localization
{
    /// <summary>
    /// JSON file-based <see cref="IStringLocalizer"/> implementation.
    /// Loads flat key-value JSON files named <c>{culture}.json</c> from the
    /// <c>Translations</c> directory under <see cref="AppContext.BaseDirectory"/>.
    /// <example>
    /// <code>
    /// spiderly.UseTranslations();
    /// </code>
    /// </example>
    /// </summary>
    public class JsonStringLocalizer : IStringLocalizer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

        public JsonStringLocalizer()
        {
            string translationsPath = Path.Combine(AppContext.BaseDirectory, "Translations");

            if (!Directory.Exists(translationsPath))
                return;

            string[] jsonFiles = Directory.GetFiles(translationsPath, "*.json");

            foreach (string jsonFile in jsonFiles)
            {
                string langCode = Path.GetFileNameWithoutExtension(jsonFile);
                string json = File.ReadAllText(jsonFile);

                if (string.IsNullOrWhiteSpace(json))
                    continue;

                Dictionary<string, string>? translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (translations != null)
                    _translations[langCode] = translations;
            }
        }

        public LocalizedString this[string name]
        {
            get
            {
                string? value = GetTranslation(name);
                return value != null
                    ? new LocalizedString(name, value)
                    : new LocalizedString(name, name, resourceNotFound: true);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                string? value = GetTranslation(name);
                return value != null
                    ? new LocalizedString(name, string.Format(value, arguments))
                    : new LocalizedString(name, string.Format(name, arguments), resourceNotFound: true);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            string langCode = CultureInfo.CurrentCulture.Name;

            if (_translations.TryGetValue(langCode, out Dictionary<string, string>? translations))
            {
                foreach (KeyValuePair<string, string> kvp in translations)
                {
                    yield return new LocalizedString(kvp.Key, kvp.Value);
                }
            }
        }

        private string? GetTranslation(string key)
        {
            string langCode = CultureInfo.CurrentCulture.Name;

            if (_translations.TryGetValue(langCode, out Dictionary<string, string>? langDict)
                && langDict.TryGetValue(key, out string? value)
                && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return null;
        }
    }
}
