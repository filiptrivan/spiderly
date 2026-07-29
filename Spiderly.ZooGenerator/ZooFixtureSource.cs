using System.Text;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.ZooGenerator;

/// <summary>
/// Single source for the "type zoo" e2e fixture: one entity property per supported shape axis —
/// every scalar in <see cref="SpiderlyTypeRef.ScalarKindByName"/> plus a <c>[SpiderlyEnum]</c>
/// enum, each in non-nullable and nullable form. <see cref="Generate"/> renders the fixture
/// entity file the e2e app compiles end-to-end (C#, EF migration, and the generated Angular TS);
/// <see cref="ShapeProperties"/> exposes the same model so unit tests can push the identical
/// property set through the generators' emission seams without duplicating the axis walk.
/// <para>
/// Motivating escape: a nullable enum property emitted <c>prop?: MyEnum?;</c> — invalid
/// TypeScript (TS17019) — and no hand-written fixture entity had one, so it shipped in two
/// releases before a consumer created the first nullable enum. The zoo makes "no fixture had
/// one" structurally impossible for the shape axes: a new axis member grows this fixture (and
/// the e2e's compile coverage) automatically.
/// </para>
/// </summary>
public static class ZooFixtureSource
{
    /// <summary>Enum type name declared by the fixture; also the [SpiderlyEnum] registry entry for tests.</summary>
    public const string EnumTypeName = "ZooCodes";

    /// <summary>
    /// The axis members that are reference types — their <c>?</c> form is an NRT annotation, only legal
    /// syntax in an annotated context, so they render into <see cref="NullableReferenceShapeProperties"/>
    /// (the <c>#nullable enable</c> entity) instead of the oblivious <see cref="ShapeProperties"/>.
    /// Read from the axis itself, so adding a reference-type scalar there grows this fixture automatically.
    /// </summary>
    private static HashSet<string> ReferenceTypeScalars => SpiderlyTypeRef.ReferenceTypeScalarNames;

    /// <summary>
    /// The full shape axis as (C# type, property name) pairs, in the exact order
    /// <see cref="Generate"/> renders them. Computed once — the axis is compile-time constant.
    /// </summary>
    public static readonly IReadOnlyList<(string Type, string Name)> ShapeProperties = BuildShapeProperties();

    /// <summary>
    /// The nullable-REFERENCE shape axis (<c>string?</c> today), rendered into a second fixture
    /// entity under a file-level <c>#nullable enable</c>. Split from <see cref="ShapeProperties"/>
    /// so <c>ZooShape</c> keeps exercising the nullable-oblivious source context while
    /// <c>ZooShapeNullable</c> exercises the annotated one — both branches of the generators'
    /// NRT handling stay compiled end-to-end. Auto-grows with the axis like everything else here.
    /// </summary>
    public static readonly IReadOnlyList<(string Type, string Name)> NullableReferenceShapeProperties = BuildNullableReferenceShapeProperties();

    /// <summary>
    /// The REQUIREDNESS half of the axis: one <c>[Required]</c> property per scalar (plus the enum).
    /// Requiredness is a shape axis for DTO emission — it decides whether the generated DTO member is
    /// <c>int</c> or <c>int?</c>, and whether a reference type carries <c>= null!</c> — so the fixture
    /// has to compile both sides of it, not just the optional one. Rendered onto <c>ZooShape</c>
    /// alongside <see cref="ShapeProperties"/>; auto-grows with the axis like everything else here.
    /// </summary>
    public static readonly IReadOnlyList<(string Type, string Name)> RequiredShapeProperties = BuildRequiredShapeProperties();

    private static IReadOnlyList<(string Type, string Name)> BuildShapeProperties()
    {
        List<(string, string)> properties = new()
        {
            (EnumTypeName, "CodesValue"),
            ($"{EnumTypeName}?", "CodesNullableValue"),
        };

        foreach (string scalar in SpiderlyTypeRef.ScalarKindByName.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            properties.Add((scalar, $"{Pascal(scalar)}Value"));

            // Reference-type '?' variants live on ZooShapeNullable (see NullableReferenceShapeProperties).
            if (!ReferenceTypeScalars.Contains(scalar))
                properties.Add(($"{scalar}?", $"{Pascal(scalar)}NullableValue"));
        }

        return properties;
    }

    private static IReadOnlyList<(string Type, string Name)> BuildRequiredShapeProperties()
    {
        List<(string, string)> properties = new() { (EnumTypeName, "CodesRequiredValue") };

        foreach (string scalar in SpiderlyTypeRef.ScalarKindByName.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            properties.Add((scalar, $"{Pascal(scalar)}RequiredValue"));
        }

        return properties;
    }

