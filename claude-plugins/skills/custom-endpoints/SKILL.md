---
name: custom-endpoints
description: Add custom (non-CRUD) API endpoints to a Spiderly project. Use when creating custom controllers, adding new service methods beyond generated CRUD, building storefront or webhook endpoints, calling generated services from custom code, or choosing between return types.
---

# Custom Endpoints

## Controller Patterns

### Pattern 1: Extend Generated Base Controller

Add custom methods alongside generated CRUD endpoints:

```csharp
[ApiController]
[Route("/api/[controller]/[action]")]
public class OrderController : OrderBaseController
{
    private readonly IServiceProvider _serviceProvider;

    public OrderController(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        IStringLocalizer localizer
    )
        : base(context, serviceProvider, localizer)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpGet]
    [AuthGuard]
    public async Task UpdateOrderStatus(long orderId, byte newStatusId)
    {
        OrderServiceGenerated orderService =
            _serviceProvider.GetRequiredService<OrderServiceGenerated>();
        await orderService.UpdateOrderStatus(orderId, newStatusId);
    }
}
```

The generated `OrderBaseController` already provides `GetPaginatedOrderList`, `SaveOrder`, `DeleteOrder`, etc. Your custom methods are added alongside them.

### Pattern 2: Fully Custom Controller

For endpoints with no generated entity CRUD (storefronts, webhooks), inject domain services directly:

```csharp
[ApiController]
[Route("/api/[controller]/[action]")]
public class StorefrontController : ControllerBase
{
    private readonly StorefrontCatalogService _catalogService;

    public StorefrontController(
        StorefrontCatalogService catalogService
    )
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<List<StorefrontCategoryDTO>> Categories()
    {
        return await _catalogService.GetCategoriesForDisplay();
    }

    [HttpGet]
    public async Task<ActionResult<StorefrontBrandDTO>> BrandBySlug(string slug)
    {
        StorefrontBrandDTO result = await _catalogService.GetBrandBySlug(slug);
        if (result == null) return NotFound();
        return result;
    }
}
```

### `[Controller("Name")]` — Grouping Entities

Multiple entities under one controller:

```csharp
[Controller("SecurityController")]
public class User : BusinessObject<long> { ... }

[Controller("SecurityController")]
public class Role : BusinessObject<int> { ... }
```

Generates a single `SecurityBaseController` with CRUD for both entities.

## Custom Service Methods

Create standalone service classes for custom logic. Inject `IApplicationDbContext` or `EntityServiceDependencies` as needed:

```csharp
public class StorefrontCatalogService
{
    private readonly IApplicationDbContext _context;

    public StorefrontCatalogService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StorefrontCategoryDTO>> GetCategoriesForDisplay()
    {
        return await _context.DbSet<Category>()
            .AsNoTracking()
            .Select(x => new StorefrontCategoryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
            })
            .ToListAsync();
    }
}
```

To add custom methods to a generated entity service, create an `{Entity}Service` class:

```csharp
public class OrderService : OrderServiceGenerated
{
    public OrderService(EntityServiceDependencies deps) : base(deps) { }

    public async Task UpdateOrderStatus(long orderId, byte newStatusId)
    {
        await _deps.Context.DbSet<Order>()
            .Where(x => x.Id == orderId)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.OrderStatusId, newStatusId));
    }
}
```

### Database Access Patterns

```csharp
// Simple query
List<Product> products = await _context.DbSet<Product>()
    .Where(x => x.IsActive)
    .ToListAsync();

// Eager load navigations
List<ProductVariant> variants = await _context.DbSet<ProductVariant>()
    .Include(x => x.Product)
    .Where(x => ids.Contains(x.Id))
    .ToListAsync();

// Fetch with version check (optimistic concurrency)
Notification notification = await GetInstanceAsync<Notification, long>(id, version);

// Fetch without version check
OrderStatus status = await GetInstanceAsync<OrderStatus, byte>(statusId);

// Add + save
_context.DbSet<Order>().Add(order);
await _context.SaveChangesAsync();

// Batch delete
await _context.DbSet<OrderItem>()
    .Where(x => x.Order.Id == orderId)
    .ExecuteDeleteAsync();
```

### Transactions

```csharp
StorefrontPlaceOrderResultDTO result = await _context.WithTransactionAsync(async () =>
{
    // All operations here are atomic
    _context.DbSet<Order>().Add(order);
    await _context.SaveChangesAsync();

    foreach (var item in dto.Items)
    {
        _context.DbSet<OrderItem>().Add(new OrderItem { Order = order, ... });
        variant.Stock -= item.Quantity;
    }
    await _context.SaveChangesAsync();

    return new StorefrontPlaceOrderResultDTO { OrderNumber = order.OrderNumber };
});
```

