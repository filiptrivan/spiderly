using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Grammar sweep over the full supported-shape matrix, derived from the same axis data the
/// production dispatch reads (<see cref="SpiderlyTypeRef.ScalarKindByName"/>,
/// <see cref="SpiderlyTypeRef.CollectionTypeNames"/>, <see cref="SpiderlyTypeRef.TransportWrapperNames"/>),
/// so adding an axis member automatically widens this net — no hand-written row to forget.
/// <para>
/// Unlike the characterization tests (which pin exact outputs for representative cases), this sweep
/// asserts CLASS invariants on every combination: the emitted string is valid TypeScript type
/// grammar, never carries a C# nullability marker, and never leaks a C#-only type name. The
/// nullable-enum TS17019 escape ('prop?: MyEnum?;') is the motivating instance: 'MyEnum?' was
/// simply missing from the hand-written matrix, while this sweep generates it.
/// </para>
/// </summary>
public class AngularTypeGrammarSweepTests
{
    private const string EnumCore = "MyEnum";
    private const string DtoCore = "UserDTO";
    private const string UnmappedCore = "Guid";

    private static readonly ImmutableArray<string> Enums = ImmutableArray.Create(EnumCore);

    /// <summary>
    /// TS type-reference grammar for the swept domain: a bare identifier plus array suffixes — no
    /// swept shape can produce a generic output (the only generic emission, PaginatedResultDTO,
    /// is not a shape-axis member; widen the regex if such shapes ever join the sweep). A leaked
    /// '?' or raw C# syntax fails it.
    /// </summary>
    private static readonly Regex TsTypeGrammar =
        new(@"^[A-Za-z_][A-Za-z0-9_]*(\[\])*$", RegexOptions.Compiled);

    /// <summary>
    /// Type names that must never appear as a token in emitted TS, derived mechanically: every
    /// scalar whose TS mapping differs from its own C# name (so 'string', which maps to itself,
    /// self-excludes), plus the DTO core (must emit as 'User').
    /// </summary>
    private static readonly string[] BannedOutputTokens =
        SpiderlyTypeRef.ScalarKindByName.Keys
            .Where(x => AngularTypeMapper.GetAngularType(x, Enums) != x)
            .Append(DtoCore)
            .ToArray();

    public static TheoryData<string> AllShapes()
    {
        TheoryData<string> data = new();
        IEnumerable<string> cores = SpiderlyTypeRef.ScalarKindByName.Keys
            .Append(EnumCore)
            .Append(DtoCore)
            .Append(UnmappedCore);

        foreach (string core in cores)
        {
            data.Add(core);
            data.Add($"{core}?");
            data.Add($"{core}[]");

            foreach (string collection in SpiderlyTypeRef.CollectionTypeNames)
            {
                data.Add($"{collection}<{core}>");
                data.Add($"{collection}<{core}>?");
                data.Add($"{collection}<{core}?>");
            }

            foreach (string wrapper in SpiderlyTypeRef.TransportWrapperNames)
            {
                data.Add($"{wrapper}<{core}>");
                data.Add($"{wrapper}<{core}?>");

                foreach (string collection in SpiderlyTypeRef.CollectionTypeNames)
                    data.Add($"{wrapper}<{collection}<{core}>>");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void GetAngularType_EmitsValidTsTypeGrammar(string cSharp)
    {
        string result = AngularTypeMapper.GetAngularType(cSharp, Enums);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.DoesNotContain("?", result);
        Assert.Matches(TsTypeGrammar, result);

        foreach (string token in Regex.Split(result, "[^A-Za-z0-9_]+").Where(x => x.Length > 0))
            Assert.DoesNotContain(token, BannedOutputTokens);
    }

    /// <summary>
    /// The SPIDERLY001 resolvability check compares one symbol against the known-type set, so every
    /// swept shape must reduce to a bare identifier — never a generic expression, array suffix, or a
    /// leaked C# marker (any of which could never match the set and would misreport a resolvable type).
    /// </summary>
    [Theory]
    [MemberData(nameof(AllShapes))]
    public void GetValidationTargetSymbol_EmitsABareSymbol(string cSharp)
    {
        string result = AngularTypeMapper.GetValidationTargetSymbol(cSharp, Enums);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Matches(@"^[A-Za-z_][A-Za-z0-9_]*$", result);
    }
}
