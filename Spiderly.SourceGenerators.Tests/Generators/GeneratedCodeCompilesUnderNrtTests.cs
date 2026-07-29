using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// An NRT-enabled consumer gets generated files that declare <c>#nullable enable</c>
/// (see <see cref="GeneratedCSharpNullablePragmaTests"/>), so the emitted code must itself be
/// nullable-clean — a consumer cannot edit generated files, and with
/// <c>&lt;WarningsAsErrors&gt;Nullable&lt;/WarningsAsErrors&gt;</c> a single CS8618 there fails
/// their whole build. This compiles the generator's own output and asserts it raises no nullable
/// diagnostic. The oblivious case is asserted too, so the <c>#nullable disable</c> branch can't
/// regress into warnings either.
/// </summary>
public class GeneratedCodeCompilesUnderNrtTests
{
    // Entity shapes that drive the interesting emission branches: a required and an optional
    // string, a value scalar, an optional value scalar, a [SpiderlyEnum], a M2O nav (flattens to
    // {Nav}Id + {Nav}DisplayName), and a one-to-many collection.
    private const string Source = """
        using System.Collections.Generic;

        namespace TestApp.Business.Enums
        {
            [SpiderlyEnum]
            public enum StatusCodes { Active = 1, Archived = 2 }
        }

        namespace TestApp.Business.Entities
        {
            using TestApp.Business.Enums;

            [SpiderlyEntity]
            public class Category : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                public virtual List<Product> Products { get; } = new();
            }

            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Title { get; set; }

                public string Description { get; set; }

                public decimal Price { get; set; }

                public int? Stock { get; set; }

                public StatusCodes Status { get; set; }

                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; }
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }
        """;

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EmittedDtos_RaiseNoNullableDiagnostics(NullableContextOptions nullable)
    {
        AssertGeneratedOutputIsNullableClean<EntitiesToDTOGenerator>(nullable);
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EmittedValidationRules_RaiseNoNullableDiagnostics(NullableContextOptions nullable)
    {
        AssertGeneratedOutputIsNullableClean<FluentValidationGenerator>(nullable);
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EmittedPermissionCodes_RaiseNoNullableDiagnostics(NullableContextOptions nullable)
    {
        AssertGeneratedOutputIsNullableClean<PermissionCodesGenerator>(nullable);
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EmittedEntityServices_RaiseNoNullableDiagnostics(NullableContextOptions nullable)
    {
        AssertGeneratedOutputIsNullableClean<ServicesGenerator>(nullable);
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EmittedMapperConfigs_RaiseNoNullableDiagnostics(NullableContextOptions nullable)
    {
        AssertGeneratedOutputIsNullableClean<MapperGenerator>(nullable);
    }

    /// <summary>
    /// Asserts the generator's OWN output compiles without nullable warnings. Diagnostics are filtered
    /// to syntax trees the generator produced — the fixture's hand-written entities are deliberately
    /// un-annotated (that's what a consumer writes) and their warnings belong to the consumer, not here.
    /// </summary>
    private static void AssertGeneratedOutputIsNullableClean<TGenerator>(NullableContextOptions nullable)
        where TGenerator : IIncrementalGenerator, new()
    {
        Compilation withGenerated = GeneratorTestHarness.CompileGeneratedOutput<TGenerator>(Source, nullable);

        HashSet<SyntaxTree> generatedTrees = withGenerated.SyntaxTrees
            .Where(t => t.FilePath.Contains(".generated") || t.FilePath.Contains("SourceGenerators"))
            .ToHashSet();

        Assert.NotEmpty(generatedTrees);

        // Nullable diagnostics live in the CS86xx/CS87xx bands; CS8652 (preview-feature use) shares the
        // prefix without being one. Reference errors (CS0246/CS0234) are a harness artifact — the fixture
        // compilation doesn't link the real Spiderly assemblies — and are deliberately out of scope here.
        string[] offenders = withGenerated.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Where(d => (d.Id.StartsWith("CS86") || d.Id.StartsWith("CS87")) && d.Id != "CS8652")
            .Where(d => d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree))
            .Select(d => $"{d.Id} {d.Location.GetLineSpan().Path}{d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}")
            .Distinct()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Generated output raised {offenders.Length} diagnostic(s) under {nullable}:\n"
                + string.Join("\n", offenders));
    }
}
