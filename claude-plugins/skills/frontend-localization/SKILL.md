---
name: frontend-localization
description: How Spiderly localizes Angular admin-panel UI strings — labels, buttons, menu items, validation messages, any user-facing text. Spiderly uses Transloco with flat assets/i18n/{lang}.json files loaded by SpiderlyTranslocoLoader. Use whenever you add or translate a UI string, hit a raw translation key rendering on screen, set up or change languages (provideTransloco / availableLangs), use translocoService.translate or the *transloco template directive, run i18n:extract, or wonder how form labels get auto-translated. For .NET backend strings (error messages, Excel names, IStringLocalizer), use the backend-localization skill instead.
---

# Frontend Localization

The Spiderly Angular admin panel localizes every user-facing string through **Transloco**. Translation files are flat key→value JSON, one per language, served as static assets and loaded over HTTP at runtime — there is no compile-time embedding. Never hardcode user-facing English in a template; route it through a translation key so it localizes with the rest of the panel (e.g. a PACMS app runs entirely in Serbian).

This is a **separate system from the backend**: a string shown by the API (a `BusinessException` message) is translated on the .NET side via `IStringLocalizer` (see the **backend-localization** skill); a string rendered by the admin UI is translated here.

## Setup (`app.config.ts`)

Transloco is registered in the app's `ApplicationConfig` with Spiderly's loader:

```typescript
provideTransloco({
  config: {
    availableLangs: ['sr-Latn-RS'],   // every language you ship a JSON file for
    defaultLang: 'sr-Latn-RS',
    reRenderOnLangChange: false,       // true only if you let users switch language at runtime
  },
  loader: SpiderlyTranslocoLoader,     // from the `spiderly` package
}),
```

`SpiderlyTranslocoLoader` fetches `${ConfigService.frontendUrl}/assets/i18n/{lang}.json`. So:

- Files live in **`src/assets/i18n/{lang}.json`**, named **exactly** after a `lang` in `availableLangs` (e.g. `sr-Latn-RS.json`, `en.json`).
- They must be deployed as static assets (they're requested over HTTP, not bundled). They already are under `src/assets`, but a build that drops the `assets/` glob will 404 the translations and every key renders raw.

## File format

A flat JSON object — no nesting:

```jsonc
// src/assets/i18n/sr-Latn-RS.json
{
  "Save": "Sačuvaj",
  "Products": "Proizvodi",
  "Product": "Proizvod",
  "InvalidSKU": "SKU mora biti 6–12 velikih slova/brojeva.",
  "WelcomeUser": "Dobrodošli, {{name}}!"
}
```

Parameters use Transloco's `{{param}}` syntax: `translocoService.translate('WelcomeUser', { name: user.name })`.

## Using translations

**In TypeScript** — inject `TranslocoService`:

```typescript
this.translocoService.translate('Products');
this.translocoService.translate('WelcomeUser', { name: user.name });
```

**In templates** — open a Transloco context once, then call `t(...)`:

```html
<ng-container *transloco="let t">
  <spiderly-button [label]="t('Save')"></spiderly-button>
  <h2>{{ t('Products') }}</h2>
</ng-container>
```

Add `TranslocoDirective` to the component's `imports` to use `*transloco`.

### Adding a new key

1. Add it to **every** `assets/i18n/{lang}.json` (one entry per language).
2. Reference it via `translate('Key')` / `t('Key')`.

Run **`npm run i18n:extract`** (transloco-keys-manager) to scan `src/` and add any missing keys to the JSON files automatically — use it instead of hand-syncing. Note it scans only your own `src/`, **not** `node_modules/spiderly`; keys used inside Spiderly's own library components are pre-seeded by `spiderly init`, so they resolve out of the box.

## Form label auto-translation

You rarely translate field labels by hand. When `BaseFormService` builds a form, it sets each control's `labelForDisplay` from `getTranslatedLabel(controlName)`, which normalizes the camelCase property name to a key and translates it:

- strips a trailing `Id` (`productId` → `Product`)
- strips a trailing `DisplayName` (`categoryDisplayName` → `Category`)
- upper-cases the first char, then `translate(...)`

So `name` → key `Name`, `productId` → key `Product`, `categoryDisplayName` → key `Category`. Provide those PascalCase keys in your i18n files and labels localize automatically. (The form/control mechanics themselves are documented in the **angular-customization** skill.)

## Translating menu items and validation messages

These are ordinary `translate(...)` calls — the surrounding mechanics live in **angular-customization**; only the translation call is shown here.

```typescript
// Menu (layout.component.ts)
menu: SpiderlyMenuItem[] = [
  { label: this.translocoService.translate('Dashboard'), icon: 'pi pi-fw pi-home', routerLink: ['/dashboard'] },
];

// Custom validator message (ValidatorAbstractService subclass)
if (value && !value.match(/^[A-Z0-9]{6,12}$/))
  return { _: this.translocoService.translate('InvalidSKU') };
```

## Static text in custom library-style components

If you build a reusable control/component with text baked into its template, route it through Transloco rather than hardcoding — same rule the Spiderly library follows for its own controls:

```html
<ng-container *transloco="let t">
  <p-tab>{{ t('Preview') }}</p-tab>
</ng-container>
```

## Gotchas

- **A missing key renders the raw key.** Transloco's default `missingHandler` returns the key string, so a typo'd or unseeded key leaks the identifier (e.g. `InvalidSKU`) onto the screen instead of erroring. Treat a raw-key sighting as a missing-translation bug. Keep keys in sync across all `{lang}.json` files and lean on `i18n:extract`.
- **Filename must match `availableLangs`.** `SpiderlyTranslocoLoader` requests `{lang}.json` for the active lang; a mismatch (`sr.json` while `availableLangs` is `['sr-Latn-RS']`) 404s and the whole file falls through to raw keys.
- **Translations are fetched, not bundled.** They load over HTTP from `frontendUrl/assets/i18n/`. A misconfigured `frontendUrl` (in `ConfigService`) or a build that strips the `assets` glob breaks all translations at once — a useful first thing to check when *everything* shows raw keys.
- **`reRenderOnLangChange: false`** means a runtime language switch won't re-render already-rendered text. Set it to `true` only if you actually offer in-session language switching.

## Backend strings are a separate system

This skill is the Angular admin panel only. Server-side strings (API error messages, Excel export names, anything from a `BusinessException`) are localized on the .NET side through `IStringLocalizer` and `Translations/{culture}.json` — a completely separate mechanism. Use the **backend-localization** skill for those.
