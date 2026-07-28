---
name: mapper-customization
description: Customize Spiderly-generated Mapster mappers. Use when overriding DTO-to-entity or entity-to-DTO mapping, adding computed fields to DTOs, customizing query projections, or implementing Customize* partial mapper hooks.
---

# Mapper Customization

## Generated Methods

Spiderly generates **4 Mapster configuration methods per entity** (3 for abstract entities) in a `partial class Mapper`:

| Method | Direction | Used By |
|---|---|---|
| `{Entity}DTOToEntityConfig()` | DTO → Entity | Save flow (mapping DTO to entity before insert/update) |
| `{Entity}ToDTOConfig()` | Entity → DTO | Main UI form, general DTO mapping |
| `{Entity}ProjectToConfig()` | Entity → DTO | Paginated list queries (projection) |
| `{Entity}ExcelProjectToConfig()` | Entity → DTO | Excel export projection |

Each method returns a `TypeAdapterConfig` with Mapster mappings. For M2O relationships, the generator automatically maps `{Nav}Id` and `{Nav}DisplayName`:

```csharp
public static TypeAdapterConfig CartToDTOConfig()
{
    TypeAdapterConfig config = new();

    config
        .NewConfig<Cart, CartDTO>()
        .Map(dest => dest.UserId, src => src.User.Id)
        .Map(dest => dest.UserDisplayName, src => src.User.Email)
        .Map(dest => dest.CartStatusId, src => src.CartStatus.Id)
        .Map(dest => dest.CartStatusDisplayName, src => src.CartStatus.Name)
        ;

    return config;
}
```

## `Customize*` Partial Hooks — Add Mappings in Real C#

Every generated config method declares a matching `static partial void Customize{MethodName}(TypeAdapterConfig config)`
hook and calls it before returning. Implement it in your hand-written `Mapper` partial class to
**add** custom mappings on top of the generated ones — compiler-checked, IntelliSense-assisted,
and able to express null guards (which a projection through an optional navigation requires):

```csharp
[SpiderlyDataMapper]
public static partial class Mapper
{
    static partial void CustomizeOrderItemProjectToConfig(TypeAdapterConfig config)
    {
        // ProductVariant is an optional nav (nullable FK) — the guard is mandatory:
        // an unguarded src.ProductVariant.ProductId LEFT JOINs to NULL and crashes the
        // EF shaper with "Nullable object must have a value" at materialization.
        config.ForType<OrderItem, OrderItemDTO>()
            .Map(dest => dest.ProductId, src => src.ProductVariant != null ? (int?)src.ProductVariant.ProductId : null);
    }
}
```

Use `config.ForType<...>()` (get-or-extend), never `NewConfig` (replace) — the generated
M2O/display-name mappings are already on the config when the hook runs. An unimplemented hook
compiles away entirely; implementing a hook whose config method you also fully overrode (see below)
is a compile error (`CS0759`), so a dead hook can't sit around silently.

### Convention flattening is OFF — unmapped extension props stay unmapped

Generated configs strip Mapster's flatten-by-name strategy (`NewStrictConfig()` in the generated
mapper). Without this, a DTO extension prop named e.g. `ShippingTierIsBulky` would *silently*
project through `src.ShippingTier.IsBulky` — and when that navigation is optional, the LEFT JOIN's
NULL crashes the shaper on a non-nullable member. With flattening off, a prop you never mapped
simply stays at its default; wire it deliberately via a `Customize*` hook.

### The hook only fills the value — the field must exist on the DTO

A mapping does **not** create the property. If `dest.ProductId` is not a property on the DTO, the
field **never appears in the generated Angular type** (`entities.generated.ts`). Declare the
property on a `partial class {Entity}DTO` extension — it merges into the generated DTO
automatically, no attribute needed — **then** map its value in the hook:

```csharp
// The property — a partial extension of the generated OrderItemDTO. No [SpiderlyDTO]
// needed: a partial that extends a generated DTO is merged in by name.
public partial class OrderItemDTO
{
    public int? ProductId { get; set; }
}
```

Then `dotnet build` the backend — the source generators run on build and the field appears
in `entities.generated.ts`. There is no separate "regenerate" command; the build is it.

> **Removed:** the string-based `[ProjectToDTO(".Map(...)")]` entity attribute. Its string DSL
> could not express a null guard (the parser choked on anything beyond a bare dotted path), which
> made unsafe projections through optional navs the *only* writable form. Move any existing usage
> into the corresponding `Customize{Entity}ProjectToConfig` hook.

## Partial Method Override — Full Control

For complex mapping logic, override the entire generated method. The generator **skips generation** for any method that already exists in the user's partial `Mapper` class (detected by method name match).

**Setup** — the user's mapper file (one per project, marked with `[SpiderlyDataMapper]`):

```csharp
using Spiderly.Shared.Attributes;

namespace MyProject.Business.DataMappers
{
    [SpiderlyDataMapper]
    public static partial class Mapper
    {
        // Override any generated method by declaring it here
    }
}
```

**Override example:**

```csharp
[SpiderlyDataMapper]
public static partial class Mapper
{
    public static TypeAdapterConfig ProductToDTOConfig()
    {
        TypeAdapterConfig config = new();

        config
            .NewConfig<Product, ProductDTO>()
            .Map(dest => dest.BrandId, src => src.Brand.Id)
            .Map(dest => dest.BrandDisplayName, src => src.Brand.Name)
            .Map(dest => dest.PriceWithTax, src => src.Price * 1.2m)
            ;

        return config;
    }
}
```

**Important:** When overriding, you take full responsibility — the generated M2O mappings won't be included automatically. Copy the generated method from `obj/.../Mapper.generated.cs` as a starting point, then add your custom mappings.

## When to Use Each Approach

| Scenario | Approach |
|---|---|
| Add computed/custom fields to any config (projection, DTO, save, Excel) | Implement the matching `Customize{Entity}{Method}` partial hook |
| Mapping through an optional navigation | `Customize*` hook with an explicit null guard |
| Replace the generated mappings wholesale | Override the full method in `Mapper.cs` |

Most projects never need custom mappers — the generated mappings handle M2O, M2M display names, and standard field-to-field mapping automatically.
