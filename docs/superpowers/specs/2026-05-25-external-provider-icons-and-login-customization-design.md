# External-provider icons on the frontend + login-page customization

**Date:** 2026-05-25
**Status:** Design — awaiting review
**Repos touched:** `spiderly` (Angular lib + backend), `spiderly-website` (docs), `pa-cms` (consumer)
**Builds on:** the external-auth-providers redesign (see `spiderly/docs/external-auth-providers.md`), which is implemented and committed but **not yet pushed**. This work rides on that same unpushed branch.

## Motivation

The admin login page renders a "Sign in with Google" button whose icon URL currently comes from the **backend** (`appsettings.json` → `ExternalProviders[].IconUrl`), exposed via `GET Security/GetExternalProviders`. PACMS hotlinks Google's CDN (`https://developers.google.com/identity/images/g-logo.png`).

Two problems:

1. **Hotlinking a third-party CDN** for the icon is fragile (no stable URL contract), leaks the admin's IP/referrer to Google on every login load, needs a CSP allowance, and breaks offline. The icon is pure presentation and does not belong in backend config.
2. **No consumer control over the login page.** A consumer routes `/login` to the lib's `LoginComponent` and gets it exactly as-is, or forks. The `AuthComponent` wrapper (logo + provider buttons + form slot) is not even exported, so a consumer cannot recompose their own login page from lib parts. There is no middle ground between "take everything" and "rebuild from scratch."

## Goals

- The provider icon lives on the **frontend**, mapped by provider `code`, overridable per provider (Google and any future provider).
- Backend stops owning the icon — `IconUrl` is removed from config, the DTO, and the public endpoint.
- A consumer gets graduated control over the login page (config → wrapper → full composition → slots) without forking.
- The lib ships sensible defaults so an out-of-the-box app still shows the Google icon with zero config.

## Non-goals

- Moving the provider **`Label`** to the frontend. Label stays in backend config (it is short, locale-set text per consumer, and was not in scope). Revisit later if needed.
- Storefront (React) Google login — that is the still-pending "slice 4" of the external-auth work. When it lands it will adopt the same frontend-icon-by-code approach (its own React-side map), which this design is consistent with.
- Scaffolding/generating the login page into the consumer app. Considered and rejected as overkill (large framework change + ongoing sync burden when the auth flow evolves).

## Design

### Icon resolution (frontend, by `code`)

New lib file `Angular/projects/spiderly/src/lib/components/auth/external-provider-icons.ts`:

```ts
/** Built-in default provider icons shipped by Spiderly. Inline data URI — no network fetch, no CSP entry, works offline. */
export const DEFAULT_EXTERNAL_PROVIDER_ICONS: Record<string, string> = {
  google: 'data:image/svg+xml;base64,…', // official Google "G" mark
};
```

`SpiderlyAuthComponent` (renamed from `AuthComponent`, see Decision D2) gains:

```ts
/** Map of provider code -> icon (asset path, URL, or data URI). Per-code override; unset codes fall back to defaults. */
@Input() providerIcons: Record<string, string> = {};

iconFor(code: string): string | undefined {
  return this.providerIcons[code] ?? DEFAULT_EXTERNAL_PROVIDER_ICONS[code];
}
```

Template change: `[iconUrl]="iconFor(provider.code)"` instead of `[iconUrl]="provider.iconUrl"`. When neither map has the code, `spiderly-button` already falls back to a label-only button.

Resolution is **per-code merge**: passing `{ google: 'x' }` overrides only Google; other providers keep their defaults.

### Backend: remove `IconUrl`

Delete `IconUrl` from:

- `Spiderly.Shared/Options/ExternalProviderConfig.cs:51`
- `Spiderly.Shared/ExternalAuth/ExternalProviderPublicInfo.cs:23`
- `Spiderly.Security/DTO/ExternalProviderPublicDTO.cs:26`
- the projection in `Spiderly.Security/Services/SecurityServiceBase.cs:398`
- `Spiderly.Shared/ExternalAuth/ExternalAuthProviderRegistry.cs:58` (the `_publicConfigs` projection)
- `schemas/appsettings.schema.json` (the `ExternalProviders` item shape)

The `GetExternalProviders` endpoint continues to return `{ code, authority, clientId, label }` — it remains the source of truth for **which** providers are enabled and their identity config. Only the icon leaves.

### Consumer control — the levels

| Level | Who | How |
| --- | --- | --- |
| 1 | any consumer | route to lib `SpiderlyLoginComponent`; built-in default icons show, zero config |
| 1.5 | **PACMS** | thin owned wrapper renders `<spiderly-login [providerIcons]="…">` |
| 2 | consumer needing a different form/layout | compose own page from `<spiderly-auth>` + lib parts |
| 3 | any | named content slots `[auth-logo]` / `[auth-footer]` |