    private static IReadOnlyList<(string Type, string Name)> BuildNullableReferenceShapeProperties()
    {
        List<(string, string)> properties = new();

        foreach (string scalar in SpiderlyTypeRef.ScalarKindByName.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!ReferenceTypeScalars.Contains(scalar))
                continue;

            properties.Add(($"{scalar}?", $"{Pascal(scalar)}NullableValue"));
        }

        return properties;
    }

    /// <summary>
    /// Invariant casing on purpose — <c>Extensions.FirstCharToUpper</c> is culture-sensitive and would
    /// break the byte-identical output contract on e.g. tr-TR machines ("int" -&gt; "İnt"). Do not route
    /// this through that helper.
    /// </summary>
    private static string Pascal(string scalar) => char.ToUpperInvariant(scalar[0]) + scalar.Substring(1);

    /// <summary>
    /// Renders one fixture property. Every emitted shape goes through here so the property form exists
    /// once — the only differences between the three axes are the two arguments.
    /// </summary>
    private static void AppendShapeProperty(StringBuilder body, string type, string name, bool required)
    {
        body.AppendLine();

        if (required)
            body.AppendLine("        [Required]");

        if (type.WithoutNullableSuffix() == "string")
            // MinimumLength pairs with the NotEmpty that [Required] emits, mirroring the Name property
            // both fixture entities declare — so the required twin exercises that rule combination.
            body.AppendLine(required
                ? "        [StringLength(100, MinimumLength = 1)]"
                : "        [StringLength(100)]");

        // A non-nullable reference type needs the '= null!;' initializer the framework's own convention
        // prescribes — apps scaffolded by `spiderly init` compile under NRT, so an un-initialized
        // 'string' here would warn (CS8618) in the e2e fixture app.
        string initializer = ReferenceTypeScalars.Contains(type) ? " = null!;" : "";

        body.AppendLine($"        public {type} {name} {{ get; set; }}{initializer}");
    }

    /// <summary>
    /// Renders the complete ZooShapes.cs fixture file (with the e2e __APP_NAME__ placeholder).
    /// LF-normalized regardless of platform/checkout so the drift guards compare byte-identical
    /// output everywhere (same determinism contract as the metadata pipeline).
    /// </summary>
    public static string Generate()
    {
        StringBuilder body = new();

        foreach ((string type, string name) in ShapeProperties)
            AppendShapeProperty(body, type, name, required: false);

        foreach ((string type, string name) in RequiredShapeProperties)
            AppendShapeProperty(body, type, name, required: true);

        StringBuilder nullableBody = new();

        foreach ((string type, string name) in NullableReferenceShapeProperties)
            AppendShapeProperty(nullableBody, type, name, required: false);

        return $$"""
// <auto-generated>
//     GENERATED by Spiderly.ZooGenerator — do not hand-edit.
//     Regenerate: tools/regen-metadata.sh (CI and the pre-commit hook fail on drift).
// </auto-generated>
//
// The "type zoo": one entity property per supported shape axis — every scalar in
// SpiderlyTypeRef.ScalarKindByName and a [SpiderlyEnum] enum, in non-nullable and nullable form —
// so the generated C# and Angular TS for every shape is compiled by the real toolchain in the e2e
// job. Derived from the same axis data the production dispatch reads: adding an axis member grows
// this fixture automatically. Relational and blob shapes are covered by the hand-written fixture
// entities (Project, ProjectTask, Product, ...), which carry the semantics a data walk can't invent.
//
// ZooShape lives in the app's default (nullable-oblivious) context; ZooShapeNullable sits under a
// file-level '#nullable enable' and carries the reference-type shapes in NRT-annotated form
// ('string?'), so both source contexts the generators must handle are compiled end-to-end.

using System.ComponentModel.DataAnnotations;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;
using __APP_NAME__.Business.Enums;

namespace __APP_NAME__.Business.Enums
{
    [SpiderlyEnum]
    public enum {{EnumTypeName}}
    {
        Alpha = 1,
        Beta = 2,
    }
}

namespace __APP_NAME__.Business.Entities
{
    [SpiderlyEntity]
    [DoNotAuthorize]
    public class ZooShape : BusinessObject<int>
    {
        [DisplayName]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;
{{body.ToString().TrimEnd('\n', '\r')}}
    }

#nullable enable

    [SpiderlyEntity]
    [DoNotAuthorize]
    public class ZooShapeNullable : BusinessObject<int>
    {
        [DisplayName]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;
{{nullableBody.ToString().TrimEnd('\n', '\r')}}
    }
}
""".Replace("\r\n", "\n") + "\n";
    }
}
