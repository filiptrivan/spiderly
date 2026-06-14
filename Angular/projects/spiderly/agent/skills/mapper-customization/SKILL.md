---
name: mapper-customization
description: Customize Spiderly-generated Mapster mappers. Use when overriding DTO-to-entity or entity-to-DTO mapping, adding computed fields to DTOs, customizing query projections, or using the ProjectToDTO attribute.
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

## `[ProjectToDTO]` — Inline Custom Mappings

Add custom mappings to the projection method without overriding it. Applied to the **entity class** (not properties). `AllowMultiple = true`.

```csharp
[ProjectToDTO(".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)")]
public class Achievement : BusinessObject<long>
{
    // ...
}
```

The string is appended directly to the generated `.NewConfig<Entity, EntityDTO>()` chain. Use this for simple field mappings that the generator doesn't produce automatically.

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
| Add a simple computed field to projection | `[ProjectToDTO(".Map(...)")]` on entity class |
| Add multiple computed fields to projection | Stack multiple `[ProjectToDTO]` attributes |
| Complex mapping with conditionals or method calls | Override the full method in `Mapper.cs` |
| Change DTO → Entity mapping (e.g., ignore a field) | Override `{Entity}DTOToEntityConfig()` |
| Change Excel export projection | Override `{Entity}ExcelProjectToConfig()` |

Most projects never need custom mappers — the generated mappings handle M2O, M2M display names, and standard field-to-field mapping automatically.