**Level 1.5 (PACMS, the chosen primary path).** A tiny wrapper holds the icon map and passes it down — no inheritance, no logic duplication, no DI token, no route `data`:

```ts
@Component({
  selector: 'app-login',
  imports: [SpiderlyLoginComponent],
  template: `<spiderly-login [providerIcons]="providerIcons" />`,
})
export class LoginComponent {
  providerIcons = { google: 'assets/icons/google.svg' };
}
```

`SpiderlyLoginComponent` gains `@Input() providerIcons` and forwards it to its inner `<spiderly-auth>`. The consumer is now free to name their own component `LoginComponent` and use selector `app-login` (Decision D1).

**Level 2 (full composition).** With `SpiderlyAuthComponent` exported, a consumer who needs to restructure the email form builds their own page from parts:

```ts
import { SpiderlyAuthComponent, LoginVerificationComponent } from 'spiderly';
// template: <spiderly-auth [providerIcons]="icons"><my-email-form /></spiderly-auth>
```

**Level 3 (slots).** `SpiderlyAuthComponent` gets named content-projection slots with fallback to the current defaults:

- `[auth-logo]` — replaces the logo area (fallback: existing company image from config)
- default `<ng-content>` — the form (unchanged)
- `[auth-footer]` — below the provider buttons (fallback: nothing)

Empty-slot detection via `@ContentChild` so defaults render only when nothing is projected. `SpiderlyLoginComponent` forwards `[auth-logo]`/`[auth-footer]` to its inner `<spiderly-auth>` via `ngProjectAs`, so even the default page (and the Level 1.5 wrapper) can override logo/footer through projection without rebuilding.

### Renames

**D1 — `LoginComponent` → `SpiderlyLoginComponent`, selector `app-login` → `spiderly-login`.** Frees the conventional `LoginComponent` class name and `app-login` selector for the consumer's own component. Consistent with the existing Spiderly prefix convention (`SpiderlyButtonComponent`, `spiderly-textbox`, …), which `LoginComponent` currently violates.

References to update:
- `Angular/projects/spiderly/src/public-api.ts:24` (export)
- `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs:1305` (init template route → `c.SpiderlyLoginComponent`)
- `pa-cms` `app.routes.ts:330` (will route the new PACMS wrapper instead)
- `spiderly-website` docs

**D2 — `AuthComponent` → `SpiderlyAuthComponent`, selector `auth` → `spiderly-auth` (recommended; confirm in review).** `AuthComponent` becomes part of the public composition API (Level 2). The bare `auth` selector and `AuthComponent` name are generic and likely to clash with a consumer's own. Prefixing them serves the same goal as D1. The user explicitly requested only the login rename, so this is flagged for confirmation — if vetoed, export `AuthComponent` under its current name/selector.

Spiderly makes breaking changes freely (per `spiderly/CLAUDE.md`), so the renames need no compatibility shim.

### PACMS consumer changes

- Add `apps`-side `LoginComponent` wrapper (Level 1.5 above) and route `/login` to it (`app.routes.ts`).
- Add `assets/icons/google.svg` (the official Google mark, self-hosted).
- Remove `IconUrl` from `Backend/PACMS.WebAPI/appsettings.json:106` (and the BA instance's appsettings if it carries one).

### Docs & template

- Update `spiderly-website` external-auth docs: icon-by-code on the frontend, the rename, the three levels.
- Audit `NetAndAngularFilesGenerator.cs` for any emitted `ExternalProviders` block carrying `IconUrl` and remove it; update the login route reference (D1).

## Testing

- Lib: `cd spiderly/Angular && npm install && npx ng build spiderly` — compiles clean.
- PACMS admin: with the local-dev `spiderly` paths map enabled in `pa-cms/Frontend/tsconfig.json`, `npm install && npx ng build` — compiles clean; new `LoginComponent` wrapper resolves `SpiderlyLoginComponent`.
- Backend: `dotnet build PACMS.Business` — no `IconUrl` references remain.
- Runtime (manual): login page renders the Google button with the self-hosted SVG; no request to Google's CDN; email-code login still works.
- No new automated tests warranted (presentational wiring; integration covered by the existing auth flow). The external-auth `OptionsBindingTests` still pass with `IconUrl` removed (fewer fields).

## Rollout

This rides on the unpushed external-auth branch. Before pushing `spiderly`: the existing external-auth checklist still applies (e2e fixture / CI login helper, `spiderly-website` docs). PACMS admin consumes local `spiderly` source via the tsconfig paths toggle during dev; a real PACMS release needs the lib published + dep bump.
