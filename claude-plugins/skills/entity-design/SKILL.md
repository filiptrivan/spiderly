---
name: entity-design
description: Design Spiderly entities with correct attributes, relationships, and UI mappings. Use when creating or modifying entity classes, choosing entity attributes, setting up relationships (M2O, M2M, ordered O2M), configuring file uploads on entities, or asking about UI control mapping.
---

# Entity Design

## Base Classes

| Base Class | Use When | Generated |
|---|---|---|
| `BusinessObject<T>` | Full CRUD entity | Id, Version, CreatedAt, ModifiedAt + CRUD UI/API |
| `ReadonlyObject<T>` | Lookup/reference table | Id only, read-only operations |

`T` = `long` (default), `int`, or `byte`.

## Property Rules

- Navigation properties **must** be `virtual`: `public virtual Brand Brand { get; set; }`
- Collections use `List<T>` (not `IList<T>`), initialized inline: `public virtual List<Comment> Comments { get; } = new();`
- **Never** declare explicit FK properties (e.g., `BrandId`) — Spiderly generates them from navigation properties
- `[StringLength(X)]` **without** `MinimumLength` = **exact length** validation. Always use `[StringLength(X, MinimumLength = Y)]` for range
- `[Required]` on navigation properties makes the relationship required (non-nullable FK)

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

| Attribute | FK nullable? | On parent delete |
|---|---|---|
| `[CascadeDelete]` | No | Delete all children |
| `[SetNull]` | Yes | Set FK to null |
| Neither | No | Block delete (EF default) |

### Simple Many-to-Many

```csharp
[M2M]
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

Junction entity must have exactly 2 `[M2MWithMany]` properties. Always add `[CascadeDelete]` on both — otherwise deleting a parent is blocked. Parent collections:

```csharp
public class Role : BusinessObject<long>
{
    public virtual List<Permission> Permissions { get; } = new();
}
```

### Complex Many-to-Many (junction with extra fields)

Add additional properties to the junction entity (without `[M2M]`). Use `[ComplexManyToManyList]` on the parent collection for editable junction UI, or `[ComplexManyToManyReadonlyTable]` for read-only display.

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

| C# Type | Default Control | Override With |
|---|---|---|
| `string` | TextBox | `[UIControlType(nameof(UIControlTypeCodes.TextArea))]`, `Editor`, `File` |
| `int`, `long` | Number | — |
| `decimal` | Decimal | — |
| `bool` | CheckBox | — |
| `DateTime` | Calendar | — |
| Navigation prop | Autocomplete | `[UIControlType(nameof(UIControlTypeCodes.Dropdown))]` |

Other `UIControlTypeCodes`: `ColorPicker`, `MultiAutocomplete`, `MultiSelect`, `Password`, `TextBlock`, `Table`.

Width: `[UIControlWidth("col-8 md:col-4")]` (default). TextArea/Editor default to `"col-8"`.

## Key Attributes Checklist

| Attribute | Level | Purpose |
|---|---|---|
| `[DisplayName]` | Property | Marks the property shown in dropdowns/autocompletes |
| `[DisplayName("Entity.Prop")]` | Class | Display name from a related entity (e.g., `"User.Email"` — use plain string, **not** `nameof()`) |
| `[UIDoNotGenerate]` | Property/Class | Exclude from generated UI |
| `[UIControlWidth("col-X")]` | Property | Set form field width |
| `[UIOrderedOneToMany]` | Property | Enable drag-and-drop ordered child list |
| `[UIPropertyBlockOrder("N")]` | Property | Control field display order |
| `[UIPanel("Name")]` | Property | Group fields into named panels |
| `[BlobName]` | Property | Mark as file reference (pair with `[StringLength]`) |
| `[S3PublicUrl]` | Property | File stored in S3 with public CDN URL |
| `[S3Url]` | Property | File stored in S3 with private (authenticated) access |
| `[CloudinaryPublicId]` | Property | File stored in Cloudinary |
| `[AcceptedFileTypes("image/*")]` | Property | Restrict upload MIME types |
| `[MaxFileSize(N)]` | Property | Max upload size in bytes (default: 20MB) |
| `[ImageWidth(N)]` / `[ImageHeight(N)]` | Property | Validate exact image dimensions |
| `[DoNotAuthorize]` | Class | Skip authorization checks for this entity |
| `[Controller("Name")]` | Class | Group entity under a custom controller |
| `[ExcludeFromDTO]` | Property | Exclude from generated DTO |
| `[IncludeInDTO]` | Property | Force-include in DTO (e.g., collections) |
| `[ExcludeServiceMethodsFromGeneration]` | Property | Skip generated service methods (implement custom logic) |
| `[GreaterThanOrEqualTo(N)]` | Property | Numeric minimum validation |
| `[EmailAddress]` | Property | Email format validation |
| `[ProjectToDTO(".Map(...)")]` | Class | Custom Mapster projection |
| `[GenerateCommaSeparatedDisplayName]` | Property | Add comma-separated display names to DTO |
| `[ComplexManyToManyList]` | Property | Editable list UI for complex M2M junction (small sets only) |
| `[ComplexManyToManyReadonlyTable]` | Property | Read-only table for complex M2M junction |
| `[SimpleManyToManyTableLazyLoad]` | Property | Lazy-load M2M with table columns |
| `[UITableColumn(nameof(DTO.Field))]` | Property | Define columns for M2M table (use with above) |

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
