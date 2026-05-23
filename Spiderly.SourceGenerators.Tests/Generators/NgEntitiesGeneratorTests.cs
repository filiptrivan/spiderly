using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class NgEntitiesGeneratorTests
{
    private static readonly ImmutableArray<string> EnumRegistry = ImmutableArray.Create("ScoreType", "BmiLevel");

    private static SpiderlyClass Dto(string name, params (string Name, string Type)[] props) => new()
    {
        Name = name,
        Properties = props.Select(p => new SpiderlyProperty { Name = p.Name, Type = p.Type }).ToList(),
    };

    /// <summary>
    /// Locks the bug this method shipped with: two entities reusing one enum emitted a duplicate import
    /// (dedup was keyed on the property name and never fired), a nullable enum property leaked
    /// <c>import { BmiLevel? }</c>, and a collection of an enum would have leaked
    /// <c>import { List&lt;BmiLevel&gt; }</c>. Each enum must now produce exactly one unwrapped import.
    /// </summary>
    [Fact]
    public void GetEnumPropertyImports_ReusedNullableAndCollectionEnums_EmitsOneUnwrappedImportEach()
    {
        List<SpiderlyClass> dtoClasses = new()
        {
            Dto("ScoreDTO", ("Type", "ScoreType"), ("Label", "string")),
            Dto("InsightDTO", ("Category", "ScoreType")),                              // reuses ScoreType -> must NOT duplicate
            Dto("InteractionDTO", ("Level", "BmiLevel?"), ("Levels", "List<BmiLevel>")), // nullable + collection -> must NOT leak ? or List<>
        };

        List<string> imports = NgEntitiesGenerator.GetEnumPropertyImports(dtoClasses, EnumRegistry);

        Assert.Equal(new[]
        {
            "import { ScoreType } from \"../enums/enums.generated\";",
            "import { BmiLevel } from \"../enums/enums.generated\";",
        }, imports);
    }

    [Fact]
    public void GetEnumPropertyImports_NonEnumProperties_AreIgnored()
    {
        List<SpiderlyClass> dtoClasses = new()
        {
            Dto("UserDTO", ("Email", "string"), ("Age", "int?"), ("Manager", "UserDTO")),
        };

        Assert.Empty(NgEntitiesGenerator.GetEnumPropertyImports(dtoClasses, EnumRegistry));
    }
}
