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
        /// String overload — parses once, then delegates to the structured implementation.
        /// </summary>
        public static string GetAngularType(string cSharpType, ImmutableArray<string> spiderlyEnumNames) => GetAngularType(SpiderlyTypeRef.Parse(cSharpType), spiderlyEnumNames);

        /// <summary>
        /// Maps a C# type to its TypeScript/Angular equivalent by walking the parsed
        /// <see cref="SpiderlyTypeRef"/> tree, so nesting is honoured at every level instead of being
        /// inferred from flat <c>.Raw.Contains(...)</c> sniffing on the outer type.
        /// <para>
        /// Transport wrappers (<c>Task&lt;&gt;</c>, <c>ActionResult&lt;&gt;</c>) are unwrapped to the awaited
        /// body, and collections recurse on their element. That pairing is what fixes the dropped-list bug:
        /// a <c>Task&lt;List&lt;FooDTO&gt;&gt;</c> now resolves to <c>Foo[]</c> rather than <c>Foo</c> — the
        /// old code's <c>IsEnumerable()</c> only inspected the outer <c>Task</c> and never saw the list.
        /// </para>
        /// </summary>
        public static string GetAngularType(SpiderlyTypeRef type, ImmutableArray<string> spiderlyEnumNames)
        {
            if (type == null)
                return "any";

            // Unwrap async / MVC transport wrappers — the Angular client only ever sees the awaited body.
            // A bare wrapper with no type argument (e.g. "IActionResult", "Task") carries no typed body.
            // Matched by simple (unqualified) name, the form controllers are written in; fully-qualified
            // wrapper names (e.g. System.Threading.Tasks.Task<...>) are intentionally out of scope.
            if (type.Name == "Task" || type.Name == "ValueTask" || type.Name == "ActionResult" || type.Name == "IActionResult")
                return type.ElementType == null ? "any" : GetAngularType(type.ElementType, spiderlyEnumNames);

            // Collections recurse on the element, so arbitrary nesting (List<List<T>>, Task<List<T>>, ...) works.
            if (type.IsCollection)
                return $"{GetAngularType(type.ElementType, spiderlyEnumNames)}[]";

            // CoreName, never Raw: for a nullable enum property Raw carries the C# '?' ("MyEnum?"),
            // which is invalid TypeScript in type position. Optionality is already expressed by the
            // generated member's own '?:'.
            if (type.Raw.IsEnum(spiderlyEnumNames))
                return type.CoreName;

            // DTO leaf — checked before the scalar switch because a generic DTO like NamebookDTO<long>
            // has CoreName "long" and would otherwise be misread as a scalar. ExtractAngularTypeFromGenericCSharpType
            // owns the special-type mappings (PaginatedResultDTO -> PaginatedResult<T>, NamebookDTO -> Namebook,
            // CodebookDTO -> Codebook, LazyLoadSelectedIdsResultDTO -> LazyLoadSelectedIdsResult, plain FooDTO -> Foo).
            if (type.Raw.Contains(Helpers.DTONamespaceEnding))
                return ExtractAngularTypeFromGenericCSharpType(type.Raw, spiderlyEnumNames); // ManyToOne

            // Scalars (ScalarKind already folds nullable variants into their underlying kind).
            switch (type.ScalarKind)
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

            return "any"; // eg. "Guid", bare "ActionResult"/"Task", unmapped types
        }

        internal static string GetAngularDataTypeForImport(string CSharpDataType, ImmutableArray<string> spiderlyEnumNames)
        {
            //if (ExtractAngularTypeFromGenericCSharpType(CSharpDataType).IsBaseType()) // TODO FT: We were checking for the C# type, which wasn't correct, but add correct code here if we need in the future
            //    return null;

            // Parsed CoreName, never the raw C# string: a nullable enum would otherwise leak its '?'
            // into the emitted import symbol (import { MyEnum? }).
            if (ExtractAngularTypeFromGenericCSharpType(CSharpDataType, spiderlyEnumNames).IsEnum(spiderlyEnumNames))
                return SpiderlyTypeRef.Parse(CSharpDataType).CoreName;

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
