---
name: entity-design
description: Design Spiderly entities with correct attributes, relationships, and UI mappings. Use when creating or modifying entity classes, choosing entity attributes, setting up relationships (M2O, M2M, ordered O2M), configuring file uploads on entities, or asking about UI control mapping.
---

# Entity Design

## Required attribute

Every hand-written entity class must carry `[SpiderlyEntity]`. Without it, the source generators ignore the class — no generated DTO, mapper, controller, validator, or Angular form.

```csharp
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace Foo.Business.Entities
{
    [SpiderlyEntity]
    public class Product : BusinessObject<long>
    {
        public string Name { get; set; }
    }
}
```

Hand-written DTOs use `[SpiderlyDTO]`. Generated DTOs (`{Entity}DTO`, `{Entity}SaveBodyDTO`, `{Entity}MainUIFormDTO`) need no marker. The `spiderly add-new-entity` CLI emits `[SpiderlyEntity]` automatically.

## Base Classes

| Base Class          | Use When               | Generated                                        |
| ------------------- | ---------------------- | ------------------------------------------------ |
| `BusinessObject<T>` | Full CRUD entity       | Id, Version, CreatedAt, ModifiedAt + CRUD UI/API |
| `ReadonlyObject<T>` | Lookup/reference table | Id only, read-only operations                    |

`T` = `long` (default), `int`, or `byte`.

## Property Rules

- Navigation properties **must** be `virtual`: `public virtual Brand Brand { get; set; }`
- Collections use `List<T>` (not `IList<T>`), initialized inline: `public virtual List<Comment> Comments { get; } = new();`
- Explicit FK properties (`BrandId` alongside `Brand`) are **supported and recommended for hot paths** — see *Explicit FK properties* below
- `[StringLength(X)]` **without** `MinimumLength` = **exact length** validation. Always use `[StringLength(X, MinimumLength = Y)]` for range
- `[Required]` on navigation properties makes the relationship required (non-nullable FK)

## Explicit FK properties

Default: declare only the navigation (`public virtual Brand Brand { get; set; }`). Spiderly uses EF Core's shadow FK convention (`"BrandId"` column) and generated mappers read it via `EF.Property<>()`. For most admin entities this is fine.

**Declare an explicit FK scalar** when the entity is in a hot path:

```csharp
public long? BrandId { get; set; }
[WithMany(nameof(Brand.Products))]
public virtual Brand Brand { get; set; }
```

### When to use it

