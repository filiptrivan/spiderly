---
name: backend-localization
description: How Spiderly localizes backend (.NET) strings — error messages, Excel export names, any server-side text. Spiderly uses a JSON file localizer (flat {culture}.json files), NOT .resx. Use whenever you add or translate a backend string, hit a raw translation key leaking to the user, wonder where backend translations live or why there are no .resx files, localize a BusinessException message, register a custom (e.g. DB-backed) IStringLocalizer, or set up UseTranslations / UseCulture. For Angular admin UI strings (Transloco, assets/i18n/*.json), use the frontend-localization skill instead.
---

# Backend Localization

Spiderly localizes server-side strings through the standard .NET `IStringLocalizer` abstraction, but the **implementation is JSON-file based — there is no `.resx` anywhere in the framework**. Do not create `.resx`/`.resources` files or a `ResourceManager`; they will not be read. The choice is deliberate: flat JSON files are diff-friendly, trivial to edit by hand or by an agent, and need no designer/codegen step.

Because everything goes through `IStringLocalizer`, call sites look identical to a resx-backed app — the JSON nature only matters when you *define* or *find* a translation.

## The three localizer modes

`IStringLocalizer` is registered as a **singleton** in `AddSpiderly` based on the builder config (`StartupExtensions.cs`):

| Builder call | Registered implementation | Behavior |
|---|---|---|
| `spiderly.UseTranslations()` | `JsonStringLocalizer` | Loads every `Translations/{culture}.json` file. |
| `spiderly.UseTranslations<TLocalizer>()` | your `TLocalizer` | Custom source (e.g. database-backed). DI resolves its constructor. |
| *(neither called)* | `PassthroughStringLocalizer` | No-op: returns the key as its own value, so the app runs untranslated. |

`PassthroughStringLocalizer` being the default is why a brand-new app shows raw keys until you opt in with `UseTranslations()`.

## Enabling JSON translations

```csharp
// Startup.cs — inside AddSpiderly(spiderly => { ... })
spiderly.UsePostgreSQL();
spiderly.UseCulture("sr-Latn-RS");   // default + supported cultures
spiderly.UseTranslations();          // register the JSON localizer
```

Translation files live in a `Translations/` directory and **must be copied to the build output** — `JsonStringLocalizer` reads from `AppContext.BaseDirectory/Translations`, not the source tree. Add this to the `.csproj` that holds them:

```xml
<ItemGroup>
  <None Update="Translations\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## File format and naming

Each file is a **flat** key→value JSON object (no nesting) named exactly after the culture:

```jsonc
// Translations/sr-Latn-RS.json
{
  "ConcurrencyException": "Podaci su u međuvremenu izmenjeni. Osvežite stranicu.",
  "EntityDoesNotExistInDatabase": "Traženi podatak ne postoji.",
  "WelcomeUser": "Dobrodošli, {0}!"
}
```

Two hard naming rules — both fail silently if broken:

1. **The filename must equal `CultureInfo.CurrentCulture.Name`** that the request runs under (e.g. `sr-Latn-RS.json`, `en.json`, `bs-Latn-BA.json`). The localizer keys off the running culture's exact name; a mismatch (`sr.json` when requests run as `sr-Latn-RS`) means *no file matches* and every key falls through to its raw self.
2. **The culture must be registered via `UseCulture(default, ...additional)`.** `RequestLocalization` only switches `CurrentCulture` to cultures in that supported list; anything else falls back to the default culture, so its JSON file is never consulted.

Format placeholders use `string.Format`: `_localizer["WelcomeUser", user.Name]` → `"Dobrodošli, Filip!"`.

## Using the localizer in code

### Inside an entity service (`{Entity}Service : {Entity}ServiceGenerated`)

The base `ServiceBase` exposes a `protected readonly IStringLocalizer _localizer`, so use it directly. This is the canonical way to produce a **localized error message**:

```csharp
public class ProductService : ProductServiceGenerated
{
    public ProductService(EntityServiceDependencies deps) : base(deps) { }

    protected override async Task OnBeforeSaveProductAndReturnSaveBodyDTO(ProductSaveBodyDTO dto)
    {
        if (dto.Price < 0)
            throw new BusinessException(_localizer["PriceCannotBeNegative"]);
    }
}
```

`_deps.Localizer` is the same instance (the dependency bundle) — prefer `_localizer` for brevity inside a service; pass `_deps.Localizer` when a helper needs it (e.g. `Helper.ValidateFileSize(file.Length, max, _deps.Localizer)`).

### Inside a custom (non-entity) service or controller

Inject `IStringLocalizer` through the constructor like any other dependency:

```csharp
public class WarrantyRegistrationService
{
    private readonly IStringLocalizer _localizer;

    public WarrantyRegistrationService(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    public void Validate(IFormFile file)
    {
        Helper.ValidateFileSize(file.Length, MaxReceiptFileSize, _localizer);
    }
}
```

Custom controllers that extend a generated base already receive `IStringLocalizer localizer` and pass it to `base(...)` — see the custom-endpoints skill.

### Indexer vs. the `Translate` extension

```csharp
_localizer["Key"]                  // LocalizedString; implicitly converts to string. Renders "Key" if missing.
_localizer["Key", arg0, arg1]      // string.Format with the translation value as the format string.
_localizer.Translate("Key")        // plain string, explicitly returns the key itself on a miss.
_localizer.GetExcelTranslation("ProductExcelExportName", "ProductList")
                                   // Excel-specific key first, then the plural/list key, then the raw key.
```

`Translate` and `GetExcelTranslation` are in `Spiderly.Shared.Localization.StringLocalizerExtensions` (`using Spiderly.Shared.Localization;`).

## Gotchas

- **Translations are loaded once, at app start.** The JSON localizer is a singleton that reads all files in its constructor. **Editing a `*.json` file has no effect until you restart the backend** — there is no file watcher. (A custom `UseTranslations<T>()` localizer can reload per request if you build it that way.)
- **A missing key fails open, not loud.** Both the JSON localizer (unknown key) and the passthrough default return the *key string itself* with `ResourceNotFound = true`. So a typo'd or untranslated key silently leaks the raw identifier (e.g. `PriceCannotBeNegative`) to the end user instead of throwing. Keep keys in sync across every `{culture}.json`, and treat a raw-key-in-the-UI sighting as a missing-translation bug, not a code bug.
- **Don't reach for `.resx`.** Adding a resource file or `ResourceManager` won't integrate — the abstraction only resolves through the registered `IStringLocalizer`.

## Custom localizer (e.g. database-backed)

When translations must be editable at runtime (admin-managed) rather than shipped as files, implement `IStringLocalizer` and register it. DI resolves its constructor, so it can depend on your `DbContext` or a cache:

```csharp
public class DbStringLocalizer : IStringLocalizer
{
    private readonly IApplicationDbContext _context;
    public DbStringLocalizer(IApplicationDbContext context) => _context = context;

    public LocalizedString this[string name]
    {
        get
        {
            string culture = CultureInfo.CurrentCulture.Name;
            string value = _context.DbSet<Translation>()
                .Where(t => t.Culture == culture && t.Key == name)
                .Select(t => t.Value)
                .FirstOrDefault();
            return value != null
                ? new LocalizedString(name, value)
                : new LocalizedString(name, name, resourceNotFound: true);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            LocalizedString s = this[name];
            return new LocalizedString(name, string.Format(s.Value, arguments), s.ResourceNotFound);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Enumerable.Empty<LocalizedString>();
}
```

```csharp
spiderly.UseTranslations<DbStringLocalizer>();   // takes precedence over the built-in JSON localizer
```

Mirror the built-in's key-on-miss contract (`resourceNotFound: true`, return the key) so callers behave consistently.

## Frontend strings are a separate system

This skill is backend only. The Angular admin panel localizes through **Transloco** (`src/assets/i18n/{lang}.json` + `translocoService.translate(...)`), which is unrelated to the .NET `IStringLocalizer` here. For UI labels, menu items, validation messages, and auto-translated form labels, use the **frontend-localization** skill.
