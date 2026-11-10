using PublicCodes = Spiderly.Shared.Enums.UIControlTypeCodes;
using GeneratorCodes = Spiderly.SourceGenerators.Enums.UIControlTypeCodes;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// Guards the C# ↔ C# mirror of <c>UIControlTypeCodes</c>: the public copy in <c>Spiderly.Shared</c>, which a
/// consumer names in <c>[UIControlType(nameof(UIControlTypeCodes.X))]</c> and the framework metadata exports,
/// and the generator's copy, against which <c>Enum.TryParse</c> resolves that attribute's value. A member added,
/// renamed or renumbered on one side only drifts silently, so each difference fails here. The generator's copy
/// also carries values no consumer writes (<see cref="GeneratorOnly"/>).
/// </summary>
public class UIControlTypeCodesMirrorTests
{
    private static readonly string[] GeneratorOnly = [nameof(GeneratorCodes.Table), nameof(GeneratorCodes.None)];

    [Fact]
    public void Generator_copy_matches_the_public_enum_in_names_and_values()
    {
        Assert.NotEmpty(Enum.GetNames<PublicCodes>());
        Assert.Empty(MirrorDrift(typeof(PublicCodes), typeof(GeneratorCodes), GeneratorOnly));
    }

    [Fact]
    public void NegativeControl_a_member_renamed_renumbered_or_added_on_one_side_is_reported()
    {
        Assert.Equal(
            [
                "Second is missing from the generator's copy",
                "Third is 2 in the public enum but 5 in the generator's copy",
                "Secnod is missing from the public enum",
                "Fourth is missing from the public enum",
            ],
            MirrorDrift(typeof(SamplePublicCodes), typeof(SampleGeneratorCodes), ["Internal"]));
    }

    private enum SamplePublicCodes { First, Second, Third }

    private enum SampleGeneratorCodes { First, Secnod, Third = 5, Fourth, Internal }

    /// <summary>
    /// Each name or value the two enums disagree on, the public enum's members first, each side in value order;
    /// <paramref name="generatorOnly"/> names the generator's members the public enum may lack.
    /// </summary>
    private static List<string> MirrorDrift(Type publicEnum, Type generatorEnum, string[] generatorOnly)
    {
        OrderedDictionary<string, long> publicMembers = Members(publicEnum);
        OrderedDictionary<string, long> generatorMembers = Members(generatorEnum);

        List<string> drift = [];
        foreach ((string name, long value) in publicMembers)
        {
            if (!generatorMembers.TryGetValue(name, out long generatorValue))
                drift.Add($"{name} is missing from the generator's copy");
            else if (generatorValue != value)
                drift.Add($"{name} is {value} in the public enum but {generatorValue} in the generator's copy");
        }
        foreach (string name in generatorMembers.Keys.Where(name => !publicMembers.ContainsKey(name) && !generatorOnly.Contains(name)))
            drift.Add($"{name} is missing from the public enum");
        return drift;
    }

    /// <summary>An enum's names and values, in value order.</summary>
    private static OrderedDictionary<string, long> Members(Type enumType) =>
        new(
            Enum.GetNames(enumType).Select(name => KeyValuePair.Create(name, Convert.ToInt64(Enum.Parse(enumType, name)))),
            StringComparer.Ordinal);
}
