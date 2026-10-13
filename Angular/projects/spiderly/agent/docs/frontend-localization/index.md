---
name: frontend-localization
description: How Spiderly localizes Angular admin-panel UI strings — labels, buttons, menu items, validation messages, any user-facing text. Spiderly uses Transloco with flat assets/i18n/{lang}.json files imported into the build by the app's ImportedTranslocoLoader. Use whenever you add or translate a UI string, hit a raw translation key rendering on screen, set up or change languages (provideTransloco / availableLangs), use translocoService.translate or the *transloco template directive, run i18n:extract, or wonder how form labels get auto-translated. For .NET backend strings (error messages, Excel names, IStringLocalizer), use the backend-localization skill instead.
---

# Frontend Localization

The Spiderly Angular admin panel localizes every user-facing string through **Transloco**. Translation files are flat key→value JSON, one per language, imported into the build: each becomes a content-hashed chunk that ships with the code reading its keys, loaded when Transloco first needs the language. Never hardcode user-facing English in a template; route it through a translation key so it localizes with the rest of the panel (e.g. a PACMS app runs entirely in Serbian).

This is a **separate system from the backend**: a string shown by the API (a `BusinessException` message) is translated on the .NET side via `IStringLocalizer` (see the **backend-localization** skill); a string rendered by the admin UI is translated here.

## Setup (`translations.ts`)

`spiderly init` writes the loader and the Transloco config to `src/app/business/services/translations.ts`; `app.config.ts` only calls `provideTranslations()`:

```typescript
@Injectable({ providedIn: 'root' })
export class ImportedTranslocoLoader implements TranslocoLoader {
  getTranslation(lang: string): Promise<Translation> {
    return import(`../../../assets/i18n/${lang}.json`).then((file) => file.default);
  }
}

export function provideTranslations() {
  return provideTransloco({
    config: {
      availableLangs: ['en'],          // every language you ship a JSON file for
      defaultLang: 'en',
      reRenderOnLangChange: true,      // false if users never switch language at runtime
    },
    loader: ImportedTranslocoLoader,
  });
}
```

So:

- Files live in **`src/assets/i18n/{lang}.json`**, named **exactly** after a `lang` in `availableLangs` (e.g. `sr-Latn-RS.json`, `en.json`).
- The build bundles them; they are **not** published as `/assets/i18n/*.json`. `angular.json` leaves them out of the assets glob (`"ignore": ["i18n/**"]`), so nothing can fetch them back.

### Why imported, not fetched

A file at an unchanging URL outlives a deploy in the browser cache. A server that sends no `Cache-Control` lets the browser guess a freshness lifetime (Chromium: a tenth of the time since `Last-Modified`), and a hard reload does not help: it bypasses the cache only for requests made before the page's load event, and Transloco asks for the file after it. The user then runs the new code against the old file, and every key added by the deploy renders raw. An imported file gets a new URL whenever its content changes, so this cannot happen. Spiderly's former `SpiderlyTranslocoLoader` fetched `${frontendUrl}/assets/i18n/{lang}.json` and was removed for this reason.

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
- **Filename must match `availableLangs`.** `ImportedTranslocoLoader` imports `{lang}.json` for the active lang; a mismatch (`sr.json` while `availableLangs` is `['sr-Latn-RS']`) fails the import and the whole file falls through to raw keys.
- **New keys render raw for some users right after a deploy, and a hard reload does not fix it?** Something still fetches the translations from a URL that survives deploys (see *Why imported, not fetched*). Check that `app.config.ts` calls `provideTranslations()` and that the network panel shows no `/assets/i18n/` request.
- **`reRenderOnLangChange: false`** means a runtime language switch won't re-render already-rendered text. Set it to `true` only if you actually offer in-session language switching.

## Backend strings are a separate system

This skill is the Angular admin panel only. Server-side strings (API error messages, Excel export names, anything from a `BusinessException`) are localized on the .NET side through `IStringLocalizer` and `Translations/{culture}.json` — a completely separate mechanism. Use the **backend-localization** skill for those.
