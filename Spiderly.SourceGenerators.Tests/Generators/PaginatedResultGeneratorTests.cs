using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using Xunit;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Generated pagination must always produce a totally-ordered query. PostgreSQL returns
/// Skip/Take pages of an unordered query in arbitrary heap/plan order, so an admin table
/// with no active sort shows arbitrary rows first, and rows can repeat or vanish across
/// pages. Build must fall back to Id DESC when MultiSortMeta is empty, and append Id DESC
/// as a tie-breaker after user sorts (sorting by a non-unique column has the same
/// page-shuffling problem). M2M junctions have no Id, so they get no fallback.
/// (PACMS regression: unsorted admin category table surfaced arbitrary high-Id rows first.)
/// </summary>
public class PaginatedResultGeneratorTests
{
    private const string FallbackSort =
        "query = query.ApplySort(x => x.Id, ascending: false, isFirst: filterDTO.MultiSortMeta == null || filterDTO.MultiSortMeta.Count == 0);";

    private const string Source = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Item : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                public virtual List<ItemWarehouse> ItemWarehouses { get; } = new();
            }

            [SpiderlyEntity]
            public class Warehouse : BusinessObject<byte>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                public virtual List<ItemWarehouse> ItemWarehouses { get; } = new();
            }

            [M2M]
            [SpiderlyEntity]
            public class ItemWarehouse
            {
                public long ItemId { get; set; }
                [M2MWithMany(nameof(Item.ItemWarehouses))]
                public virtual Item Item { get; set; }

                public byte WarehouseId { get; set; }
                [M2MWithMany(nameof(Warehouse.ItemWarehouses))]
                public virtual Warehouse Warehouse { get; set; }

                public int Stock { get; set; }
            }
        }
        """;

    [Fact]
    public void Build_AlwaysAppliesIdFallbackOrdering_ForBusinessObjectEntities()
    {
        Assert.Contains(FallbackSort, BuildMethodOf("Item"));
        Assert.Contains(FallbackSort, BuildMethodOf("Warehouse"));
    }

    [Fact]
    public void Build_OmitsIdFallback_ForM2MJunctionWithoutId()
    {
        Assert.DoesNotContain("ApplySort(x => x.Id", BuildMethodOf("ItemWarehouse"));
    }

    /// <summary>
    /// [GenerateCommaSeparatedDisplayName] over a KEYLESS junction is genuinely unsupported: the emitted
    /// filter case is <c>values.Contains(x.Id)</c>, so this generator really does need the child's id type,
    /// and a keyless junction has none. Unlike the collection controls in <c>SpiderlyClassFactory</c>, there
    /// is no branch to move the lookup into — the answer is required.
    /// <para>
    /// The consumer must be told which property is at fault — SPIDERLY029, raised by
    /// <c>CommaSeparatedDisplayNameValidator</c> from <c>EntityValidationGenerator</c> (see
    /// <c>CommaSeparatedDisplayNameDiagnosticTests</c>). THIS test owns the other half: that this generator
    /// merely omits the filter case and keeps emitting. It previously threw the diagnostic itself, which
    /// aborted <c>Execute</c> — the Filtering file was never emitted, so every entity lost <c>Build()</c>
    /// and the consumer got a CS0103 wall instead of the located error.
    /// </para>
    /// </summary>
    [Fact]
    public void CommaSeparatedDisplayName_OverAKeylessJunction_DegradesWithoutKillingTheFile()
    {
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Item : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    [GenerateCommaSeparatedDisplayName]
                    public virtual List<ItemWarehouse> ItemWarehouses { get; } = new();
                }

                [SpiderlyEntity]
                public class Warehouse : BusinessObject<byte>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    public virtual List<ItemWarehouse> ItemWarehouses { get; } = new();
                }

                [M2M]
                [SpiderlyEntity]
                public class ItemWarehouse
                {
                    public long ItemId { get; set; }
                    [M2MWithMany(nameof(Item.ItemWarehouses))]
                    public virtual Item Item { get; set; }

                    public byte WarehouseId { get; set; }
                    [M2MWithMany(nameof(Warehouse.ItemWarehouses))]
                    public virtual Warehouse Warehouse { get; set; }

                    public int Stock { get; set; }
                }
            }
            """;

        GeneratorRunResult result = GeneratorTestHarness.Run<PaginatedResultGenerator>(source)
            .GetRunResult().Results.Single();

        Assert.Null(result.Exception);
        Assert.Empty(result.Diagnostics);

        // One bad property must not delete Build() for every OTHER entity in the project.
        string generated = Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Contains("Build(IQueryable<Item> query", generated);
        Assert.Contains("Build(IQueryable<Warehouse> query", generated);
        // ...but the unfilterable column is simply absent, rather than emitting a broken case.
        Assert.DoesNotContain("itemWarehousesCommaSeparated", generated);
    }

    /// <summary>
    /// A project can hold [SpiderlyDTO] classes without a single [SpiderlyEntity] — Spiderly.Security is
    /// exactly that shape. This generator's pipeline collects entities, DTOs and data mappers, so the
    /// early `classes.Count == 0` return doesn't fire, and it then indexes `currentProjectEntities[0]`
    /// on an empty list. EntitiesToDTOGenerator and ServicesGenerator already guard this; the failure
    /// surfaces as a warning-level CS8785 with the generator silently contributing nothing.
    /// </summary>
    [Fact]
    public void DtoOnlyProject_WithNoEntities_DoesNotFaultTheGenerator()
    {
        const string dtoOnlySource = """
            namespace TestApp.Business.DTO
            {
                [SpiderlyDTO]
                public class SomeDTO
                {
                    public string Name { get; set; }
                }
            }
            """;

        GeneratorRunResult result = GeneratorTestHarness.Run<PaginatedResultGenerator>(dtoOnlySource)
            .GetRunResult().Results.Single();

        Assert.Null(result.Exception);
    }

    private static readonly Lazy<SyntaxTree> GeneratedTree = new(() =>
        GeneratorTestHarness.Run<PaginatedResultGenerator>(Source).GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("PaginatedResultGenerator.generated.cs")));

    /// <summary>
    /// One entity's Build overload, selected structurally by its parameter type so
    /// assertions can't accidentally match another entity's Build.
    /// </summary>
    private static string BuildMethodOf(string entityName)
    {
        return GeneratedTree.Value.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == "Build"
                && m.ParameterList.Parameters[0].Type?.ToString() == $"IQueryable<{entityName}>")
            .ToString();
    }
}
