using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Spiderly.SourceGenerators.Shared
{
    public static class AngularTypeMapper
    {
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
        /// <remarks>
        /// <paramref name="type"/> is nullable: reached both from the string overload above
        /// (<see cref="SpiderlyTypeRef.Parse"/> may return null) and recursively for a collection/wrapper's
        /// <c>ElementType</c> (also nullable). The leading null check is the single "any"-for-null fallback,
        /// so callers don't each need their own null guard.
        /// </remarks>
        public static string GetAngularType(SpiderlyTypeRef? type, ImmutableArray<string> spiderlyEnumNames)
        {
            if (type == null)
                return "any";

            // Unwrap async / MVC transport wrappers — the Angular client only ever sees the awaited body.
            // A bare wrapper with no type argument (e.g. "IActionResult", "Task") carries no typed body.
            // Matched by simple (unqualified) name, the form controllers are written in; fully-qualified
            // wrapper names (e.g. System.Threading.Tasks.Task<...>) are intentionally out of scope.
            if (type.IsTransportWrapper)
                return type.ElementType == null ? "any" : GetAngularType(type.ElementType, spiderlyEnumNames);

            // Collections recurse on the element, so arbitrary nesting (List<List<T>>, Task<List<T>>, ...) works.
            if (type.IsCollection)
                return $"{GetAngularType(type.ElementType, spiderlyEnumNames)}[]";

            // CoreName, never Raw: for a nullable enum property Raw carries the C# '?' ("MyEnum?"),
            // which is invalid TypeScript in type position. Optionality is already expressed by the
            // generated member's own '?:'.
            if (type.IsEnum(spiderlyEnumNames))
                return type.CoreName;

            // DTO leaf — checked before the scalar switch because a generic DTO like NamebookDTO<long>
            // has CoreName "long" and would otherwise be misread as a scalar.
            if (IsDtoName(type.Name))
                return GetAngularDtoType(type, spiderlyEnumNames); // ManyToOne

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

        private static bool IsDtoName(string name)
            => name != null && name.EndsWith(Helpers.DTONamespaceEnding);

        /// <summary>
        /// Maps a DTO leaf to its TS type: the parsed outer name minus the <c>DTO</c> suffix
        /// (<c>UserDTO</c> -> <c>User</c>). An EXACT suffix strip — a user DTO whose name merely
        /// contains a framework form (e.g. <c>BrandNamebookDTO</c>) stays the user's type (the legacy
        /// string parser's <c>Contains</c> sniffing collapsed it onto <c>Namebook</c>); the framework
        /// read-shapes (<c>NamebookDTO</c>, <c>CodebookDTO</c>, <c>LazyLoadSelectedIdsResultDTO</c>)
        /// coincide with the strip and need no cases of their own. <c>PaginatedResultDTO</c> is the
        /// one genuinely special form — the only generic emission, whose type argument maps through
        /// <see cref="GetAngularType(SpiderlyTypeRef, ImmutableArray{string})"/> like any other type,
        /// so a scalar argument becomes its TS scalar instead of the raw C# name.
        /// </summary>
        private static string GetAngularDtoType(SpiderlyTypeRef type, ImmutableArray<string> spiderlyEnumNames)
        {
            if (type.Name == "PaginatedResultDTO")
                return $"PaginatedResult<{GetAngularType(type.ElementType, spiderlyEnumNames)}>";

            return Helpers.RemoveDtoSuffix(type.Name); // UserDTO -> User
        }

        /// <summary>
        /// String overload — parses once, then delegates to the structured implementation.
        /// </summary>
        internal static string GetValidationTargetSymbol(string cSharpType, ImmutableArray<string> spiderlyEnumNames)
            => GetValidationTargetSymbol(SpiderlyTypeRef.Parse(cSharpType), spiderlyEnumNames);

        /// <summary>
        /// The symbol whose TS resolvability decides the SPIDERLY001 diagnostic for a controller method
        /// type that passed the caller's needs-no-validation skip-list (bare wrappers, <c>void</c>,
        /// <c>IFormFile</c>, ...): transport wrappers and collections are unwrapped, and
        /// <c>PaginatedResultDTO&lt;T&gt;</c> validates its payload <c>T</c> (the container itself is a
        /// framework base class that always resolves). Scalars come back as their TS mapping (members of
        /// <see cref="KnownTsScalars"/>), enums as the bare enum name, DTOs as the emitted class name —
        /// anything else is the unresolvable symbol the diagnostic should report.
        /// </summary>
        internal static string GetValidationTargetSymbol(SpiderlyTypeRef? type, ImmutableArray<string> spiderlyEnumNames)
        {
            while (type != null && type.ElementType != null
                && (type.IsTransportWrapper || type.IsCollection || type.Name == "PaginatedResultDTO"))
            {
                type = type.ElementType;
            }

            // TODO(nrt): returns null! here despite the non-nullable return type — a real latent null path
            // if type started null (only possible when the string overload is called with a null
            // cSharpType, or type.Name is somehow empty). The one known caller (NgControllersGenerator's
            // ValidateControllerType) always passes a real controller method's return type, so this is
            // never hit in practice; kept as-is rather than widening the contract.
            if (type == null || string.IsNullOrEmpty(type.Name))
                return null!;

            if (type.CoreName.IsBaseDataType())
                return GetAngularType(type, spiderlyEnumNames);

            if (IsDtoName(type.Name))
                return GetAngularDtoType(type, spiderlyEnumNames);

            return type.Name;
        }
    }
}
