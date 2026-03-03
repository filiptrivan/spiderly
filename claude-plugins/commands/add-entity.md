---
name: add-entity
description: Scaffold a new Spiderly entity end-to-end (entity class, Angular pages, routes, menu, migration)
---

# Add Entity Workflow

Follow these steps in order. Do not skip ahead.

## Step 1 — Gather Requirements

Ask the user:

1. **Entity name** (PascalCase, singular — e.g., `Product`, `BlogPost`)
2. **Properties** — for each: name, C# type, required?, string max length, any special attributes
3. **Relationships** — many-to-one (required/optional?), many-to-many, ordered one-to-many
4. **Base class** — `BusinessObject<long>` (CRUD, default) or `ReadonlyObject<long>` (lookup table)
5. **File uploads?** — which properties, storage type (S3 public, Cloudinary, or default file manager)
6. **List page format** — data table (default) or data view (card grid)?

Do NOT proceed until the user confirms the entity design.

## Step 2 — Run the CLI scaffold

**Note:** This command prompts interactively for the entity name. Tell the user to run it themselves in a terminal, then continue with Step 3 once it completes.

```bash
spiderly add-new-entity
```

If the user chose data view format:

```bash
spiderly add-new-entity --data-view
```

This creates:
- `Backend/{App}.Business/Entities/{Entity}.cs` (empty entity with `[DoNotAuthorize]`)
- `Frontend/src/app/pages/{kebab-name}/{kebab-name}-list.component.ts` + `.html`
- `Frontend/src/app/pages/{kebab-name}/{kebab-name}-details.component.ts` + `.html`
- Inserts routes into `Frontend/src/app/app.routes.ts`
- Inserts menu item into `Frontend/src/app/business/layout/layout.component.ts`

## Step 3 — Write the entity class

Use the `entity-design` skill to write the entity with correct attributes. Replace the CLI-generated empty entity.

Checklist:
- [ ] `[DisplayName]` on the name/title property
- [ ] `[Required]` on mandatory fields
- [ ] `[StringLength(max, MinimumLength = min)]` on ALL strings (never omit — avoids NVARCHAR(MAX))
- [ ] `virtual` on navigation properties
- [ ] `List<T>` with `{ get; } = new()` on collections
- [ ] `[WithMany]` on M2O child side, `[CascadeDelete]` or `[SetNull]` for delete behavior
- [ ] `[UIOrderedOneToMany]` on parent collection + `OrderNumber` on child (if ordered)
- [ ] `[M2M]` on junction entities with exactly 2 `[M2MWithMany]` properties
- [ ] File properties: `[BlobName]` + storage attribute + `[AcceptedFileTypes]` + `[MaxFileSize]` + `[StringLength]`
- [ ] Remove `[DoNotAuthorize]` if the entity needs authorization

Also add the collection property to related parent entities (e.g., `public virtual List<Product> Products { get; } = new();` on `Category`).

## Step 4 — Build the backend

```bash
dotnet build
```

Run from the `Backend/` directory. This triggers Spiderly source generators which produce:
- DTOs, services, controllers, validators, mappers, TypeScript classes

Fix any build errors before continuing.

## Step 5 — Create and apply the migration

```bash
spiderly add-migration Add{EntityName}Table
spiderly update-database
```

Use PascalCase for migration names. If the migration looks wrong, `spiderly remove-migration` and fix the entity first.

## Step 6 — Customize Angular components

### List component (`{kebab-name}-list.component.ts`)

Update the `cols` array in `ngOnInit()` with actual entity columns:

```typescript
this.cols = [
    { name: this.translocoService.translate('Name'), filterType: 'text', field: 'name' },
    { name: this.translocoService.translate('Price'), filterType: 'numeric', field: 'price' },
    { actions: [
        { name: this.translocoService.translate('Details'), field: 'Details' },
        { name: this.translocoService.translate('Delete'), field: 'Delete' },
    ]},
];
```

### Translation keys

Add keys to both `Frontend/src/assets/i18n/en.json` and `sr-Latn-RS.json`:

```json
{
  "{EntityName}": "...",
  "{EntityName}List": "...",
  // property name keys as needed
}
```

### Menu icon (optional)

In `layout.component.ts`, change `'pi pi-fw pi-list'` to an appropriate PrimeNG icon.

## Step 7 — Build frontend

```bash
ng build
```

From `Frontend/`. Fix any build errors.

## Step 8 — Add backend hooks (if needed)

If the entity requires custom business logic (validation, computed fields, side effects), override hooks in `BusinessService`:

```csharp
protected override async Task OnBeforeSave{Entity}AndReturnMainUIFormDTO(
    {Entity}SaveBodyDTO saveBodyDTO) { }

protected override async Task OnAfterSave{Entity}AndReturnMainUIFormDTO(
    {Entity}SaveBodyDTO saveBodyDTO,
    {Entity}MainUIFormDTO mainUIFormDTO) { }
```

See the `backend-hooks` skill for the full hook reference.

## Step 9 — Verify

1. Start the app (F5 or `dotnet run`)
2. Navigate to the new entity's list page
3. Create a record, verify all fields save correctly
4. Edit the record, verify update works
5. Delete the record, verify cascade/set-null behavior
6. Check file uploads if applicable