### Current User Context

```csharp
long currentUserId = _authenticationService.GetCurrentUserId();

UserWishlist wishlist = await _context.DbSet<UserWishlist>()
    .FirstOrDefaultAsync(x => x.User.Id == currentUserId);
```

### Calling Generated Service Methods

Within an entity service, call inherited methods directly. From other services, resolve the entity service via DI:

```csharp
// Within ProductService — call inherited generated methods directly
PaginatedResultDTO<ProductDTO> products = await GetPaginatedProductList(
    filterDTO, _deps.Context.DbSet<Product>(), authorize: false);

// From a different service — resolve the entity service via DI
ProductServiceGenerated productService =
    _deps.ServiceProvider.GetRequiredService<ProductServiceGenerated>();
PaginatedResultDTO<ProductDTO> products = await productService.GetPaginatedProductList(
    filterDTO, _deps.Context.DbSet<Product>(), authorize: false);

// Use the internal overload for custom projection
PaginatedResult<Product> result = await GetPaginatedProductList(filterDTO, query);
List<CustomDTO> dtos = await result.Query
    .Skip(filterDTO.First).Take(filterDTO.Rows)
    .Select(x => new CustomDTO { ... })
    .ToListAsync();
```

## Return Types

| Return Type | When to Use | HTTP Status |
|---|---|---|
| `Task<TDto>` | Single entity | 200 with JSON |
| `Task<List<TDto>>` | Entity list | 200 with JSON array |
| `Task<PaginatedResultDTO<T>>` | Paginated data | 200 with `{ data, totalRecords }` |
| `Task` (void) | Fire-and-forget actions | 200 empty |
| `Task<int>` / `Task<string>` | Scalar values | 200 with value |
| `Task<IActionResult>` | File downloads, conditional status | Varies |
| `Task<ActionResult<TDto>>` | Entity or 404 | 200 or 404 |

```csharp
// File download
return File(bytes, SettingsProvider.Current.ExcelContentType, "export.xlsx");

// Conditional 404
StorefrontBrandDTO result = await _catalogService.GetBrandBySlug(slug);
if (result == null) return NotFound();
return result;

// Webhook acknowledgement
return Ok();
```

## Exception Handling

| Type | HTTP | When |
|---|---|---|
| `BusinessException(message)` | 400 | User-facing validation errors |
| `HackerException()` | 500 (generic) | Tampering, impossible conditions |

```csharp
if (dto.Items.Count == 0)
    throw new BusinessException("Cart is empty.");

if (paymentMethod == null)
    throw new HackerException($"Invalid PaymentMethodId: {dto.PaymentMethodId}");
```

## Key Attributes

| Attribute | Purpose |
|---|---|
| `[AuthGuard]` | Require valid JWT |
| `[UIDoNotGenerate]` | Hide from Swagger / skip Angular UI generation |
| `[SkipSpinner]` | Frontend won't show loading spinner |
| `[ApiExplorerSettings(GroupName = "...")]` | Swagger grouping |
| `[FromForm]` | Bind file uploads |
| `[FromBody]` | Bind JSON body |

## DI Registration

Entity services are auto-registered by the generated `EntityServiceRegistration` class. Register custom services in `Extensions/AppServiceExtensions.cs`:

```csharp
public static class AppServiceExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // Entity services (auto-generated — registers all {Entity}ServiceGenerated + user overrides)
        services.AddEntityServices();

        // Custom services
        services.AddTransient<StorefrontCatalogService>();
        services.AddTransient<MeilisearchService>();
        services.AddTransient<IPaymentGateway, RaiAcceptPaymentGateway>();

        return services;
    }
}
```

Then call `services.AddAppServices()` in `Startup.ConfigureServices()`. Inject into controllers via constructor — the DI container resolves all dependencies automatically.

If you create an `{Entity}Service` that extends `{Entity}ServiceGenerated`, the auto-generated registration detects it and registers both the concrete type and the generated base type to resolve to your override.

## Custom DTOs

Define in `Business/DTOs/` or a similar folder:

```csharp
public class StorefrontProductDTO
{
    [Required]
    public long Id { get; set; }

    [Required]
    public string Title { get; set; }

    public decimal? SalePrice { get; set; }

    [Required]
    public string ImageUrl { get; set; }
}
```

Use `[Required]` on non-nullable fields for correct Swagger/TypeScript generation.

## Extending PermissionCodes

Add custom permission codes via partial class:

```csharp
public static partial class PermissionCodes
{
    public static string ExportReports { get; } = "ExportReports";
    public static string ManageSettings { get; } = "ManageSettings";
}
```

Then check in custom code:

```csharp
await _authorizationService.AuthorizeAndThrowAsync<User>(PermissionCodes.ExportReports);
```
