using System.Linq;
using Microsoft.CodeAnalysis;
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
        string generated = GetGeneratedPaginatedResult();

        Assert.Contains(FallbackSort, BuildSectionOf(generated, "Item"));
        Assert.Contains(FallbackSort, BuildSectionOf(generated, "Warehouse"));
    }

    [Fact]
    public void Build_OmitsIdFallback_ForM2MJunctionWithoutId()
    {
        string generated = GetGeneratedPaginatedResult();

        Assert.DoesNotContain("ApplySort(x => x.Id", BuildSectionOf(generated, "ItemWarehouse"));
    }

    private static string GetGeneratedPaginatedResult()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<PaginatedResultGenerator>(Source);

        GeneratedSourceResult generatedSource = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(s => s.HintName.StartsWith("PaginatedResultGenerator.generated"));

        return generatedSource.SourceText.ToString();
    }

    /// <summary>
    /// Slices the generated file down to one entity's Build method so assertions can't
    /// accidentally match another entity's overload ("IQueryable&lt;Item&gt;" is a prefix
    /// of "IQueryable&lt;ItemWarehouse&gt;", hence the " query" suffix in the marker).
    /// </summary>
    private static string BuildSectionOf(string generated, string entityName)
    {
        string marker = $"Build(IQueryable<{entityName}> query";
        int start = generated.IndexOf(marker);
        Assert.True(start >= 0, $"No Build method generated for {entityName}");

        int end = generated.IndexOf("public static async Task<PaginatedResult<", start + marker.Length);
        return end < 0 ? generated[start..] : generated[start..end];
    }
}
