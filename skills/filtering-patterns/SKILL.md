---
name: filtering-patterns
description: Customize server-side filtering, pagination, and paginated list overrides. Use when doing custom server-side filtering, overriding paginated lists, working with FilterDTO, using AdditionalFilterId for parent-child filtering, or building programmatic filters.
---

# Filtering Patterns

## FilterDTO Structure

```csharp
public class FilterDTO
{
    public Dictionary<string, List<FilterRuleDTO>> Filters { get; set; } = new();
    public int First { get; set; }          // zero-based offset
    public int Rows { get; set; }           // page size
    public List<FilterSortMetaDTO> MultiSortMeta { get; set; } = new();
    public int? AdditionalFilterIdInt { get; set; }
    public long? AdditionalFilterIdLong { get; set; }
}

public class FilterRuleDTO
{
    public object Value { get; set; }
    public MatchModeCodes MatchMode { get; set; }
    public string Operator { get; set; }    // "and" / "or"
}
```

**Match modes:** `StartsWith`, `Contains`, `Equals`, `LessThan`, `GreaterThan`, `In`

Filter dictionary keys are **camelCase DTO property names**. The generated `PaginatedResultGenerator` auto-resolves them to entity paths (e.g., `categoryDisplayName` → `x.Category.Name`).

## Override `GetPaginated{Entity}List` (Most Common)

The most common customization — pre-filter the query before pagination:

```csharp
public override async Task<PaginatedResultDTO<CommentDTO>> GetPaginatedCommentList(
    FilterDTO filterDTO,
    IQueryable<Comment> query,
    bool authorize)
{
    // Add custom WHERE clauses
    query = query.Where(x => x.IsApproved == true);

    // Parent-child filtering via AdditionalFilterId
    if (filterDTO.AdditionalFilterIdLong.HasValue)
        query = query.Where(x => x.BlogPost.Id == filterDTO.AdditionalFilterIdLong.Value);

    return await base.GetPaginatedCommentList(filterDTO, query, authorize);
}
```

Always call `base.GetPaginated{Entity}List(...)` to keep generated filtering, sorting, pagination, and authorization intact.

## Programmatic `FilterDTO<T>` API

Build filters in backend code with type-safe fluent API:

```csharp
FilterDTO<Product> filter = new FilterDTO<Product>()
    .AddFilter(x => x.Name, "widget", MatchModeCodes.Contains)
    .AddFilter(x => x.Price, 100m, MatchModeCodes.GreaterThan)
    .AddSort(x => x.CreatedAt, -1)   // -1 = descending
    .SetPagination(0, 25);

PaginatedResultDTO<ProductDTO> result = await GetPaginatedProductList(
    filter, _context.DbSet<Product>(), authorize: false);
```

Property names are auto-converted from PascalCase to camelCase.

## AdditionalFilterId Pattern

Pass a parent ID from the frontend to filter child records server-side.

**Frontend (Angular):**

```html
<spiderly-data-table
  [cols]="cols"
  [getPaginatedListObservableMethod]="getCommentListObservableMethod"
  [additionalFilterIdLong]="blogPostId">
</spiderly-data-table>
```

**Backend override:**

```csharp
public override async Task<PaginatedResultDTO<CommentDTO>> GetPaginatedCommentList(
    FilterDTO filterDTO,
    IQueryable<Comment> query,
    bool authorize)
{
    if (filterDTO.AdditionalFilterIdLong.HasValue)
        query = query.Where(x => x.BlogPost.Id == filterDTO.AdditionalFilterIdLong.Value);

    return await base.GetPaginatedCommentList(filterDTO, query, authorize);
}
```

Use `AdditionalFilterIdInt` or `AdditionalFilterIdLong` based on the parent entity's ID type.

## Custom Projection (Advanced)

For fully custom DTOs (e.g., storefront-specific), call the internal overload that returns `PaginatedResult<T>` (with query + total count), then project yourself:

```csharp
public async Task<PaginatedResultDTO<CustomDTO>> GetPaginatedProductsCustom(
    FilterDTO filterDTO, IQueryable<Product> query)
{
    PaginatedResult<Product> result = await GetPaginatedProductList(filterDTO, query);

    List<CustomDTO> dtos = await result.Query
        .Skip(filterDTO.First)
        .Take(filterDTO.Rows)
        .Select(x => new CustomDTO { Id = x.Id, Title = x.Title })
        .ToListAsync();

    return new PaginatedResultDTO<CustomDTO>
    {
        Data = dtos,
        TotalRecords = result.TotalRecords
    };
}
```
