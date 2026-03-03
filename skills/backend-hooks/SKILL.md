---
name: backend-hooks
description: Override Spiderly lifecycle hooks to customize generated CRUD behavior
triggers:
  - overriding lifecycle hooks
  - customizing generated CRUD logic
  - adding business logic to save/delete/get operations
  - MARS exceptions or transaction issues
  - throwing business or hacker exceptions
---

# Backend Hooks

## Inheritance Chain

```
BusinessServiceBase (Spiderly.Shared — concrete base class)
        ↓
BusinessServiceGenerated (generated — all virtual hooks live here)
        ↓
BusinessService (your code — override hooks here)
```

All generated methods are `public virtual` or `protected virtual`. Override them in your `BusinessService` (partial class).

## Hook Signatures by Phase

### Save Flow (execution order)

```
1. OnBeforeSave{Entity}AndReturnMainUIFormDTO(SaveBodyDTO)
2. OnBefore{Entity}IsMapped(DTO)
3. OnBefore{Entity}Insert(entity, DTO)  — or —  OnBefore{Entity}Update(entity, DTO)
4. SaveChangesAsync
5. Update M2M + ordered O2M collections
6. OnAfterSave{Entity}AndReturnMainUIFormDTO(SaveBodyDTO, MainUIFormDTO)
```

**Signatures:**

```csharp
// Step 1 — modify DTO before anything else
protected virtual async Task OnBeforeSave{Entity}AndReturnMainUIFormDTO(
    {Entity}SaveBodyDTO saveBodyDTO) { }

// Step 2 — just before DTO→Entity mapping
protected virtual async Task OnBefore{Entity}IsMapped(
    {Entity}DTO dto) { }

// Step 3a — after mapping, before insert
protected virtual async Task OnBefore{Entity}Insert(
    {Entity} entity, {Entity}DTO dto) { }

// Step 3b — after loading from DB, before update
protected virtual async Task OnBefore{Entity}Update(
    {Entity} entity, {Entity}DTO dto) { }

// Step 6 — after everything is saved
protected virtual async Task OnAfterSave{Entity}AndReturnMainUIFormDTO(
    {Entity}SaveBodyDTO saveBodyDTO,
    {Entity}MainUIFormDTO mainUIFormDTO) { }
```

### Delete Hooks

```csharp
public virtual async Task OnBefore{Entity}Delete({IdType} id) { }
public virtual async Task OnBefore{Entity}ListDelete(List<{IdType}> ids) { }
```

### Get Hooks

```csharp
// After MainUIFormDTO is constructed (enrich with computed fields)
protected virtual async Task OnAfterGet{Entity}MainUIFormDTO(
    {Entity}MainUIFormDTO mainUIFormDTO) { }
```

### Paginated List (override the whole method)

```csharp
public override async Task<PaginatedResultDTO<{Entity}DTO>> GetPaginated{Entity}List(
    FilterDTO filterDTO, IQueryable<{Entity}> query, bool authorize)
{
    query = query.Where(x => x.IsActive);
    return await base.GetPaginated{Entity}List(filterDTO, query, authorize);
}
```

### Blob/File Upload Hooks

```csharp
// Before upload authorization
public virtual async Task OnBefore{Property}BlobFor{Entity}UploadIsAuthorized(
    IFormFile file, {IdType} id) { }

// Before upload to storage (transform bytes, validate)
public virtual async Task<byte[]> OnBefore{Property}BlobFor{Entity}IsUploaded(
    Stream stream, IFormFile file, {IdType} id) { }

// Image-specific hooks (called by OnBefore*IsUploaded for image/* content types)
public virtual async Task ValidateImageFor{Property}Of{Entity}(
    Stream stream, IFormFile file, {IdType} id) { }
public virtual async Task<byte[]> OptimizeImageFor{Property}Of{Entity}(
    Stream stream, IFormFile file, {IdType} id) { }
```

### Relationship Hooks

```csharp
// Customize the base query for M2M autocomplete/dropdown
protected virtual async Task<IQueryable<{Related}>> GetAll{Property}QueryFor{Entity}(
    IQueryable<{Related}> query) { return query; }
```

## All Hooks Run Inside Transactions

Every generated method wraps its logic in `_context.WithTransactionAsync(...)`. Nested calls reuse the existing transaction. You do **not** need to start your own transaction in hooks.

If you need a transaction in **custom** (non-hook) methods:

```csharp
await _context.WithTransactionAsync(async () =>
{
    // all DB operations here are transactional
});
```

## Exception Types

| Type | HTTP Status | When to Use |
|---|---|---|
| `BusinessException(message)` | 400 | Validation the user can trigger through normal UI usage |
| `HackerException()` | 500 (generic) | Impossible conditions, tampering, unauthorized access |

```csharp
throw new BusinessException("Sale price must be less than regular price.");
throw new HackerException(); // logs detailed message server-side, returns generic error
```

## PostgreSQL MARS Pitfall

EF Core on PostgreSQL does **not** support Multiple Active Result Sets. You'll get a `NpgsqlOperationInProgressException` if you enumerate two queries concurrently.

**Fix 1 — Materialize with `.Select()` before starting another query:**

```csharp
// BAD — lazy enumeration holds the connection open
var dict = await _context.DbSet<Product>().ToDictionaryAsync(x => x.Id, x => x.Name);

// GOOD — materialize first
var dict = await _context.DbSet<Product>()
    .Select(x => new { x.Id, x.Name })
    .ToDictionaryAsync(x => x.Id, x => x.Name);
```

**Fix 2 — Use `.Include()` instead of lazy loading navigation properties:**

```csharp
// BAD — accessing Product.Category triggers lazy load while connection is busy
var products = await _context.DbSet<Product>().ToListAsync();
var names = products.Select(p => p.Category.Name); // MARS error

// GOOD — eager load
var products = await _context.DbSet<Product>()
    .Include(p => p.Category)
    .ToListAsync();
```

## Real-World Example

```csharp
protected override async Task OnBeforeSaveProductAndReturnMainUIFormDTO(
    ProductSaveBodyDTO saveBodyDTO)
{
    // Validate variant prices
    foreach (ProductVariantSaveBodyDTO v in saveBodyDTO.OrderedProductVariantsSaveBodyDTO)
    {
        if (v.ProductVariantDTO.SalePrice >= v.ProductVariantDTO.Price)
            throw new BusinessException("Sale price must be less than regular price.");
    }

    // Compute aggregate fields
    saveBodyDTO.ProductDTO.Price = saveBodyDTO.OrderedProductVariantsSaveBodyDTO[0]
        .ProductVariantDTO.Price;
    saveBodyDTO.ProductDTO.Stock = saveBodyDTO.OrderedProductVariantsSaveBodyDTO
        .Sum(v => v.ProductVariantDTO.Stock ?? 0);
}

protected override async Task OnAfterSaveProductAndReturnMainUIFormDTO(
    ProductSaveBodyDTO saveBodyDTO,
    ProductMainUIFormDTO mainUIFormDTO)
{
    // Side effects: indexing, cache invalidation
    await _meilisearchService.IndexProduct(mainUIFormDTO.ProductDTO.Id);
    _storefrontRevalidationService.RevalidateTag("products");
}
```
