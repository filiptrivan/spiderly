using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// An NRT-enabled consumer annotates optional navigations (<c>Category?</c>) and nullable strings
/// (<c>string?</c>). The syntax path keeps the annotation verbatim in <c>Type.Raw</c>, so every
/// name-equality lookup (<c>x.Name == property.Type.Raw</c>) must match on <c>Type.Name</c> instead —
/// otherwise navs silently vanish from mappers/DTOs, or nav resolution faults the generator.
/// The parity facts pin the stronger invariant: in the oblivious emission mode, NRT annotations on the
/// input must not change generated output AT ALL — neither by dropping artifacts nor by leaking
/// <c>?</c> into emitted reference types (CS8632 in an oblivious consumer). The deliberate
/// annotation-propagating emission is a separate, NRT-context-keyed branch with its own tests.
/// </summary>
public class NullableAnnotatedEntityTests
{
    /// <summary>
    /// {0} — nullable-annotation marker on reference types: "?" for the annotated twin, "" for the plain one.
    /// The two sources differ ONLY in NRT annotations, so generated output must be byte-identical.
    /// </summary>
    private const string M2OSourceTemplate = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Category : BusinessObject<long>
            {
                [DisplayName]
                public string{0} Name { get; set; }

                public virtual List<Product> Products { get; } = new();
            }

            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                [DisplayName]
                public string Title { get; set; }

                public string{0} Description { get; set; }

                public int? Stock { get; set; }

                [WithMany(nameof(Category.Products))]
                public virtual Category{0} Category { get; set; }
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }
        """;

    private static string AnnotatedSource => M2OSourceTemplate.Replace("{0}", "?");
    private static string PlainSource => M2OSourceTemplate.Replace("{0}", "");

    [Fact]
    public void Mapper_ResolvesNullableAnnotatedManyToOneNav()
    {
        var driver = GeneratorTestHarness.Run<MapperGenerator>(AnnotatedSource);
        string mapper = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(s => s.HintName == "Mapper.generated.cs").SourceText.ToString();

        // The nav lookup must resolve "Category?" to the Category entity: the DisplayName
        // projection exists, and a string? DisplayName is still a string (no .ToString() fallback).
        // The nav carries `!` because an optional nav is legitimately null and both consumers of this
        // config handle that (Mapster null-checks nested access, EF LEFT JOINs it) — emitted regardless of
        // the entity's annotations, which is what keeps ObliviousOutput_IsIdentical_* below valid.
        Assert.Contains("dest.CategoryDisplayName", mapper);
        Assert.Contains("src.Category!.Name", mapper);
        Assert.DoesNotContain("src.Category!.Name.ToString()", mapper);
    }

    [Fact]
    public void Dto_EmitsManyToOneColumns_ForNullableAnnotatedNav()
    {
        var driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(AnnotatedSource);
        string dtos = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(s => s.HintName == "DTOList.generated.cs").SourceText.ToString();

        Assert.Contains("CategoryId", dtos);
        Assert.Contains("CategoryDisplayName", dtos);
    }

    [Theory]
    [InlineData(typeof(MapperGenerator))]
    [InlineData(typeof(EntitiesToDTOGenerator))]
    [InlineData(typeof(ServicesGenerator))]
    [InlineData(typeof(FluentValidationGenerator))]
    public void ObliviousOutput_IsIdentical_WithAndWithoutNrtAnnotations(Type generatorType)
    {
        var annotated = GeneratorTestHarness.Run(generatorType, AnnotatedSource).GetRunResult();
        var plain = GeneratorTestHarness.Run(generatorType, PlainSource).GetRunResult();

        Assert.All(annotated.Results, r => Assert.Null(r.Exception));
        Assert.DoesNotContain(annotated.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var annotatedSources = annotated.Results.Single().GeneratedSources;
        var plainSources = plain.Results.Single().GeneratedSources;

        Assert.Equal(
            plainSources.Select(s => s.HintName).OrderBy(h => h),
            annotatedSources.Select(s => s.HintName).OrderBy(h => h));

        foreach (var plainSource in plainSources)
        {
            string annotatedText = annotatedSources.Single(s => s.HintName == plainSource.HintName).SourceText.ToString();
            Assert.Equal(plainSource.SourceText.ToString(), annotatedText);
        }
    }

}
