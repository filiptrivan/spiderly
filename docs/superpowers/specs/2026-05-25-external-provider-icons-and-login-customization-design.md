# External-provider icons on the frontend + login-component refactor & customization

**Date:** 2026-05-25
**Status:** Design — awaiting review
**Repos touched:** `spiderly` (Angular lib + backend), `spiderly-website` (docs), `pa-cms` (consumer)
**Builds on:** the external-auth-providers redesign (see `spiderly/docs/external-auth-providers.md`), which is implemented and committed but **not yet pushed**. This work rides on that same unpushed branch.

## Motivation

The admin login page renders a "Sign in with Google" button whose icon URL currently comes from the **backend** (`appsettings.json` → `ExternalProviders[].IconUrl`), exposed via `GET Security/GetExternalProviders`. PACMS hotlinks Google's CDN (`https://developers.google.com/identity/images/g-logo.png`).

Three problems, surfaced while scoping the icon change:

1. **Hotlinking a third-party CDN** for the icon is fragile (no stable URL contract), leaks the admin's IP/referrer to Google on every login load, needs a CSP allowance, and breaks offline. The icon is pure presentation and does not belong in backend config.
2. **No consumer control over the login page.** A consumer routes `/login` to the lib's login component and gets it as-is, or forks. The auth wrapper is not even exported, so a consumer cannot recompose their own login page from lib parts. There is no middle ground between "take everything" and "rebuild from scratch."
3. **`AuthComponent` violates single-responsibility.** It is simultaneously a layout shell (gradient card + logo), a company-branding fetcher, the entire external-provider feature (fetch + "or" separator + buttons + challenge redirect), a content host (`<ng-content>` for the form), and a back-channel that emits the company name up to the page (`onCompanyNameChange` — which the login template does not even consume). It cannot be named well because it has no single responsibility. The verification side of the module (`VerificationWrapperComponent` dumb UI + `LoginVerificationComponent` container) is, by contrast, cleanly factored — so this is an inconsistency to correct, not a module-wide rewrite.

We are already making breaking changes (renames, new public exports) and these components are only now becoming public API. This is the cheapest moment to fix the factoring — exporting a mis-factored `AuthComponent` would lock the confusion into the public API.

## Goals

- The provider icon lives on the **frontend**, mapped by provider `code`, overridable per provider; backend stops owning it.
- Split the login UI into single-responsibility, well-named, individually-exported components, mirroring the already-clean verification side.
- Give a consumer graduated control over the login page (defaults → wrapper → composition → content slots) without forking.
- Ship sensible defaults so an out-of-the-box app still shows the Google icon with zero config.

## Non-goals

- Moving the provider **`Label`** to the frontend. Label stays in backend config (short, locale-set per consumer; not in scope).
- Extracting the email form into its own `LoginFormComponent`. The form stays inline in the login page template (placed as content, no longer projected through a shell). A standalone form component is a possible later refinement, out of scope here.
- Touching the verification components beyond what the rename forces — they are already well-factored.
- Storefront (React) Google login (pending external-auth "slice 4"). When it lands it adopts the same frontend-icon-by-code approach.
- Scaffolding/generating the login page into the consumer app — considered and rejected as overkill.

## Architecture (from-scratch factoring)

`<auth>` is used by exactly one template (`login.component.html`); no registration page consumes it, so the refactor is fully localized to the login flow. `AuthComponent` is **deleted** and its responsibilities split:

| New component | Selector | Responsibility |
| --- | --- | --- |
| `AuthCardComponent` | `spiderly-auth-card` | Pure presentational shell: gradient card + logo + content slots. Fetches & displays company branding (logo/name) internally; **no output back-channel**. Hosts page content via `<ng-content>`. |
| `ExternalLoginComponent` | `spiderly-external-login` | The whole external-login feature: fetches enabled providers (`GetExternalProviders`), renders the "or" separator + provider buttons, resolves icons, initiates the challenge redirect. Owns `@Input() providerIcons`. Self-contained — droppable into login, registration, or the storefront later. |
| `SpiderlyLoginComponent` | `spiderly-login` | The page / route target (renamed from `LoginComponent`/`app-login`). Owns the email form + send-verification logic and the toggle to verification. Composes `<spiderly-auth-card>` with the email form and `<spiderly-external-login>` as its content. Exposes `@Input() providerIcons`, forwarded to `<spiderly-external-login>`. |
| `LoginVerificationComponent` | `login-verification` | Unchanged (container wiring resend/submit to `authService`). |
| `VerificationWrapperComponent` | `verification-wrapper` | Unchanged (dumb OTP UI with `@Output` events). |

