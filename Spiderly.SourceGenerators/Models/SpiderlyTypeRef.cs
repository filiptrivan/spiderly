using System;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Models
{
    /// <summary>
    /// Structured, parsed view of a C# type as it appears in entity/DTO source
    /// (e.g. <c>"int?"</c>, <c>"List&lt;Foo&gt;"</c>, <c>"Foo[]"</c>, <c>"List&lt;NamebookDTO&lt;long&gt;&gt;"</c>).
    /// <para>
    /// The generators historically passed types around as raw strings and each call site
    /// re-derived nullability / collection-ness / the inner type by hand (<c>EndsWith("?")</c>,
    /// <c>ExtractTypeFromGenericType</c>, <c>IsEnumerable</c>, ...). Because the parsing was
    /// duplicated, sites drifted — e.g. the Angular enum-import emit used the raw string while
    /// the enum filter unwrapped it, so a nullable enum property leaked <c>import { Foo? }</c>.
    /// Parsing the string exactly once, here, removes that whole bug class.
    /// </para>
    /// <para>
    /// <see cref="Raw"/> / <see cref="ToString"/> return the exact original string, so a type can
    /// still be emitted verbatim into generated code without any round-trip drift.
    /// </para>
    /// </summary>
    public sealed class SpiderlyTypeRef
    {
        /// <summary>
        /// Generic outer types treated as collections. Mirrors the loose <c>IsEnumerable</c> contract
        /// (List / IList / arrays); the read-only interfaces are included for completeness.
        /// Internal (not private) because it is a supported-shape axis the grammar-sweep tests
        /// enumerate, so coverage grows automatically with the dispatch itself.
        /// </summary>
        internal static readonly string[] CollectionTypeNames =
        {
            "List", "IList", "ICollection", "IEnumerable", "IReadOnlyList", "IReadOnlyCollection"
        };

        /// <summary>
        /// Async / MVC transport wrappers the Angular mappers unwrap to the awaited body
        /// (<c>Task&lt;T&gt;</c> -> <c>T</c>). A supported-shape axis, same as
        /// <see cref="CollectionTypeNames"/> — enumerated by the grammar-sweep tests.
        /// </summary>
        internal static readonly string[] TransportWrapperNames =
        {
            "Task", "ValueTask", "ActionResult", "IActionResult"
        };

        /// <summary>
        /// Scalar-name axis: every C# scalar type name the generators support, mapped to its
        /// dispatch bucket. <see cref="ScalarKind"/> and <c>IsBaseDataType</c> are lookups into this
        /// table, so the production dispatch and everything derived from it (tests, the zoo fixture)
        /// cannot drift — adding a scalar here is the single change that fans out to all of them.
        /// (Guid is deliberately absent: it is the unmapped scalar, emitted as TS <c>any</c>.)
        /// </summary>
        internal static readonly Dictionary<string, SpiderlyScalarKind> ScalarKindByName =
            new Dictionary<string, SpiderlyScalarKind>
            {
                ["string"] = SpiderlyScalarKind.String,
                ["bool"] = SpiderlyScalarKind.Boolean,
                ["DateTime"] = SpiderlyScalarKind.DateTime,
                ["DateOnly"] = SpiderlyScalarKind.DateOnly,
                ["TimeOnly"] = SpiderlyScalarKind.TimeOnly,
                ["long"] = SpiderlyScalarKind.Integer,
                ["int"] = SpiderlyScalarKind.Integer,
                ["byte"] = SpiderlyScalarKind.Integer,
                ["decimal"] = SpiderlyScalarKind.Decimal,
                ["float"] = SpiderlyScalarKind.Decimal,
                ["double"] = SpiderlyScalarKind.Decimal,
            };

        private SpiderlyTypeRef(string raw, string name, bool isNullable, bool isCollection, SpiderlyTypeRef elementType)
        {
            Raw = raw;
            Name = name;
            IsNullable = isNullable;
            IsCollection = isCollection;
            ElementType = elementType;
        }

        /// <summary>
        /// The exact original type string. Round-trips for verbatim emission into generated code.
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// Outer nominal type name, without the nullable marker, generic arguments, or <c>[]</c>.
        /// <c>"List&lt;Foo&gt;"</c> -> <c>"List"</c>; <c>"Foo?"</c> -> <c>"Foo"</c>; <c>"Foo[]"</c> -> <c>"Foo"</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Outer trailing nullability. <c>"Foo?"</c> / <c>"List&lt;Foo&gt;?"</c> -> <c>true</c>;
        /// <c>"List&lt;Foo?&gt;"</c> -> <c>false</c> (the <c>?</c> is on the element, not the outer type).
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        /// The outer type is a collection: a recognised generic collection (<c>List&lt;&gt;</c>,
        /// <c>IList&lt;&gt;</c>, ...) or an array (<c>T[]</c>).
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        /// The outer type is an async / MVC transport wrapper (<see cref="TransportWrapperNames"/>)
        /// that the Angular mappers unwrap to the awaited body.
        /// </summary>
        internal bool IsTransportWrapper => Array.IndexOf(TransportWrapperNames, Name) >= 0;

        /// <summary>
        /// The element / type-argument for collections and generics; <c>null</c> for simple types.
        /// <c>"List&lt;Foo&gt;"</c> -> <c>Foo</c>; <c>"Foo[]"</c> -> <c>Foo</c>;
        /// <c>"NamebookDTO&lt;long&gt;"</c> -> <c>long</c>; <c>"int"</c> -> <c>null</c>.
        /// </summary>
        public SpiderlyTypeRef ElementType { get; }

        /// <summary>
        /// The innermost underlying type name, fully unwrapped of collections, generics and nullability —
        /// "what enum / entity is this really". <c>"List&lt;MyEnum&gt;"</c> -> <c>"MyEnum"</c>;
        /// <c>"MyEnum?"</c> -> <c>"MyEnum"</c>; <c>"List&lt;NamebookDTO&lt;long&gt;&gt;"</c> -> <c>"long"</c>.
        /// Matches the legacy <c>ExtractTypeFromGenericType(...).WithoutNullableSuffix()</c> result.
        /// </summary>
        public string CoreName => ElementType?.CoreName ?? Name;

        /// <summary>
        /// Buckets a scalar C# type for the Angular generators' type dispatch (TS type, form control,
        /// table filter, …). Centralizes the "which type names are integers / decimals / dates / …"
        /// membership that those per-target switches used to each repeat. Non-scalar types — collections,
        /// enums, entities, DTOs, qualified names — are <see cref="SpiderlyScalarKind.Other"/>. Nullable
        /// variants share their underlying kind (<c>int?</c> -> <see cref="SpiderlyScalarKind.Integer"/>),
        /// reproducing the old <c>case "int": case "int?":</c> lists.
        /// </summary>
        public SpiderlyScalarKind ScalarKind
        {
            get
            {
                if (IsCollection)
                    return SpiderlyScalarKind.Other;

                return CoreName != null && ScalarKindByName.TryGetValue(CoreName, out SpiderlyScalarKind kind)
                    ? kind
                    : SpiderlyScalarKind.Other;
            }
        }

        public override string ToString() => Raw;

        public override bool Equals(object obj) => obj is SpiderlyTypeRef other && other.Raw == Raw;

        public override int GetHashCode() => Raw == null ? 0 : Raw.GetHashCode();

        /// <summary>
        /// Lets construction sites and existing string-keyed call sites keep assigning a raw type string
        /// (<c>Type = "List&lt;Foo&gt;"</c>) without an explicit <see cref="Parse"/> call.
        /// </summary>
        public static implicit operator SpiderlyTypeRef(string raw) => Parse(raw);

        /// <summary>
        /// Parses a stringified C# type into its structured parts. Returns <c>null</c> for a <c>null</c> input.
        /// Single-type-argument generics and arrays are supported; multi-argument generics
        /// (e.g. <c>Dictionary&lt;,&gt;</c>) are not — they don't occur in generated entity/DTO types,
        /// matching the reach of the legacy string helpers this replaces.
        /// </summary>
        public static SpiderlyTypeRef Parse(string raw)
        {
            if (raw == null)
                return null;

            string trimmed = raw.Trim();

            bool isNullable = trimmed.EndsWith("?");
            string core = isNullable ? trimmed.Substring(0, trimmed.Length - 1).TrimEnd() : trimmed;

            // Array: Foo[]
            if (core.EndsWith("[]"))
            {
                SpiderlyTypeRef element = Parse(core.Substring(0, core.Length - 2).TrimEnd());
                return new SpiderlyTypeRef(raw, element?.Name, isNullable, isCollection: true, element);
            }

            // Generic: Outer<Inner>
            int open = core.IndexOf('<');
            if (open >= 0)
            {
                string outerName = core.Substring(0, open).Trim();
                int close = core.LastIndexOf('>');
                string innerRaw = close > open ? core.Substring(open + 1, close - open - 1).Trim() : string.Empty;
                SpiderlyTypeRef element = innerRaw.Length > 0 ? Parse(innerRaw) : null;
                return new SpiderlyTypeRef(raw, outerName, isNullable, CollectionTypeNames.Contains(outerName), element);
            }

            // Simple: Foo
            return new SpiderlyTypeRef(raw, core, isNullable, isCollection: false, elementType: null);
        }
    }

    /// <summary>
    /// Scalar classification buckets for the Angular generators' per-target type dispatch.
    /// See <see cref="SpiderlyTypeRef.ScalarKind"/>. <see cref="Other"/> covers every non-scalar type
    /// (collections, enums, entities, DTOs, qualified names).
    /// </summary>
    public enum SpiderlyScalarKind
    {
        Other = 0,
        String,
        Boolean,
        DateTime,
        DateOnly,
        TimeOnly,
        Integer,
        Decimal,
    }
}
