---
name: entity-design
description: Design Spiderly entities with correct attributes, relationships, and UI mappings. Use when creating or modifying entity classes, choosing entity attributes, setting up relationships (M2O, 1-1 via [WithOne], M2M, ordered O2M), configuring file uploads on entities, or asking about UI control mapping.
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

`Version` is an optimistic-concurrency token you get for free on every `BusinessObject<T>` — `ReadonlyObject<T>` has none. It's a `[ConcurrencyCheck]` column, auto-set to `1` on insert and incremented on every update inside `SaveChanges` (you never touch it), and it round-trips to the client on the DTO. On update the generated `Save{Entity}` reloads the row via `GetInstanceAsync(id, dto.Version)`, which throws a localized `ConcurrencyException` (a `BusinessException`) when the incoming version is stale — so two users editing the same record can't silently overwrite each other. No per-entity wiring required. (The guard is on update; deletes go through `ExecuteDeleteAsync` and are not version-checked.)

`T` = `long` (default), `int`, or `byte`. **Anything else is rejected at compile time by [SPIDERLY018](/docs/build-diagnostics#spiderly018)** — including `Guid`, `decimal`, `short`, `DateTime`, etc. Ordinary `Guid` scalar properties on entities are fully supported; only the PK type argument is restricted. For public, non-enumerable identifiers (UUID-style URLs) keep the numeric `Id` and add a separate `Guid PublicId` property.

## Operational tables

Tables that exist as operational state (outbox, audit log, sync cursors, dispatcher queues) are still `[SpiderlyEntity]` — do **not** reach for `[UIDoNotGenerate]` at the class level and a parallel custom Angular page; you lose the generated table, mappers, validators, and DTOs for no gain. Restrict mutations by not granting `Insert{Entity}` / `Update{Entity}` / `Delete{Entity}` permissions to any role in your seed setup; default-filter the admin table to "interesting" rows (e.g. pending, failed) via a paginated-list override (see `spiderly:filtering-patterns`); expose semantic row actions like Retry/Dismiss via a custom controller alongside the generated one (see `spiderly:custom-endpoints`).

## Property Rules

- Navigation properties **must** be `virtual`: `public virtual Brand Brand { get; set; }`
- Collections use `List<T>` (not `IList<T>`), initialized inline: `public virtual List<Comment> Comments { get; } = new();`
- Explicit FK properties (`BrandId` alongside `Brand`) are **supported and recommended for hot paths** — see *Explicit FK properties* below
- `[StringLength(X)]` without `MinimumLength` = **max-length** validation (minimum defaults to 0, standard .NET semantics). Use `[StringLength(X, MinimumLength = Y)]` for a range; `[StringLength(X, MinimumLength = X)]` (min == max) for exact length.
- On properties that aren't effectively required (no `[Required]`, not an `[M2MWithMany]` junction), all validation rules wrap in `.Unless(string.IsNullOrEmpty(x))` on strings — or `== null` on other types — so the validator skips null/empty entirely. Consequence: `MinimumLength = 1` is a no-op on non-required strings; use `MinimumLength ≥ 2` or add `[Required]` to actually reject empty values.
- `[Required]` on navigation properties makes the relationship required (non-nullable FK)
- The `[Index]` attribute lives in `Microsoft.EntityFrameworkCore` and is **not** in Spiderly's default using-block. Add `using Microsoft.EntityFrameworkCore;` to the entity file or you get the misleading `'Index' is not an attribute class` error.

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

The target entity **must** have a back-collection matching the `[WithMany(nameof(...))]` name. Forgetting `[WithMany]`, naming a target collection that doesn't exist, or declaring the back-collection with the wrong element type all surface at `dotnet build` time as `SPIDERLY015` / `SPIDERLY016` / `SPIDERLY017` respectively — no runtime explosion in `DbContext.OnModelCreating`. Two options:

1. Add the back-collection on the target (`public virtual List<Comment> Comments { get; } = new();` on `Post`) — preferred when both directions are useful.
2. **Drop the nav property** and keep only the explicit FK scalar (`public long PostId { get; set; }`). Then configure the relationship + delete behavior manually in `OnModelCreating`:
   ```csharp
   modelBuilder.Entity<Comment>()
       .HasOne<Post>().WithMany().HasForeignKey(c => c.PostId)
       .OnDelete(DeleteBehavior.Cascade);
   ```
   Use this when an FK exists only as a pointer (e.g. `LastReadMessage`, `ParentMessage`) and a back-collection on the target would be noise.

**Delete behavior:**

| Attribute         | FK nullable? | On parent delete                                          |
| ----------------- | ------------ | --------------------------------------------------------- |
| `[CascadeDelete]` | No           | Children deleted by generated service code               |
| `[SetNull]`       | Yes          | DB sets FK to null (`OnDelete(SetNull)`)                 |
| Neither           | No           | DB throws FK violation at runtime (`OnDelete(NoAction)`) |

#### How `[CascadeDelete]` actually works

`[CascadeDelete]` is **application-layer**, not EF Core `OnDelete(Cascade)`. The source generator scans many-to-one navigations marked with it and emits explicit `ExecuteDeleteAsync()` calls inside the generated `Delete{Entity}` / `Delete{Entity}List` methods, recursing through dependents in child→parent order inside a single transaction.

Although the cascade is untracked bulk `ExecuteDeleteAsync`, the generated delete still **flushes the change tracker right after `OnBefore{Entity}Delete`** — so a delete hook can stage tracked writes (e.g. `IOutbox.Enqueue`) and they persist atomically with the delete, no manual `SaveChangesAsync`. See the `backend-hooks` skill.

**Why app-layer instead of `OnDelete(Cascade)`.** SQL Server refuses cascading FKs whenever the schema has any potential cycle or multiple cascade paths. App-layer cascade sidesteps that entirely and gives transaction control, `OnBefore{Entity}Delete` hooks, authorization checks, and audit visibility — so it stays the idiom even on Postgres.

**Placement vs. semantics gotcha.** The attribute sits on the **child's** FK navigation but fires on **parent** deletion. `[CascadeDelete] public virtual Post Post` on `Comment` means *"when the `Post` is deleted, this `Comment` is deleted with it"* — not the other direction.

**No DB safety net.** Because the relationship is `NoAction`, forgetting `[CascadeDelete]` on a required FK causes a runtime FK violation at parent deletion. Either add the attribute, or delete the dependent rows explicitly with `ExecuteDeleteAsync` before the parent delete.

**Collection-side placement is a no-op.** The generator only scans many-to-one navigations on the child side; `[CascadeDelete]` on a parent's `List<Child>` collection does nothing — it must go on `Child.ParentNav`.

**Intentional omission** requires an inline `// no cascade because …` comment on the FK and a manual `ExecuteDeleteAsync` in the entity's `OnBefore{Entity}Delete` hook. Use this only when a dependent must outlive its parent (e.g. an audit or loyalty row that should survive the order it references), so future readers don't flag it as a bug.

### One-to-One

Use `[WithOne]` for a true 1-1 between **two independent aggregate roots** (each is queried and edited on its own). For a value object that only ever lives inside its parent (an address, a money amount), don't use 1-1 — either inline the fields on the parent or model an EF owned type; a separate `[SpiderlyEntity]` is overkill.

`[WithOne(nameof(Principal.InverseNav))]` goes on the **dependent** (foreign-key-holding) side's single-valued reference nav. Its *presence* designates that side as the dependent; the other side is the principal and is a plain nav with no attribute.

```csharp
public class Conversation : BusinessObject<long>   // dependent — owns the FK
{
    public long? OwningTaskItemId { get; set; }     // explicit FK; nullable => optional 1-1
    [WithOne(nameof(TaskItem.Conversation))]
    [CascadeDelete]                                  // delete the TaskItem => delete its Conversation
    public virtual TaskItem OwningTaskItem { get; set; }
}

public class TaskItem : BusinessObject<long>        // principal
{
    public virtual Conversation Conversation { get; set; }   // inverse nav, no attribute
}
```

This generates: single-valued navs both ways, an automatic **unique index** on the FK, the correct EF `HasOne().WithOne().HasForeignKey()` mapping, dependent-side DTO flattening (`{Nav}Id` + `{Nav}DisplayName`, same as M2O), and an autocomplete control + endpoint on the dependent's page.

**Required vs optional — dependent-FK nullability only.**

| Declaration | Meaning | DB |
| --- | --- | --- |
| `[Required]` on the `[WithOne]` nav (non-nullable FK) | the dependent must have a principal | plain unique index |
| no `[Required]` (nullable FK) | optional; the dependent may have no principal | unique index that **allows many NULLs** (Postgres `NULLS DISTINCT` / SQL Server auto `IS NOT NULL` filter — handled by provider conventions, no manual work) |

The schema **cannot** enforce "every principal has a dependent" — that direction is always 0..1. `[Required]` on the **principal**-side nav is a hard build error (**SPIDERLY021**); if you truly need that invariant, create the dependent in the principal's `OnAfter{Entity}Insert` hook.

**Other rules & diagnostics:**

- **Explicit FK recommended for code-managed 1-1s.** Shadow FK is allowed (symmetric with `[WithMany]`), but if you create the dependent in hand-written code (`new Conversation { OwningTaskItemId = taskId }`) you need the explicit scalar — there's no scalar to set on a shadow FK.
- **Cascade is app-layer**, exactly like M2O: `[CascadeDelete]` (deleted with the principal, walker-ordered), `[SetNull]` (nullable FK), or neither. No DB-level `ON DELETE CASCADE` is emitted.
- **Unidirectional**: parameterless `[WithOne]` when the principal has no back-nav.
- **Self-referential 1-1 is unsupported** → **SPIDERLY022**. Both-sides `[WithOne]` → **SPIDERLY019**; a `[WithOne]` inverse-nav name that doesn't exist → **SPIDERLY020**.
- **Duplicate guard for free**: a second dependent pointing at an already-taken principal violates the unique index and surfaces as a clean localized 409 (the generic constraint handler), not a 500.
- **The principal inverse renders nothing by default** — it's excluded from the principal's DTO and UI automatically (the FK lives on the dependent). For a fully code-managed 1-1 (the dependent is created/edited in code, never picked in the admin), put `[UIDoNotGenerate]` on the dependent's `[WithOne]` nav to suppress the autocomplete too.

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

Junction entity must have exactly 2 `[M2MWithMany]` properties and both `[M2M]` and `[SpiderlyEntity]` markers. `[M2M]` flags the class as a junction; `[SpiderlyEntity]` enrolls it in the generator pipeline — missing it breaks the parent entity's generated service. Always add `[CascadeDelete]` on both navigations — otherwise the parent delete throws an FK violation at runtime (see *How `[CascadeDelete]` actually works* under Many-to-One). Parent collections:

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
| `string`        | TextBox         | `[UIControlType(nameof(UIControlTypeCodes.TextArea))]`, `Editor`, `Markdown`, `File` |
| `int`, `long`   | Number          | —                                                                        |
| `decimal`       | Decimal         | —                                                                        |
| `bool`          | CheckBox        | —                                                                        |
| `DateTime`      | Calendar        | —                                                                        |
| `[SpiderlyEnum]` enum | Dropdown (translated, auto-populated) | model the value as `int`/`byte`/`long` instead if you do **not** want a dropdown |
| Navigation prop | Autocomplete    | `[UIControlType(nameof(UIControlTypeCodes.Dropdown))]`                   |

Other `UIControlTypeCodes`: `ColorPicker`, `MultiAutocomplete`, `MultiSelect`, `Password`, `Table`.

Width: `[UIControlWidth("col-8 md:col-4")]` (default). TextArea/Editor/Markdown default to `"col-8"`.

`Editor` stores HTML (Quill WYSIWYG); `Markdown` stores raw Markdown (textarea + live preview). Both support inline image upload (paste, in Markdown's case) when combined with `[S3PublicStorage]`.

### Enum properties → translated dropdown

A property typed as a C# `enum` marked `[SpiderlyEnum]` auto-renders as a **dropdown**, populated client-side from the generated TS enum (no API round-trip) and labeled through Transloco.

```csharp
[SpiderlyEnum]
public enum AnnouncementSeverityCodes { Info = 1, Warning = 2, Critical = 3 }

public class Announcement : BusinessObject<long>
{
    public AnnouncementSeverityCodes Severity { get; set; } // -> translated dropdown
}
```

Rule of thumb: **a fixed set the user picks from → `[SpiderlyEnum]` enum** (you get a translated dropdown for free). **A coded value never shown as a choice → a raw numeric** (`int`/`byte`/`long`), which renders as a number field, or hide it with `[UIDoNotGenerate]`.

- **Translation key = the enum member name** (`Info`, `Warning`, `Critical`). The generator emits a `get{Enum}NamebookList(translocoService)` builder in `enums.generated.ts`; run `npm run i18n:extract` and fill each locale's value (e.g. `"Critical": "Kritično"`). A missing value renders the raw key, so don't skip this.
- **Break a label collision by renaming the member.** Two enums that both have `Pending` share one `Pending` key; if they need different wording, rename one member (e.g. `PendingReview`). The key follows the member name — no attribute required.
- **List-table enum column filter** reuses the same builder, wrapped in the `spiderly` helper `getPrimengNamebookOptions` (`Namebook[]` → the table's `{ label, code }[]`):
  ```ts
  { name: t('Severity'), filterType: 'multiselect', field: 'severity',
    dropdownOrMultiselectValues:
      getPrimengNamebookOptions(getAnnouncementSeverityCodesNamebookList(this.translocoService)) }
  ```

> Class-based enums (a `static class` of string constants, like `PermissionCodes`) are **not** usable as a dropdown property type — you can't type a property as a static class, so the field would be a bare `string` the generator can't recognize. Use a real `enum` for dropdown fields.

## Key Attributes Checklist

The complete list of every Spiderly attribute and its valid target is generated from the attribute classes themselves: see [references/attributes.generated.md](references/attributes.generated.md). The curated highlights below are the ones you'll reach for most.

| Attribute                               | Level          | Purpose                                                                                              |
| --------------------------------------- | -------------- | ---------------------------------------------------------------------------------------------------- |
| `[DisplayName]`                         | Property       | Marks the property shown in dropdowns/autocompletes                                                  |
| `[DisplayName("Entity.Prop")]`          | Class          | Display name from a related entity (e.g., `"User.Email"` — use plain string, **not** `nameof()`)     |
| `[UIDoNotGenerate]`                     | Property/Class | Exclude from generated UI (template, frontend validators). Backend DTO + validation still generated. |
| `[UIControlWidth("col-X")]`             | Property       | Set form field width                                                                                 |
| `[UIOrderedOneToMany]`                  | Property       | Enable drag-and-drop ordered child list                                                              |
| `[UIPropertyBlockOrder("N")]`           | Property       | Control field display order                                                                          |
| `[UISection("Name")]`                   | Property       | Group fields into named sections (cards) on the details page                                         |
| `[DiskStorage]`                         | Property       | File stored on local filesystem (dev only). Marks the property as a blob.                            |
| `[S3PublicStorage]`                     | Property       | File stored in S3 with public CDN URL. Marks the property as a blob.                                 |
| `[S3PrivateStorage]`                    | Property       | File stored in S3 with private (signed-URL) access. Marks the property as a blob.                    |
| `[AcceptedFileTypes("mime/type", ...)]` | Property       | **Required on every blob property** — whitelist upload MIME types. Build error `SPIDERLY014` if missing. No default.          |
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

    [S3PublicStorage]
    [AcceptedFileTypes("image/*")]
    [MaxFileSize(2_000_000)]
    [StringLength(1000, MinimumLength = 1)]
    public string MainImage { get; set; }

    public virtual List<Tag> Tags { get; } = new();

    [UIOrderedOneToMany]
    public virtual List<ProductVariant> ProductVariants { get; } = new();
}
```

## Diagnosing build failures

When the build dumps **hundreds of CS0246 errors about missing `*DTO` types**, scroll up and find the **SPIDERLY-prefixed error first**. Violating any contract (unsupported PK type, missing `[WithMany]` target, unsupported scalar, broken `[DisplayName]` path) makes `MapperGenerator` bail, which in turn deletes every entity's generated DTO — and *that* is what produces the CS0246 flood. The downstream errors are noise. Full diagnostic code reference: https://www.spiderly.dev/docs/build-diagnostics