This removes the `onCompanyNameChange` back-channel (the card self-brands) and the inverted content ownership (the page now owns its primary content — the form — instead of projecting it into a shell that also owns the secondary provider buttons).

### Icon resolution (frontend, by `code`)

New lib file `Angular/projects/spiderly/src/lib/components/auth/external-provider-icons.ts`:

```ts
/** Built-in default provider icons shipped by Spiderly. Inline data URI — no network fetch, no CSP entry, works offline. */
export const DEFAULT_EXTERNAL_PROVIDER_ICONS: Record<string, string> = {
  google: 'data:image/svg+xml;base64,…', // official Google "G" mark
};
```

`ExternalLoginComponent`:

```ts
/** Map of provider code -> icon (asset path, URL, or data URI). Per-code override; unset codes fall back to defaults. */
@Input() providerIcons: Record<string, string> = {};

iconFor(code: string): string | undefined {
  return this.providerIcons[code] ?? DEFAULT_EXTERNAL_PROVIDER_ICONS[code];
}
```

Template: `[iconUrl]="iconFor(provider.code)"`. When neither map has the code, `spiderly-button` already falls back to a label-only button. Resolution is per-code merge: `{ google: 'x' }` overrides only Google.

### Content slots (`AuthCardComponent`)

Named content-projection slots with fallback to current defaults:

- `[auth-logo]` — replaces the logo area (fallback: the company image the card fetches).
- default `<ng-content>` — page content (the form + external-login).
- `[auth-footer]` — below the content (fallback: nothing).

Empty-slot detection via `@ContentChild` so defaults render only when nothing is projected. `SpiderlyLoginComponent` forwards `[auth-logo]`/`[auth-footer]` to its inner `<spiderly-auth-card>` via `ngProjectAs`, so the default page (and the Level 1.5 wrapper) can override logo/footer through projection.

## Consumer control — the levels

| Level | Who | How |
| --- | --- | --- |
| 1 | any consumer | route to `SpiderlyLoginComponent`; built-in default icons show, zero config |
| 1.5 | **PACMS** | thin owned wrapper renders `<spiderly-login [providerIcons]="…">` |
| 2 | consumer needing a different form/layout | compose own page from `<spiderly-auth-card>` + `<spiderly-external-login>` + own form |
| 3 | any | named content slots `[auth-logo]` / `[auth-footer]` |

**Level 1.5 (PACMS, the chosen primary path)** — no inheritance, no logic duplication, no DI token, no route `data`:

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

The consumer is free to name their own component `LoginComponent` and use selector `app-login`.

**Level 2 (full composition)** — for a consumer who needs to restructure the form:

```ts
import { AuthCardComponent, ExternalLoginComponent, LoginVerificationComponent } from 'spiderly';
// template:
// <spiderly-auth-card>
//   <my-email-form />
//   <spiderly-external-login [providerIcons]="icons" />
// </spiderly-auth-card>
```

## Backend: remove `IconUrl`

Delete `IconUrl` from:

- `Spiderly.Shared/Options/ExternalProviderConfig.cs:51`
- `Spiderly.Shared/ExternalAuth/ExternalProviderPublicInfo.cs:23`
- `Spiderly.Security/DTO/ExternalProviderPublicDTO.cs:26`
- the projection in `Spiderly.Security/Services/SecurityServiceBase.cs:398`
- the `_publicConfigs` projection in `Spiderly.Shared/ExternalAuth/ExternalAuthProviderRegistry.cs:58`
- `schemas/appsettings.schema.json` (the `ExternalProviders` item shape)

