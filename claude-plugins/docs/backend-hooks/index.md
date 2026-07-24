---
name: backend-hooks
description: Override Spiderly lifecycle hooks to customize generated CRUD behavior. Use when overriding lifecycle hooks, customizing generated CRUD logic, adding business logic to save/delete/get operations, handling MARS exceptions or transaction issues, or throwing business/security-violation exceptions.
---

# Backend Hooks

## Inheritance Chain

```
ServiceBase (Spiderly.Shared — concrete base class)
        ↓
{Entity}ServiceGenerated (generated — per-entity virtual hooks)
        ↓
{Entity}Service (your code — override hooks here)
```

Each entity gets its own generated service class. All generated methods are `public virtual` or `protected virtual`. Override them by creating an `{Entity}Service` class that inherits from `{Entity}ServiceGenerated`. DI registration is fully auto-generated — the source generator detects your override class and registers it automatically.

The generated service receives `EntityServiceDependencies` (bundles `IApplicationDbContext`, `ExcelService`, `AuthorizationService`, `IFileManager`, `IStringLocalizer`, `IServiceProvider`). Access them via `_deps`.

## Hook Signatures by Phase

### Save Flow (execution order)

```
1. SaveBody validation (SaveBodyDTOValidationRules — usually empty)
2. OnBeforeSave{Entity}AndReturnMainUIFormDTO(SaveBodyDTO)
3. DTO validation ({Entity}DTOValidationRules — NotEmpty, Length, etc.)
4. OnBefore{Entity}IsMapped(DTO)
5. OnBefore{Entity}Insert(entity, DTO)  — or —  OnBefore{Entity}Update(entity, DTO)
6. SaveChangesAsync
7. Update M2M + ordered O2M collections
8. OnAfterSave{Entity}AndReturnMainUIFormDTO(SaveBodyDTO, MainUIFormDTO)
```

Step 2 runs **before** step 3 — this means `OnBeforeSave` can set server-generated fields (e.g., `[UIDoNotGenerate]` + `[Required]` properties like hashes or computed values) before DTO validation runs.

On the **update path**, the entity load before step 5b goes through `GetInstanceAsync(id, dto.Version)` — it throws a localized `ConcurrencyException` (HTTP 409) when the client's `Version` is stale, and the whole flow runs in a transaction. Generated saves are therefore protected by optimistic concurrency out of the box; don't add manual race-condition guards to hooks for plain concurrent edits. Mechanism and limits: `entity-design` skill, *Base Classes* (`BusinessObject` `Version`).

**Signatures:**

```csharp
// Step 2 — modify DTO before DTO validation
protected virtual async Task OnBeforeSave{Entity}AndReturnMainUIFormDTO(
    {Entity}SaveBodyDTO saveBodyDTO) { }

// Step 4 — just before DTO→Entity mapping
protected virtual async Task OnBefore{Entity}IsMapped(
    {Entity}DTO dto) { }

// Step 5a — after mapping, before insert
protected virtual async Task OnBefore{Entity}Insert(
    {Entity} entity, {Entity}DTO dto) { }

// Step 5b — after loading from DB, before update
protected virtual async Task OnBefore{Entity}Update(
    {Entity} entity, {Entity}DTO dto) { }

// Step 8 — after everything is saved
protected virtual async Task OnAfterSave{Entity}AndReturnMainUIFormDTO(
    {Entity}SaveBodyDTO saveBodyDTO,
    {Entity}MainUIFormDTO mainUIFormDTO) { }
```

### Delete Hooks

```csharp
// Before the delete — rows still exist; validation / capturing data that dies with the rows.
// Default forwards to OnBefore{Entity}ListDelete with a single-element list.
public virtual Task OnBefore{Entity}Delete({IdType} id) =>
    OnBefore{Entity}ListDelete(id.StructToList());

public virtual async Task OnBefore{Entity}ListDelete(List<{IdType}> ids) { }

// After the delete (cascades included), still inside the transaction — queries observe
// the post-delete state; writes commit or roll back atomically with the delete.
// Default forwards to OnAfter{Entity}ListDelete with a single-element list.
public virtual Task OnAfter{Entity}Delete({IdType} id) =>
    OnAfter{Entity}ListDelete(id.StructToList());

public virtual async Task OnAfter{Entity}ListDelete(List<{IdType}> deletedIds) { }
```

**When single and batch deletes need the same logic, override only the list hook** — the single-id path forwards to it automatically. Override the single hook only when the per-id behaviour genuinely diverges from the batch case.

**Recomputing denormalized aggregates belongs in the After hook**, where a plain query reads the real post-delete state. In the Before hook the doomed rows still exist, so an aggregate query there would have to exclude them by hand to simulate the future state.

### Get Hooks

```csharp
// After MainUIFormDTO is constructed (enrich with computed fields)
protected virtual async Task OnAfterGet{Entity}MainUIFormDTO(
    {Entity}MainUIFormDTO mainUIFormDTO) { }
```

### Paginated List (override the whole method)

Override in your `{Entity}Service`:

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

// Image-specific hooks (called by OnBefore*IsUploaded for raster image content
// types only — Helper.IsOptimizableImage; SVG/video/PDF pass through raw)
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

**Generated CRUD operations flush the change tracker before commit**, so you can stage tracked writes — an entity `Add`/`Update`, or `IOutbox.Enqueue` — inside any `OnBefore...` hook or the delete-side `OnAfter...Delete` hooks, and they persist atomically with the operation; no manual `SaveChangesAsync`. This holds even though the delete path deletes via untracked bulk `ExecuteDeleteAsync` — the operation still flushes whatever the hook staged. Two exceptions: the save-side `OnAfterSave...` hook runs after the save's own flush, so a tracked write staged there needs its own `SaveChangesAsync`; and in a **custom** (non-hook) `WithTransactionAsync` block you must `SaveChangesAsync` yourself — `WithTransactionAsync`'s clean-tracker-at-commit guard throws otherwise.

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
| `SecurityViolationException()` | 403 | Impossible conditions, tampering, unauthorized access |

```csharp
throw new BusinessException("Sale price must be less than regular price.");
throw new SecurityViolationException(); // logs detailed message server-side, returns generic error
```

Both surface in the admin UI automatically — the global HTTP-error interceptor toasts the `BusinessException` message (and a safe generic message for everything else). Throw and return; don't build a parallel error channel or per-call frontend handling.

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
public class ProductService : ProductServiceGenerated
{
    private readonly MeilisearchService _meilisearchService;
    private readonly StorefrontRevalidationService _storefrontRevalidationService;

    public ProductService(
        EntityServiceDependencies deps,
        MeilisearchService meilisearchService,
        StorefrontRevalidationService storefrontRevalidationService
    ) : base(deps)
    {
        _meilisearchService = meilisearchService;
        _storefrontRevalidationService = storefrontRevalidationService;
    }

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
        // Cross-entity service call
        ProductVariantServiceGenerated variantService =
            _deps.ServiceProvider.GetRequiredService<ProductVariantServiceGenerated>();

        // Side effects: indexing, cache invalidation
        await _meilisearchService.IndexProduct(mainUIFormDTO.ProductDTO.Id);
        _storefrontRevalidationService.RevalidateProducts(mainUIFormDTO.ProductDTO.Slug);
    }
}
```
