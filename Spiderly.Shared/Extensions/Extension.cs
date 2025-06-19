using Spiderly.Shared.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Spiderly.Shared.Extensions
{
    public static class Extension
    {
        #region Case Extensions

        public static string FirstCharToUpper(this string input) =>
            input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1))
            };

        public static string FirstCharToLower(this string input) =>
            input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => string.Concat(input[0].ToString().ToLower(), input.AsSpan(1))
            };

        public static string ToCommaSeparatedString<T>(this List<T> input)
        {
            List<string> stringList = input.Select(item => item?.ToString() ?? string.Empty).ToList();

            if (stringList.Count > 1)
                return $"{string.Join(", ", stringList.Take(stringList.Count - 1))} {SharedTerms.And.FirstCharToLower()} {stringList.Last()}"; // TODO FT: Add to the resources
            else
                return stringList.FirstOrDefault();
        }

        public static List<T> StructToList<T>(this T structValue)
            where T : struct
        {
            return new List<T> { structValue };
        }

        public static bool HasSpaces(this string input)
        {
            if (input.Any(x => x == ' '))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Is Type

        /// <summary>
        /// User -> true
        /// string -> false
        /// List<User> -> false
        /// </summary>
        public static bool IsManyToOneType(this PropertyInfo property)
        {
            Type type = property.PropertyType;

            return type.IsClass &&
               type != typeof(string) &&
               !type.Namespace.StartsWith("System") &&
               !type.Name.StartsWith("Dictionary") &&
               !type.Name.StartsWith("List");
        }

        #endregion

        #region ResourceManager

        public static string GetExcelTranslation(this ResourceManager manager, string excelKey, string pluralKey)
        {
            string result = manager.GetTranslation(excelKey);

            if (result == null)
                result = manager.GetTranslation(pluralKey);

            if (result == null)
                result = SharedTerms.ResourceManager.GetTranslation(pluralKey);

            return string.IsNullOrEmpty(result) ? pluralKey : result;
        }

        public static string GetTranslation(this ResourceManager manager, string key)
        {
            string result = manager.GetString(key, CultureInfo.CurrentCulture);
            return string.IsNullOrEmpty(result) ? null : result;
        }

        #endregion
    }
}
