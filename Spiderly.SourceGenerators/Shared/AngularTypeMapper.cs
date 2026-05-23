using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class AngularTypeMapper
    {
        /// <summary>
        /// Pass the properties with the C# data types. <paramref name="spiderlyEnumNames"/> is the
        /// <c>[SpiderlyEnum]</c> registry used to decide whether a non-DTO type is an enum.
        /// </summary>
        public static List<string> GetAngularImports(List<SpiderlyProperty> properties, ImmutableArray<string> spiderlyEnumNames, bool generateClassImports = false, string importPath = null)
        {
            List<string> result = new();

            foreach (SpiderlyProperty prop in properties)
            {
                string cSharpDataType = prop.Type.Raw;
                if (cSharpDataType.IsBaseDataType() == false)
                {
                    string angularDataType = GetAngularDataTypeForImport(cSharpDataType, spiderlyEnumNames);

                    if (generateClassImports && cSharpDataType.Contains($"{Helpers.DTONamespaceEnding}"))
                    {
                        result.Add($"import {{ {angularDataType} }} from \"./{importPath}entities.generated\";");
                    }
                    else if (generateClassImports && cSharpDataType.IsEnum(spiderlyEnumNames))
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

        /// <summary>
        /// SpiderlyTypeRef overload — lets callers holding a parsed property type pass it directly
        /// (<c>GetAngularType(property.Type, ...)</c>) instead of reaching for <c>.Raw</c>.
        /// Delegates to the string implementation, so behavior is identical.
        /// </summary>
        public static string GetAngularType(SpiderlyTypeRef cSharpType, ImmutableArray<string> spiderlyEnumNames) => GetAngularType(cSharpType?.Raw, spiderlyEnumNames);

        public static string GetAngularType(string cSharpType, ImmutableArray<string> spiderlyEnumNames)
        {
            switch (SpiderlyTypeRef.Parse(cSharpType)?.ScalarKind ?? SpiderlyScalarKind.Other)
            {
                case SpiderlyScalarKind.String:
                    return "string";
                case SpiderlyScalarKind.Boolean:
                    return "boolean";
                case SpiderlyScalarKind.DateTime:
                    return "Date";
                case SpiderlyScalarKind.DateOnly:
                case SpiderlyScalarKind.TimeOnly:
                    return "string";
                case SpiderlyScalarKind.Integer:
                case SpiderlyScalarKind.Decimal:
                    return "number";
                default:
                    break;
            }

            if (cSharpType.IsEnumerable())
                return $"{ExtractAngularTypeFromGenericCSharpType(cSharpType, spiderlyEnumNames)}[]";

            if (cSharpType.IsEnum(spiderlyEnumNames))
                return cSharpType;

            if (cSharpType.Contains(Helpers.DTONamespaceEnding) || (cSharpType.Contains("Task<") && cSharpType.Contains("ActionResult") == false)) // FT: We don't want to handle "ActionResult"
                return ExtractAngularTypeFromGenericCSharpType(cSharpType, spiderlyEnumNames); // ManyToOne

            return "any"; // eg. "ActionResult", "Task"...
        }

        internal static string GetAngularDataTypeForImport(string CSharpDataType, ImmutableArray<string> spiderlyEnumNames)
        {
            //if (ExtractAngularTypeFromGenericCSharpType(CSharpDataType).IsBaseType()) // TODO FT: We were checking for the C# type, which wasn't correct, but add correct code here if we need in the future
            //    return null;

            if (ExtractAngularTypeFromGenericCSharpType(CSharpDataType, spiderlyEnumNames).IsEnum(spiderlyEnumNames))
                return CSharpDataType;

            return ExtractAngularTypeFromGenericCSharpType(CSharpDataType, spiderlyEnumNames);
        }

        /// <summary>
        /// cSharp type could be enumerable or class
        /// List<long> -> number
        /// </summary>
        internal static string ExtractAngularTypeFromGenericCSharpType(string cSharpType, ImmutableArray<string> spiderlyEnumNames)
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
                result = GetAngularType(parts[parts.Length - 1], spiderlyEnumNames); // List<long>
            }
            else
            {
                result = parts[parts.Length - 1]; // List<UserDTO>
            }

            return result.Replace(Helpers.DTONamespaceEnding, "").Replace("[]", "");
        }
    }
}