`GetExternalProviders` continues to return `{ code, authority, clientId, label }` — still the source of truth for which providers are enabled and their identity config. Only the icon leaves.

## File-level change list

**Spiderly Angular lib**
- New `components/auth/external-provider-icons.ts` (`DEFAULT_EXTERNAL_PROVIDER_ICONS`).
- New `components/auth/auth-card/auth-card.component.{ts,html}` (`AuthCardComponent`, `spiderly-auth-card`) — branding fetch/display + slots, carved out of the old `AuthComponent`.
- New `components/auth/external-login/external-login.component.{ts,html}` (`ExternalLoginComponent`, `spiderly-external-login`) — provider fetch + buttons + icon resolution + challenge redirect, carved out of the old `AuthComponent`.
- Delete `components/auth/partials/auth.component.{ts,html}`.
- `components/auth/login/login.component.{ts,html}` → rename class `LoginComponent` → `SpiderlyLoginComponent`, selector `app-login` → `spiderly-login`; template now composes `<spiderly-auth-card>` + inline email form + `<spiderly-external-login [providerIcons]>`; add `@Input() providerIcons`; forward slots via `ngProjectAs`.
- `public-api.ts` — export `AuthCardComponent`, `ExternalLoginComponent`, `DEFAULT_EXTERNAL_PROVIDER_ICONS`, `SpiderlyLoginComponent` (replacing the `LoginComponent` export at line 24).

**Spiderly backend** — `IconUrl` removals listed above.

**Spiderly init template** — `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs:1305` (login route → `c.SpiderlyLoginComponent`); audit the emitted `ExternalProviders` block for `IconUrl` and remove.

**PACMS**
- New `app/.../login.component.ts` wrapper (Level 1.5) + route `/login` to it (`app.routes.ts:330`).
- `assets/icons/google.svg` (official Google mark, self-hosted).
- Remove `IconUrl` from `Backend/PACMS.WebAPI/appsettings.json:106` (and the BA instance's appsettings if present).

**spiderly-website** — update external-auth docs: icon-by-code on the frontend, the component split + rename, the control levels.

## Decisions

- **D1 — `LoginComponent` → `SpiderlyLoginComponent`, `app-login` → `spiderly-login`.** Frees the conventional name/selector for the consumer; consistent with the Spiderly prefix convention.
- **D2 — split `AuthComponent` into `AuthCardComponent` + `ExternalLoginComponent` (supersedes the earlier "rename AuthComponent" question).** Resolves the SRP violation and the naming concern; `AuthComponent` ceases to exist.
- **D3 — verification component selectors (`login-verification`, `verification-wrapper`) keep their current names** for now (well-factored, rarely rendered directly by consumers). Prefixing them is a possible later consistency pass, out of scope.

Spiderly makes breaking changes freely (per `spiderly/CLAUDE.md`), so no compatibility shims.

## Testing

- Lib: `cd spiderly/Angular && npm install && npx ng build spiderly` — compiles clean.
- PACMS admin: with the local-dev `spiderly` paths map enabled in `pa-cms/Frontend/tsconfig.json`, `npm install && npx ng build` — compiles clean; the new `LoginComponent` wrapper resolves `SpiderlyLoginComponent`.
- Backend: `dotnet build PACMS.Business` — no `IconUrl` references remain.
- Runtime (manual): login page renders the Google button with the self-hosted SVG; no request to Google's CDN; email-code login still works; logo/branding still render (card self-brands); slot overrides work.
- No new automated tests warranted (presentational wiring; integration covered by the existing auth flow). The external-auth `OptionsBindingTests` still pass with `IconUrl` removed.

## Rollout

Rides on the unpushed external-auth branch. Before pushing `spiderly`: the existing external-auth checklist still applies (e2e fixture / CI login helper — note the login selector changed `app-login` → `spiderly-login`, so the Playwright helper needs updating; `spiderly-website` docs). PACMS admin consumes local `spiderly` source via the tsconfig paths toggle during dev; a real PACMS release needs the lib published + dep bump.
