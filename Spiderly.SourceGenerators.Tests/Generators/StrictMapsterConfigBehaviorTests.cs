using Mapster;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Behavioral pin for the Mapster semantics the generated Mapper relies on
// (MapperGeneratorTests pins only the emitted STRINGS). Two contracts:
//
// 1. NewStrictConfig (replicated verbatim from MapperGenerator's emitted helper): removing
//    ValueAccessingStrategy.FlattenMember stops convention flattening (dest.NavX no longer
//    silently resolves through src.Nav.X — the crash class behind PACMS BACKEND-RS-1C) while
//    exact-name convention and explicit .Map keep working.
// 2. Precedence the Customize* hook docs promise: .Map is first-registration-wins (a hook
//    .Map on an already-mapped member is a no-op) and .Ignore beats an earlier .Map.
//
// If a Mapster upgrade changes any of these, this file fails instead of the docs silently lying.
public class StrictMapsterConfigBehaviorTests
{
    private class Nav
    {
        public bool IsBulky { get; set; }
        public string Name { get; set; } = "";
    }

    private class Src
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";

        // Optional by design — the flattening hazard this file pins is exactly a null nav.
        public Nav? Nav { get; set; }
    }

    private class Dst
    {
        public long Id { get; set; }

        // Both are legitimately null on a mapped instance: Title when a config .Ignore()s it,
        // NavName when the source nav is null (see the assertions below).
        public string? Title { get; set; }
        public bool? NavIsBulky { get; set; }
        public string? NavName { get; set; }
    }

    // Verbatim body of the helper MapperGenerator emits into every generated Mapper class.
    private static TypeAdapterConfig NewStrictConfig()
    {
        TypeAdapterConfig config = new();

        foreach (TypeAdapterRule rule in config.Rules)
            rule.Settings.ValueAccessingStrategies.Remove(ValueAccessingStrategy.FlattenMember);

        return config;
    }

    [Fact]
    public void StrictConfig_DoesNotFlatten_ButKeepsExactNameAndExplicitMaps()
    {
        TypeAdapterConfig config = NewStrictConfig();
        config.NewConfig<Src, Dst>()
            .Map(dest => dest.NavName, src => src.Nav != null ? src.Nav.Name : null);

        Dst dst = new Src { Id = 5, Title = "probe", Nav = new Nav { IsBulky = true, Name = "alati" } }
            .Adapt<Dst>(config);

        Assert.Null(dst.NavIsBulky); // would be true if FlattenMember still resolved Nav.IsBulky
        Assert.Equal("probe", dst.Title);
        Assert.Equal("alati", dst.NavName);
    }

    [Fact]
    public void DefaultConfig_StillFlattens_SoStrippingStaysNecessary()
    {
        TypeAdapterConfig config = new();
        config.NewConfig<Src, Dst>();

        Dst dst = new Src { Nav = new Nav { IsBulky = true } }.Adapt<Dst>(config);

        Assert.True(dst.NavIsBulky); // if Mapster ever flips the default, the strip becomes dead weight — revisit
    }

    [Fact]
    public void MapIsFirstRegistrationWins_AndIgnoreBeatsEarlierMap()
    {
        TypeAdapterConfig config = NewStrictConfig();
        config.NewConfig<Src, Dst>()
            .Map(dest => dest.NavName, src => "generated")
            .Map(dest => dest.Title, src => "generated");

        // The Customize* hook pattern: ForType after NewConfig on the same pair.
        config.ForType<Src, Dst>()
            .Map(dest => dest.NavName, src => "hook") // must be a no-op
            .Ignore(dest => dest.Title!);             // must win ('!' only to box a nullable into Mapster's Func<T, object>)

        Dst dst = new Src().Adapt<Dst>(config);

        Assert.Equal("generated", dst.NavName);
        Assert.Null(dst.Title);
    }
}
