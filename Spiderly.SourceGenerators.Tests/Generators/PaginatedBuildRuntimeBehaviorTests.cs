using Spiderly.Shared.DTO;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Exceptions;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static Spiderly.SourceGenerators.Tests.Infrastructure.GeneratedBuildRuntimeHarness;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Runtime behavior of the generated <c>Build</c> — see <see cref="GeneratedBuildRuntimeHarness"/> for
/// why execution (not text pins) is the required altitude here. The contract under test (decided
/// 2026-08-10, from Sentry BACKEND-RS-1F): client garbage in the pagination payload — an unknown sort
/// field, an unknown filter key, an invalid match mode — is a 400 <see cref="BusinessException"/>
/// naming the offender, never a silent no-op and never an opaque 500. Silent no-op was the pre-existing
/// behavior and is the dangerous one: an unknown filter key returned UNFILTERED results the caller
/// would treat as filtered.
/// </summary>
public class PaginatedBuildRuntimeBehaviorTests
{
    [Fact]
    public async Task UnknownSortField_ThrowsBusinessExceptionNamingFieldAndSortableFields()
    {
        FilterDTO filterDTO = new() { Rows = 10 };
        filterDTO.MultiSortMeta.Add(new FilterSortMetaDTO { Field = "sku", Order = 1 });

        BusinessException ex = await Assert.ThrowsAsync<BusinessException>(
            () => RunBuildAsync(filterDTO, ("Hammer", 1)));

        Assert.Contains("sku", ex.Message);
        // The message must carry the self-correction material: the fields that ARE sortable.
        Assert.Contains("title", ex.Message);
        Assert.Contains("rank", ex.Message);
    }

    [Fact]
    public async Task UnknownFilterField_ThrowsBusinessExceptionNamingField()
    {
        FilterDTO filterDTO = new() { Rows = 10 };
        filterDTO.Filters["brand"] = [new FilterRuleDTO { Value = "Bosch", MatchMode = MatchModeCodes.Contains }];

        BusinessException ex = await Assert.ThrowsAsync<BusinessException>(
            () => RunBuildAsync(filterDTO, ("Hammer", 1)));

        Assert.Contains("brand", ex.Message);
    }

    [Fact]
    public async Task InvalidMatchMode_ThrowsBusinessExceptionNamingModeAndField()
    {
        FilterDTO filterDTO = new() { Rows = 10 };
        filterDTO.Filters["title"] = [new FilterRuleDTO { Value = "x", MatchMode = "gte" }];

        BusinessException ex = await Assert.ThrowsAsync<BusinessException>(
            () => RunBuildAsync(filterDTO, ("Hammer", 1)));

        Assert.Contains("gte", ex.Message);
        Assert.Contains("title", ex.Message);
    }

    [Fact]
    public async Task ClientSort_AppliesWithIdDescTieBreaker()
    {
        FilterDTO filterDTO = new() { Rows = 10 };
        filterDTO.MultiSortMeta.Add(new FilterSortMetaDTO { Field = "rank", Order = 1 });

        BuildOutcome outcome = await RunBuildAsync(filterDTO, ("A", 2), ("B", 1), ("C", 2));

        // Rank ascending puts B (rank 1) first; inside rank 2 the Id DESC tie-breaker puts C (Id 3)
        // before A (Id 1) — proving the tie-breaker COMPOSED (ThenBy) instead of re-ordering everything.
        Assert.Equal(new[] { "B", "C", "A" }, outcome.Rows.Select(r => r.Title).ToArray());
        Assert.Equal(3, outcome.TotalRecords);
    }

    [Fact]
    public async Task NoClientSort_OrdersByIdDescending()
    {
        BuildOutcome outcome = await RunBuildAsync(new FilterDTO { Rows = 10 }, ("A", 1), ("B", 2), ("C", 3));

        Assert.Equal(new[] { 3, 2, 1 }, outcome.Rows.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task KnownFilter_Filters()
    {
        FilterDTO filterDTO = new() { Rows = 10 };
        filterDTO.Filters["title"] = [new FilterRuleDTO { Value = "ham", MatchMode = MatchModeCodes.Contains }];

        BuildOutcome outcome = await RunBuildAsync(filterDTO, ("Hammer", 1), ("Drill", 2), ("hammer drill", 3));

        Assert.Equal(2, outcome.TotalRecords);
        Assert.Equal(new[] { "hammer drill", "Hammer" }, outcome.Rows.Select(r => r.Title).ToArray());
    }
}