- **Hand-written save/sync code** that builds the entity directly (`new Order { BrandId = id, ... }`) — skips the `FindAsync` + navigation-attach roundtrip that the naive pattern requires
- **Hot read paths** with `ProjectToDTO` — the mapper emits `x.BrandId` instead of the `EF.Property<long>(x, "BrandId")` workaround for [EF Core #15826](https://github.com/dotnet/efcore/issues/15826), which otherwise still forces a JOIN in some queries

### Rules

- Naming convention: `{NavigationName}Id` — resolved automatically. Use `[ForeignKey(nameof(OtherName))]` only when you need a different scalar name.
- Nullability must match the relationship: `[Required]` navigation → non-nullable scalar (`long BrandId`); optional nav (`[SetNull]`) → nullable scalar (`long? BrandId`). Mismatch raises **SPID001**.
- Scalar type must match the parent's `Id` type (`byte`/`int`/`long`). Mismatch raises **SPID003**.

### Caveat — generated CRUD still loads the nav

The generated `Save{Entity}AndReturnDTO` keeps loading the parent via `FindAsync` even when an explicit FK is declared, because the returned DTO's `{Nav}DisplayName` fields read `poco.Nav.DisplayProperty`. Declaring the explicit FK does **not** speed up generated admin CRUD saves — it only helps when you write the save/sync logic yourself and never round-trip through `Save{Entity}AndReturnDTO`.

### Don't bother when

Small admin-only entities with low write volume (banners, announcements, lookup tables without hot reads). The boilerplate isn't worth it — shadow FK stays idiomatic.

## Relationships Quick Reference

### Many-to-One

```csharp
public class Comment : BusinessObject<long>
{
    [CascadeDelete] // or [SetNull] for optional
    [WithMany(nameof(Post.Comments))]
    public virtual Post Post { get; set; }
}
```

**Delete behavior:**

| Attribute         | FK nullable? | On parent delete          |
| ----------------- | ------------ | ------------------------- |
| `[CascadeDelete]` | No           | Delete all children       |
| `[SetNull]`       | Yes          | Set FK to null            |
| Neither           | No           | Block delete (EF default) |

### Simple Many-to-Many

`[M2MWithMany]` is treated as an implicit `[Required]` — junction rows must have both sides, so do **not** add `[Required]` on these navigations. If you declare an explicit FK scalar alongside, it must be non-nullable (e.g. `long CartId`, not `long? CartId`).

```csharp
[M2M]
[SpiderlyEntity]
public class RolePermission
{
    [CascadeDelete]
    [M2MWithMany(nameof(Role.Permissions))]
    public virtual Role Role { get; set; }

    [CascadeDelete]
    [M2MWithMany(nameof(Permission.Roles))]
    public virtual Permission Permission { get; set; }
}
```

Junction entity must have exactly 2 `[M2MWithMany]` properties and both `[M2M]` and `[SpiderlyEntity]` markers. `[M2M]` flags the class as a junction; `[SpiderlyEntity]` enrolls it in the generator pipeline — missing it breaks the parent entity's generated service. Always add `[CascadeDelete]` on both navigations — otherwise deleting a parent is blocked. Parent collections:

```csharp
public class Role : BusinessObject<long>
{
    public virtual List<Permission> Permissions { get; } = new();
}
```

### Complex Many-to-Many (junction with extra fields)

Keep `[M2M]` and `[SpiderlyEntity]` on the junction and add additional properties beside the two `[M2MWithMany]` navigations. Use `[ComplexManyToManyList]` on the parent collection for editable junction UI, or `[ComplexManyToManyReadonlyTable]` for read-only display.

### Ordered One-to-Many

```csharp
public class Course : BusinessObject<long>
{
    [UIOrderedOneToMany]
    public virtual List<CourseItem> CourseItems { get; } = new();
}

public class CourseItem : BusinessObject<long>
{
    [UIDoNotGenerate] [Required]
    public int OrderNumber { get; set; }

    [WithMany(nameof(Course.CourseItems))]
    public virtual Course Course { get; set; }
}
```

Child **must** have `[UIDoNotGenerate] [Required] public int OrderNumber { get; set; }`.

## UI Control Auto-Mapping

| C# Type         | Default Control | Override With                                                            |
| --------------- | --------------- | ------------------------------------------------------------------------ |
| `string`        | TextBox         | `[UIControlType(nameof(UIControlTypeCodes.TextArea))]`, `Editor`, `File` |
| `int`, `long`   | Number          | —                                                                        |
| `decimal`       | Decimal         | —                                                                        |
| `bool`          | CheckBox        | —                                                                        |
| `DateTime`      | Calendar        | —                                                                        |
| Navigation prop | Autocomplete    | `[UIControlType(nameof(UIControlTypeCodes.Dropdown))]`                   |

Other `UIControlTypeCodes`: `ColorPicker`, `MultiAutocomplete`, `MultiSelect`, `Password`, `TextBlock`, `Table`.

Width: `[UIControlWidth("col-8 md:col-4")]` (default). TextArea/Editor default to `"col-8"`.

## Key Attributes Checklist

| Attribute                               | Level          | Purpose                                                                                              |
| --------------------------------------- | -------------- | ---------------------------------------------------------------------------------------------------- |
| `[DisplayName]`                         | Property       | Marks the property shown in dropdowns/autocompletes                                                  |
| `[DisplayName("Entity.Prop")]`          | Class          | Display name from a related entity (e.g., `"User.Email"` — use plain string, **not** `nameof()`)     |
| `[UIDoNotGenerate]`                     | Property/Class | Exclude from generated UI (template, frontend validators). Backend DTO + validation still generated. |
| `[UIControlWidth("col-X")]`             | Property       | Set form field width                                                                                 |
| `[UIOrderedOneToMany]`                  | Property       | Enable drag-and-drop ordered child list                                                              |
| `[UIPropertyBlockOrder("N")]`           | Property       | Control field display order                                                                          |
| `[UIPanel("Name")]`                     | Property       | Group fields into named panels                                                                       |
| `[BlobName]`                            | Property       | Mark as file reference (pair with `[StringLength]`)                                                  |
| `[S3PublicUrl]`                         | Property       | File stored in S3 with public CDN URL                                                                |
| `[S3Url]`                               | Property       | File stored in S3 with private (authenticated) access                                                |
| `[CloudinaryPublicId]`                  | Property       | File stored in Cloudinary                                                                            |
| `[AcceptedFileTypes("mime/type", ...)]` | Property       | **Required on every `[BlobName]` property** — whitelist upload MIME types. Build error `SPIDERLY014` if missing. No default.  |
| `[MaxFileSize(N)]`                      | Property       | Max upload size in bytes (default: 20MB)                                                             |
| `[ImageWidth(N)]` / `[ImageHeight(N)]`  | Property       | Validate exact image dimensions                                                                      |
| `[DoNotAuthorize]`                      | Class          | Skip authorization checks for this entity                                                            |
| `[Controller("Name")]`                  | Class          | Group entity under a custom controller                                                               |
| `[ExcludeFromDTO]`                      | Property       | Exclude from generated DTO                                                                           |
| `[IncludeInDTO]`                        | Property       | Force-include in DTO (e.g., collections)                                                             |
| `[ExcludeServiceMethodsFromGeneration]` | Property       | Skip generated service methods (implement custom logic)                                              |
| `[GreaterThanOrEqualTo(N)]`             | Property       | Numeric minimum validation                                                                           |
| `[Email]`                               | Property       | Email format validation                                                                              |
| `[ProjectToDTO(".Map(...)")]`           | Class          | Custom Mapster projection                                                                            |
| `[GenerateCommaSeparatedDisplayName]`   | Property       | Add comma-separated display names to DTO                                                             |
| `[ComplexManyToManyList]`               | Property       | Editable list UI for complex M2M junction (small sets only)                                          |
| `[ComplexManyToManyReadonlyTable]`      | Property       | Read-only table for complex M2M junction                                                             |
| `[SimpleManyToManyTableLazyLoad]`       | Property       | Lazy-load M2M with table columns                                                                     |
| `[UITableColumn(nameof(DTO.Field))]`    | Property       | Define columns for M2M table (use with above)                                                        |

## Complete Entity Example

```csharp
public class Product : BusinessObject<long>
{
    [DisplayName]
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; }

    [UIControlType(nameof(UIControlTypeCodes.Editor))]
    [StringLength(10000, MinimumLength = 1)]
    public string Description { get; set; }

    [Required]
    [GreaterThanOrEqualTo(0)]
    public decimal Price { get; set; }

    [Required]
    [WithMany(nameof(Category.Products))]
    public virtual Category Category { get; set; }

    [WithMany(nameof(Brand.Products))]
    public virtual Brand Brand { get; set; }

    [BlobName]
    [S3PublicUrl]
    [AcceptedFileTypes("image/*")]
    [MaxFileSize(2_000_000)]
    [StringLength(1000, MinimumLength = 1)]
    public string MainImage { get; set; }

    public virtual List<Tag> Tags { get; } = new();

    [UIOrderedOneToMany]
    public virtual List<ProductVariant> ProductVariants { get; } = new();
}
```
