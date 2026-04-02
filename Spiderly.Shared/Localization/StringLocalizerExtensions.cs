using Microsoft.Extensions.Localization;

namespace Spiderly.Shared.Localization
{
    public static class StringLocalizerExtensions
    {
        /// <summary>
        /// Returns the translated value for the key, or the key itself if not found.
        /// </summary>
        public static string Translate(this IStringLocalizer localizer, string key)
        {
            LocalizedString result = localizer[key];
            return result.ResourceNotFound ? key : result.Value;
        }

        /// <summary>
        /// Resolves a translated Excel export filename by trying the Excel-specific key first,
        /// then falling back to the plural/list key, then the raw key itself.
        /// <example>
        /// <code>
        /// string filename = _localizer.GetExcelTranslation("ProductExcelExportName", "ProductList");
        /// // Returns "Proizvodi" if that key has a translation, otherwise falls back to "ProductList"
        /// </code>
        /// </example>
        /// </summary>
        public static string GetExcelTranslation(this IStringLocalizer localizer, string excelKey, string pluralKey)
        {
            LocalizedString result = localizer[excelKey];

            if (!result.ResourceNotFound && !string.IsNullOrEmpty(result.Value))
                return result.Value;

            return localizer.Translate(pluralKey);
        }
    }
}
