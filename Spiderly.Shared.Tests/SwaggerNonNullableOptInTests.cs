using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Spiderly.Shared.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Whether the OpenAPI spec reflects C# nullability is a WIRE-CONTRACT decision, so it is opt-in and
/// off by default: turning it on flips generated-DTO members from optional-and-nullable to required in
/// the spec, which regenerates every consumer's typed client. Consumers schedule that as its own
/// deploy rather than inheriting it from a framework upgrade.
/// <para>
/// Off is also the safe default for a NULLABLE-OBLIVIOUS consumer, where it would be a no-op anyway:
/// Swashbuckle reads runtime <c>NullabilityInfo</c>, and generated code for such a consumer is emitted
/// under <c>#nullable disable</c>, which reports Unknown and is treated as nullable.
/// </para>
/// </summary>
public class SwaggerNonNullableOptInTests
{
    private static SwaggerGenOptions Resolve(Action<IServiceCollection> configure)
    {
        ServiceCollection services = new();
        configure(services);

        return services.BuildServiceProvider().GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
    }

    [Fact]
    public void SwaggerGen_DoesNotReflectCSharpNullability_ByDefault()
    {
        SwaggerGenOptions options = Resolve(services => services.SpiderlyAddSwaggerGen());

        Assert.False(options.SchemaGeneratorOptions.SupportNonNullableReferenceTypes);
    }

    [Fact]
    public void SwaggerGen_ReflectsCSharpNullability_WhenOptedIn()
    {
        SwaggerGenOptions options = Resolve(services =>
            services.SpiderlyAddSwaggerGen(o => o.SupportNonNullableReferenceTypes()));

        Assert.True(options.SchemaGeneratorOptions.SupportNonNullableReferenceTypes);
    }

    [Fact]
    public void ConfigureRunsAfterSpiderlysDefaults()
    {
        // Ordering is the seam's whole contract: a consumer's callback has to see the configured
        // options, not a blank slate it would then have to re-establish.
        bool sawSpiderlyDefaults = false;

        Resolve(services => services.SpiderlyAddSwaggerGen(o =>
            sawSpiderlyDefaults = o.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey("v1")));

        Assert.True(sawSpiderlyDefaults);
    }
}
