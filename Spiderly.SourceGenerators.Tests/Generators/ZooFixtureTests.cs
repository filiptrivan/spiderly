using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;
using Spiderly.ZooGenerator;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Guards the generated type-zoo fixture (tests/e2e-fixtures/backend/entities/ZooShapes.cs — see
/// <see cref="ZooFixtureSource"/> for the full motivation): the committed artifact must match the
/// generator (the unit-level mirror of the CI/pre-commit drift check), and the full shape axis must
/// survive the real entities-TS emission seam the original escape came through. The e2e job remains
/// the end-to-end judge (it compiles the zoo with the real toolchain); these tests catch a broken
/// zoo or a shape-axis leak locally, before a CI round-trip.
/// </summary>
public class ZooFixtureTests
{
    [Fact]
    public void CommittedZooFixture_MatchesGeneratorOutput()
    {
        string committedPath = Path.Combine(RepoRoot(), "tests", "e2e-fixtures", "backend", "entities", "ZooShapes.cs");

        Assert.True(File.Exists(committedPath), $"Missing committed zoo fixture: {committedPath}");
        Assert.Equal(
            ZooFixtureSource.Generate().Replace("\r\n", "\n"),
            File.ReadAllText(committedPath).Replace("\r\n", "\n"));
    }

    [Fact]
    public void ZooShapeProperties_EmitValidTsPropertyDefinitions()
    {
        List<SpiderlyProperty> properties = ZooFixtureSource.ShapeProperties
            .Select(x => new SpiderlyProperty { Name = x.Name, Type = x.Type })
            .ToList();

        List<string> definitions = NgEntitiesGenerator.GetAllAngularPropertyDefinitions(
            properties, ImmutableArray.Create(ZooFixtureSource.EnumTypeName));

        Assert.Equal(properties.Count, definitions.Count);

        // The only legal '?' in an emitted definition is the member's own optionality marker '?:'.
        foreach (string definition in definitions)
            Assert.DoesNotMatch(@"\?(?!:)", definition);
    }

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", ".."));
}
