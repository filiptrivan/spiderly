using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class AngularTypeMapper
    {
        /// <summary>
        /// Pass the properties with the C# data types
        /// </summary>
        public static List<string> GetAngularImports(List<SpiderlyProperty> properties, bool generateClassImports = false, string importPath = null)
        {
            List<string> result = new();

            foreach (SpiderlyProperty prop in properties)
            {
                string cSharpDataType = prop.Type;
                if (cSharpDataType.IsBaseDataType() == false)
                {
                    string angularDataType = GetAngularDataTypeForImport(cSharpDataType);

                    if (generateClassImports && cSharpDataType.Contains($"{Helpers.DTONamespaceEnding}"))
                    {
                        result.Add($"import {{ {angularDataType} }} from \"./{importPath}entities.generated\";");
                    }
                    else if (generateClassImports && cSharpDataType.IsEnum())
                    {
                        result.Add($"import {{ {angularDataType} }} from \"../../enums/generated/{importPath}enums.generated\";"); // TODO FT: When you need, implement so you can also send enums from the controller
                    }
                }
            }

            return result.Distinct().ToList();
        }

        private static readonly HashSet<string> KnownTsScalars = new()
        {
            "string", "boolean", "Date", "number", "any"
        };

        public static bool IsKnownTsScalar(string tsType) => KnownTsScalars.Contains(tsType);

        public static string GetAngularType(string cSharpType)
        {
            switch (cSharpType)
            {
                case "string":
                    return "string";
                case "bool":
                case "bool?":
                    return "boolean";
                case "DateTime":
                case "DateTime?":
                    return "Date";
                case "long":
                case "long?":
                case "int":
                case "int?":
                case "decimal":
                case "decimal?":
                case "float":
                case "float?":
                case "double":
                case "double?":
                case "byte":
                case "byte?":
                    return "number";
                default:
                    break;
            }

            if (cSharpType.IsEnumerable())
                return $"{ExtractAngularTypeFromGenericCSharpType(cSharpType)}[]";

            if (cSharpType.IsEnum())
                return cSharpType;

            if (cSharpType.Contains(Helpers.DTONamespaceEnding) || (cSharpType.Contains("Task<") && cSharpType.Contains("ActionResult") == false)) // FT: We don't want to handle "ActionResult"
                return ExtractAngularTypeFromGenericCSharpType(cSharpType); // ManyToOne

            return "any"; // eg. "ActionResult", "Task"...
        }

        internal static string GetAngularDataTypeForImport(string CSharpDataType)
        {
            //if (ExtractAngularTypeFromGenericCSharpType(CSharpDataType).IsBaseType()) // TODO FT: We were checking for the C# type, which wasn't correct, but add correct code here if we need in the future
            //    return null;

            if (ExtractAngularTypeFromGenericCSharpType(CSharpDataType).IsEnum())
                return CSharpDataType;

            return ExtractAngularTypeFromGenericCSharpType(CSharpDataType);
        }

        /// <summary>
        /// cSharp type could be enumerable or class
        /// List<long> -> number
        /// </summary>
        internal static string ExtractAngularTypeFromGenericCSharpType(string cSharpType)
        {
            string result;

            string[] parts = cSharpType.Split('<'); // List, long>

            parts[parts.Length - 1] = parts[parts.Length - 1].Replace(">", ""); // long

            if (cSharpType.Contains("PaginatedResultDTO"))
            {
                result = $"PaginatedResult<{parts[parts.Length - 1].Replace("DTO", "")}>";
            }
            else if (cSharpType.Contains("LazyLoadSelectedIdsResultDTO"))
            {
                result = "LazyLoadSelectedIdsResult";
            }
            else if (cSharpType.Contains("NamebookDTO"))
            {
                result = "Namebook";
            }
            else if (cSharpType.Contains("CodebookDTO"))
            {
                result = "Codebook";
            }
            else if (cSharpType.Contains("IFormFile"))
            {
                result = "any";
            }
            else if (parts[parts.Length - 1].IsBaseDataType())
            {
                result = GetAngularType(parts[parts.Length - 1]); // List<long>
            }
            else
            {
                result = parts[parts.Length - 1]; // List<UserDTO>
            }

            return result.Replace(Helpers.DTONamespaceEnding, "").Replace("[]", "");
        }
    }
}
