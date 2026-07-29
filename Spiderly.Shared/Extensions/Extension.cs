using Microsoft.Extensions.Localization;
using Spiderly.Shared.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

        public static string? ToCommaSeparatedString<T>(this List<T> input, IStringLocalizer? localizer = null)
        {
            List<string> stringList = input.Select(item => item?.ToString() ?? string.Empty).ToList();

            if (stringList.Count > 1)
            {
                string andWord = localizer != null ? localizer.Translate("And").FirstCharToLower() : "and";
                return $"{string.Join(", ", stringList.Take(stringList.Count - 1))} {andWord} {stringList.Last()}";
            }
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
        /// User - true <br/>
        /// string - false <br/>
        /// List - false <br/>
        /// </summary>
        public static bool IsManyToOneType(this PropertyInfo property)
        {
            Type type = property.PropertyType;

            return type.IsClass &&
               type != typeof(string) &&
               // Type.Namespace is null for a type declared in the global namespace, which is simply not a
               // System type — so it classifies like any other reference type rather than NRE-ing here.
               type.Namespace?.StartsWith("System") != true &&
               !type.Name.StartsWith("Dictionary") &&
               !type.Name.StartsWith("List");
        }

        #endregion
    }
}
