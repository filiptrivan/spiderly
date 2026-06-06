# Spiderly External Authentication Providers — Design

> Status: **ALL SLICES DONE + compile-verified.** Slice 4 (PACMS consumer migration + docs) complete. This doc reflects the fully-implemented design. Keep it in sync if behavior changes.
>
> **Slice 3a — done.** `GET /api/security/GetExternalProviders` added to `SecurityBaseController` (anonymous, inherited by consumer `SecurityController`s — no template change). Returns `List<ExternalProviderPublicDTO>` ([SpiderlyDTO], in `Spiderly.Security/DTO`). No controller/service ctor ripple: the registry (already injected into `SecurityServiceBase`) gained `GetPublicConfigs()` returning `ExternalProviderPublicInfo` (Shared), built once in its ctor (authority resolved via preset); `SecurityServiceBase.GetExternalProviders()` maps Shared info → the SpiderlyDTO. `PACMS.Business`/`PACMS.WebAPI` build clean.
> Spiderly has **no backward-compatibility constraint** (see root `CLAUDE.md`), so the old Google-specific code is deleted outright rather than deprecated.
>
> **Slice 2 split into 2a/2b/2c** (entanglement + a destructive column-drop made one-shot risky):
> - **2a — done, additive only.** `IUserExternalLogin`; `SecurityServiceBase<TUser, TUserExternalLogin>` + `SecurityBaseController<TUser, TRole, TUserExternalLogin>`; `LoginExternal` now resolves by `(Provider, Subject)` and auto-links/creates on verified email (`AuthPolicyOptions.AutoLinkByVerifiedEmail`, default true). `BusinessException` gained an optional `ErrorCode`; `ApiErrorCodes.EmailNotVerified`/`ExternalProviderNotConfigured` added to all 3 mirrors. PACMS got a scaffolded `UserExternalLogin` (`[SpiderlyEntity]` + class-level `[UIDoNotGenerate]`, unique `(Provider, ProviderKey)`) + migration `20260524154257_AddUserExternalLogin` (table only) applied. Entity gotcha: a non-nullable FK (`UserId`) requires `[Required]` on the `User` navigation or the `SPIDERLY006` diagnostic cascades into missing-Mapper-config errors.
> - **2b — done, destructive.** Removed the bool from `IUser`/`User`; the `ApplicationDbContext` `Ignore()` branch + its `IOptions<ExternalProviderOptions>` ctor params (both) + `ExternalProviderOptions.UseGoogleAsExternalProvider` + the schema entry; deleted the 2 `AuthorizationService` guards (update + insert — there were 2, not 3). Ripples fixed: `PACMSApplicationDbContext`, `MigrationsDbContextFactory`, `TestDbContextFactory` all lost the `ExternalProviderOptions` ctor arg; hand-written Angular `user-details.component.{ts,html}` lost the `showHasLoggedInWithGoogleAsExternalProvider` field/binding/`.disable()`. Migration `20260524155747_DropUserGoogleExternalProviderColumn` (drops the one column) applied.
> - **2c — done, propagation.** Init template (`NetAndAngularFilesGenerator.cs`): User-entity template lost the bool + gained `ExternalLogins`; new `GetUserExternalLoginCsData` emitted as `UserExternalLogin.cs`; DbContext template + its design-time factory lost the `ExternalProviderOptions` ctor arg; `SecurityService<TUser>` → `SecurityService<TUser, TUserExternalLogin>`; `SecurityController`/`AuthorizationService`/DI registrations bumped to the new arity; the 2 AuthorizationService guard strings + the Angular `show…` template bits + the stale translation removed. E2e fixture `User.cs` lost the bool + gained `ExternalLogins`. **Verification note:** `Spiderly.Shared` compiles (the generator's own C#); the real gate is the CI e2e `init→build` job on push.

## Two acquisition front-doors, one core — *do not collapse to one*

External login deliberately has **two token-acquisition mechanisms**. They are **not** redundant and neither supersedes the other; they share a single validation + linking + session core (`IExternalAuthProviderRegistry` → `ResolveExternalUser` → cookie session). Only *how the id token is obtained* differs.

| Surface | Mechanism | Endpoints | Why this one |
|---|---|---|---|
| **Admin** (Angular) | Server-side OAuth **code flow** (B2) | `ExternalLoginChallenge` / `ExternalLoginCallback` | Must support **arbitrary, config-driven OIDC providers**, including confidential / enterprise IdPs (Entra, Okta, Keycloak). A browser code+PKCE flow is impossible for Google's *confidential* web client (no `client_secret` in the browser), and GIS is Google-only — so only a backend-owned code flow is both generic **and** works for confidential clients. |
| **Storefront** (Next.js) | Google Identity Services **id token** | `LoginExternal` / `LoginExternalWithCookies` | Only needs Google → GIS hands the browser an id token directly (no `client_secret`), with One-Tap UX a full-page redirect can't match. |

**Consequences for anyone tempted to delete code:**

- `LoginExternal` / `LoginExternalWithCookies` are **live and load-bearing** — the storefront's customer Google login depends on `LoginExternalWithCookies`. They are *not* "superseded by B2"; the `FINAL DIRECTION` note below describes the **admin's** migration only.
- The two paths are kept at **security parity**: B2 binds a server-issued nonce (the state cookie); the id-token path does too via `GetExternalLoginNonce` — a server-issued nonce in a Data-Protection-signed HttpOnly cookie, echoed into the id token (`initialize({ nonce })`) and checked against the cookie on login, single-use. Don't add a third path at a lower posture, and don't drop the nonce from either.
- `IExternalAuthProvider` (validation) and `ResolveExternalUser` (linking/provisioning) are **mechanism-agnostic** — a future provider or flow plugs into the same seam. If needs change, add behind the seam; don't fork a parallel core.

## Findings — Facebook on the web is standard OIDC (research 2026-06-04, for PACMS storefront)

> Captured while designing customer Facebook login for the PACMS storefront. **This corrects decision 2's framing** of Facebook as a "non-OIDC oddball (Facebook's `debug_token`)" — that holds only for the *classic JS-SDK access-token* path, not the web OIDC path below. Everything except the front-door choice is still **OPEN**.

Verified directly against Facebook's live endpoints:

- **Facebook publishes a standard OIDC discovery document** at `https://www.facebook.com/.well-known/openid-configuration/`:
  - `issuer`: `https://www.facebook.com`
  - `jwks_uri`: `https://www.facebook.com/.well-known/oauth/openid/jwks/` (RS256, rotating keys — handled automatically by the generic validator's `ConfigurationManager`).
  - `authorization_endpoint`: `https://facebook.com/dialog/oauth/`
  - `response_types_supported`: `["id_token", "token id_token"]` — the **implicit `id_token` flow is available on the web**. A browser redirect with `response_type=id_token&scope=openid email&nonce=…` returns a nonce-bearing JWT in the URL fragment, exactly like Google Identity Services hands one over.
  - `claims_supported`: `iss, aud, sub, iat, exp, jti, nonce, at_hash, name, given_name, middle_name, family_name, email, picture, …` — **note: no `email_verified`.**
  - **No `token_endpoint`** in discovery (it describes only the implicit surface). A separate Facebook doc documents a `response_type=code` + backend exchange at `graph.facebook.com/oauth/access_token`, but that path is *not* advertised in discovery, so the generic OIDC **code flow** (`ExternalAuthCodeFlow`, which needs `token_endpoint`) does **not** transparently reuse for Facebook.

**Consequences:**

1. **Facebook reuses the storefront's existing id-token front door — it is NOT a new mechanism.** Add a preset (`["facebook"] = "https://www.facebook.com"`) + a config entry `{ "Code": "facebook", "ClientId": "<appId>" }`, and `GenericOidcExternalAuthProvider` validates the token (discovery + JWKS + iss/aud/exp) with the **existing nonce guard working unchanged** (the token carries a `nonce` claim). No `debug_token` provider, no `client_secret` for the implicit flow.
2. **The one real gap: the missing `email_verified` claim.** `GenericOidcExternalAuthProvider` sets `EmailVerified = GetBool(claims, "email_verified")` → `false` for every Facebook token → `ResolveExternalUser` throws `EmailNotVerified` → **every Facebook login fails**. **Decided:** add a per-provider `bool TrustEmailVerified` to `ExternalProviderConfig`; the generic validator becomes `EmailVerified = TrustEmailVerified ? Email != null : GetBool(claims, "email_verified")`. Consumers set it only on providers they vouch verify the emails they return (Facebook); the strict claim check stays the default for everyone else. Rationale: Spiderly is passwordless email-code, so the `email_verified` gate only guards against a *misconfigured* IdP returning unverified emails — Facebook verifies them, it just omits the claim. *(Framework change — not yet implemented.)*
3. **No-email accounts.** Facebook can return a valid login with **no email** (user unchecked the email permission, or a phone-only account). Even with `TrustEmailVerified`, `Email == null` must not reach provisioning — `new TUser { Email = null }` would violate the required/unique `Email`. **Decided:** branch `ResolveExternalUser` to throw a new `ApiErrorCodes.ExternalEmailMissing` (distinct from `EmailNotVerified`; 3 mirrors per the contract rule) *before* the create path, so the consumer UI can route the user to another method. *(Framework change — not yet implemented.)*
4. Residual risk: the implicit `id_token` response type is *advertised* in discovery but Facebook may gate it per app — **confirm against a real app config during the implementation spike.**

**Decided direction (framework):** implicit `response_type=id_token` via full-page redirect → consumer callback parses the `#id_token` fragment (and strips it via `history.replaceState`) → POST to `LoginExternalWithCookies` (session cookie lands on the POST response, so no cross-site redirect-cookie bounce). Facebook is a **second provider on the same storefront id-token front door as Google** — *not* a new mechanism, and *not* the `debug_token` custom provider that decision 2 once implied. Framework deltas: the `["facebook"] = "https://www.facebook.com"` preset, `ExternalProviderConfig.TrustEmailVerified`, `ExternalProviderConfig.ShowInProviderList` (a storefront-only provider with a hardcoded button opts out of the admin-facing `GetExternalProviders` list while still being validated), and `ApiErrorCodes.ExternalEmailMissing`.

The **PACMS-consumer** specifics (markets, storefront button + `provider` plumbing, Facebook app + go-live checklist) live in `pa-storefront` → `apps/rs/docs/superpowers/specs/2026-06-06-facebook-login-design.md`.

## Problem

A Spiderly app needs to let users sign in with external identity providers. Spiderly ships **Google** built-in, but consumers must be able to **add their own provider** (Microsoft/Entra, Auth0, Keycloak, a corporate SSO, …) **without forking the framework**.

Today everything is hard-coded to Google through every layer:

- `ExternalProviderOptions { UseGoogleAsExternalProvider, GoogleClientId }` — flat, Google-specific config.
- `ExternalProviderCodes` enum (`None`, `Google`) — **defined but effectively unused**.
- `ExternalProviderDTO { IdToken, BrowserId }` — the `Provider` field is **commented out** ("we only have Google").
- `IUser.HasLoggedInWithGoogleAsExternalProvider` — a **per-provider boolean on the user**; `ApplicationDbContext` conditionally `Ignore()`s the column.
- `SecurityServiceBase.LoginExternal` — validates via `Google.Apis.Auth` and matches the user **by email only**, with no provider branching.
- Angular: `createFakeGoogleWrapper()` renders a hidden Google Identity Services button off-screen and clicks it programmatically; `config.service.base.ts` carries `GoogleClientId` + `showGoogleAuth`.

Two things here are what *no* mature framework does: identity modeled as a **per-provider bool**, and account linking **by email**. This design replaces both and inverts the Google hard-coding into an open extension seam.

## Reference: what other frameworks do

| Framework | Adding a provider | Identity storage | Token acquisition |
|---|---|---|---|
| ASP.NET Core Identity | `.AddGoogle()` / generic `.AddOpenIdConnect()` handler | `AspNetUserLogins (LoginProvider, ProviderKey, UserId)` — by **subject** | server redirect/code flow |
| Auth.js / NextAuth | ~80 built-ins = config presets over a generic OIDC engine | `Account (provider, providerAccountId)` | code flow (also id_token) |
| Spring Security | `ClientRegistration` config + `CommonOAuth2Provider` presets | linked-accounts table by subject | code flow |
| Supabase / GoTrue | enable provider in config | `identities (provider, provider_id)` | code flow |
| Laravel Socialite / OmniAuth / allauth | drivers/strategies over a reusable OAuth2 base | `social_accounts (provider, uid)` | code flow |

**Universal across all of them:** (1) a linking table keyed by `(provider, subject)`, never a bool, never email; (2) a reusable OIDC/OAuth engine with per-provider code reduced to a thin preset/config. The "give consumers an interface and let them validate tokens themselves" model is shipped by *no* mature framework.

## Core decisions (and the rationale)

1. **Validation model: stateless backend id_token validation** (kept from today, not switched to server-side code flow). The browser obtains the id_token; the backend never owns a redirect/callback endpoint. This is SPA/storefront-friendly and is where the "config-only provider" goal is cleanest.

2. **Uniform provider abstraction — one interface, built-ins are instances of it.**
   - Contract: `IExternalAuthProvider { string Code; Task<ExternalIdentity> ValidateAsync(string idToken); }`
   - Result: `ExternalIdentity { string Provider; string Subject; string Email; bool EmailVerified; string Name; }`
   - Spiderly ships a `GenericOidcExternalAuthProvider` that validates **any** OIDC id_token from `{ authority, clientId }` config, using `Microsoft.IdentityModel.*` (`ConfigurationManager<OpenIdConnectConfiguration>` for discovery + cached/rotated JWKS). **`Google.Apis.Auth` is deleted** — Google is just a standard OIDC provider.
   - Built-ins (`"google"`, `"microsoft"`) are **instances of the generic provider**, auto-registered per config entry. A consumer adding a standard provider does it with **pure config**. A non-OIDC oddball (Apple's JWT-client-secret, Facebook's `debug_token`) registers their **own** `IExternalAuthProvider` keyed by the same code, which **shadows** the generic one.
   - **Single resolution path:** `registry[providerCode].ValidateAsync(idToken)`. No two-path branching; overriding a built-in is just registering your own impl for its code. (This is the NextAuth / ASP.NET-scheme shape.)

3. **Provider identity is a string code, not an enum.** A compile-time enum can't be extended by a consumer without editing the framework — directly contradicting "add their own." The `ExternalProviderCodes` enum is **deleted**; `Provider` is a string everywhere (DTO, config, the linking table). Tradeoff: typos are runtime config errors, not build errors — mitigated by `ValidateOnStart` rejecting unknown/duplicate codes at boot.

4. **Config is a keyed list + a preset registry**, replacing the flat Google-specific options.
   ```jsonc
   "ExternalProviders": [
     { "Code": "google",    "ClientId": "..." },                               // authority from preset
     { "Code": "microsoft", "Authority": "https://login.microsoftonline.com/<tenant>/v2.0", "ClientId": "..." }
   ]
   ```
   - A small **preset registry** auto-fills well-known authorities (`Code:"google"` → `https://accounts.google.com`), à la Spring's `CommonOAuth2Provider`. Consumers supply `Authority` inline for anything not presetted.
   - `ExternalProviderOptions.{UseGoogleAsExternalProvider, GoogleClientId}` are **deleted** (`UseGoogleAsExternalProvider` only existed to toggle the bool column, which is gone).

5. **Identity persistence: a scaffolded, consumer-owned linking table** — the per-provider bool and email-only matching are **deleted**.
   - New interface `IUserExternalLogin { long UserId; string Provider; string ProviderKey; }` (`ProviderKey` = the provider's stable `sub`/subject).
   - The concrete `UserExternalLogin` entity is **scaffolded into the consumer** by `spiderly init` — same path as `User`/`Role`/`Permission` — as `[SpiderlyEntity]` + `[UIDoNotGenerate]` (no auto CRUD pages; a hand-edited `ProviderKey` would be a footgun). Linked providers surface **read-only on the User detail page**.
   - Unique index on `(Provider, ProviderKey)`. One user → many external logins (Google *and* Microsoft).
   - **Why scaffolded (not framework-owned):** consistent with how Spiderly already treats the security model; the consumer can add columns (`LinkedAt`, `LastUsedAt`, provider email for display). **Cost:** the framework can't reference the concrete table, so it operates on it abstractly — see decision 6.

6. **A second generic type parameter threads the linking table through the security layer** (the price of decision 5, exactly as ASP.NET Identity's `UserStore<TUser, TRole, …, TUserLogin, …>` pays it):
   - `SecurityServiceBase<TUser, TUserExternalLogin>` (calls `_context.DbSet<TUserExternalLogin>()`).
   - `SecurityBaseController<TUser, TRole, TUserExternalLogin>` (there is already a `TRole`, so this is consistent, not novel).

7. **Login resolution & provisioning — UX-first, one guardrail.** Given a validated `(Provider, Subject, Email, EmailVerified)`:
   1. `(Provider, Subject)` already linked → log in. *(always unambiguous)*
   2. Not linked, `EmailVerified == true`, a user with that email exists → **auto-link** (write the `UserExternalLogin` row), **no interstitial, no prior sign-in required**.
   3. Not linked, verified, no such user → **create**, subject to the existing `OnlyAdminCanAddUsers` policy.
   4. `EmailVerified != true` → **reject**.
   - Toggle `AutoLinkByVerifiedEmail` (default `true`) so an app that later adds password login can tighten to explicit-authenticated linking.
   - **Rationale:** Spiderly login is **passwordless email-code**, so controlling the email address already grants full account access. Auto-linking on a *verified* email therefore grants an attacker **nothing they couldn't get via an email code** — the strict "authenticated linking" ceremony (ASP.NET/NextAuth) protects *password* accounts, which Spiderly doesn't have. The `email_verified` gate is the real and sufficient control, and it's **invisible to legitimate users** (Google/Microsoft always return `email_verified == true`); it only fires for a misconfigured/custom IdP — exactly the takeover case. So it costs zero UX and closes the one genuine hole. Dropping it was explicitly rejected.

8. **DTO carries the provider code.** `ExternalProviderDTO { Provider, IdToken, BrowserId }` — the previously-dead `Provider` field is revived as the routing key the backend uses to select the validator.

> **FINAL DIRECTION (slice 3): B2 server-side code flow + migrate the admin to HttpOnly-cookie sessions.** The admin's `localStorage`/Bearer JWT session is treated as tech debt (XSS-exfiltratable) and replaced with the framework's existing **cookie** auth (HttpOnly, what the storefront already uses) — so B2's server redirect lands cleanly with no token-handoff hack. **ALL THREE SLICES DONE + compile-verified (lib `ng build` ✓, PACMS admin `ng build` ✓ via local link, backend ✓). Runtime-test pending.** Slice 2 (admin → cookie sessions): rewrote lib `AuthServiceBase` to cookie mode (`loginWithCookies`/`refreshTokenWithCookies`/`logoutWithCookies`, no localStorage tokens, timer via `accessTokenExpiresAt`, `clearSession()` on 401, app-init refresh restores session); `jwtInterceptor` → `withCredentials`; removed `angular-auth-oidc-client` everywhere (lib/PACMS/template/package.jsons), reverted `app.config`/`auth.service`/`AppComponent`. Slice 3: provider button → full-page navigate to `ExternalLoginChallenge`. Backend was already cookie-ready (`OnMessageReceived` reads JWT from cookie, `AuthGuard` cookie-aware, CORS `.AllowCredentials()`), so slice 2 was frontend-only.
> **Slice-1 (backend B2) DONE + compile-verified** (`ExternalAuthCodeFlow` helper in Spiderly.Shared = discovery + authorize-URL + code→token exchange; `SecurityServiceBase.BeginExternalLoginAsync`/`CompleteExternalLoginAsync` = state/nonce/PKCE via `IDataProtector`, reuse `ResolveExternalUser` + cookie issuance; `SecurityBaseController.ExternalLoginChallenge`/`ExternalLoginCallback`; `ExternalProviderConfig` gained `ClientSecret`+`Scopes`; registered `ExternalAuthCodeFlow` singleton; template `SecurityService` ctor + appsettings updated). **Open: client_secret in local config, register the BACKEND callback redirect_uri in Google Console, open-redirect guard on returnUrl (TODO in code), slices 2+3.** Three slices: (1) backend B2 endpoints (`ExternalLoginChallenge`/`ExternalLoginCallback`, code→token exchange with `client_secret`, reuse generic-OIDC validation + `ResolveExternalUser`, issue **cookie** session; state/nonce/PKCE in a Data-Protection-signed cookie); (2) migrate **all** admin login (email-code included) to cookie endpoints (`LoginWithCookies`/`RefreshTokenWithCookies`/`LogoutWithCookies`, `withCredentials`, drop localStorage + Bearer interceptor, CORS `AllowCredentials`); (3) external button → navigate to the challenge URL. **The `angular-auth-oidc-client` + popup work from the earlier 3b attempt is removed.** B1 (JSON `AuthResult` → localStorage) was considered and rejected by the user as preserving the localStorage tech debt.

> **(earlier, superseded) switched to server-side code flow (Option B2).** The "browser obtains the id_token via a generic OIDC client" premise **failed for Google**: Google "Web application" OAuth clients are *confidential* — the token endpoint requires `client_secret` even with PKCE (`error: "client_secret is missing"`), and Google has no public-SPA client type. So the browser cannot complete a code/PKCE exchange. Decision: **the backend owns the whole OAuth dance** (ASP.NET `.AddGoogle()`-style). Frontend `angular-auth-oidc-client` + popup are **removed**; the provider button just navigates to a backend challenge URL. Backend gains `ExternalLoginChallenge` + `ExternalLoginCallback` endpoints, does the code→token exchange with `client_secret` (server-side), validates the id_token (reusing the generic OIDC validator), then `ResolveExternalUser` + cookie session (all reused from slices 1–2). `redirect_uri` is now the **backend callback**, re-registered in Google Console. The old decisions 9–11 below are superseded for the *acquisition* mechanism; the *validation + linking* (1–8) stand.

9. **(SUPERSEDED by B2 — kept for history) Frontend: a generic browser OIDC client, config-driven** (Angular admin; the Next.js storefront reuses the same API contract and providers endpoint).
   - Replace `createFakeGoogleWrapper` + the GIS-specific button with a generic OIDC client doing **Authorization Code + PKCE + nonce**. Configured by the same `{ authority, clientId }`, it works for any OIDC provider; it hands back an id_token that the SPA POSTs to `loginExternal`. The **backend stays a stateless id_token validator** — the *browser* does the redirect/callback.
   - **Why (A) generic client over (B) per-provider JS SDKs:** symmetry with the backend — "add a provider = config" holds on *both* ends. The cost is losing Google's One-Tap nicety and adding a frontend dependency; accepted.
   - **Library (revised during slice 3): the Angular admin uses `angular-auth-oidc-client`** (Angular-native, OpenID-certified — handles DI, callback route, and silent renew), not the framework-agnostic `oidc-client-ts` named earlier. The admin and storefront never share this code, so the "one library across both" point for `oidc-client-ts` is weak; the Angular-native lib makes the admin smaller/idiomatic. The **storefront (slice 4)** picks its own React-native OIDC approach. Provider configs load **dynamically from `GetExternalProviders`** (`angular-auth-oidc-client`'s `StsConfigHttpLoader`), so adding a provider stays backend-config-only. The admin's current **`@abacritt/angularx-social-login` dependency is removed**.

10. **Config delivery: a public providers endpoint** — `GET /api/security/external-providers` returns the enabled providers' **public** config `[{ code, authority, clientId, label }]`. Backend is the single source of truth for which providers are enabled and their OIDC identity config; both frontends fetch it at startup and render buttons dynamically. `clientId`/`authority` are public by OIDC design, so exposure is fine (this is what NextAuth's `/api/auth/providers` does). `config.service.base.ts`'s `GoogleClientId`/`showGoogleAuth` collapse into this fetched list. Chosen over build-time frontend config to avoid the config living in three places and drifting. **`IconUrl` is NOT part of this endpoint** — icon resolution is purely frontend, mapped by provider `code` (see decision 12).

11. **Replay protection: baseline + nonce, no replay cache.** TLS + strict `exp`/`aud`/`iss` validation, plus **nonce** binding (the SPA generates a one-time value, the provider echoes it into the id_token, the backend verifies the echo — defeats token injection/replay across contexts). `oidc-client-ts` handles the nonce plumbing, so it's near-free. A stateful single-use/`jti` cache (Redis) was deliberately **not** added — marginal gain over short-lived-token + TLS + nonce, and it adds infrastructure to an otherwise stateless validator. Can be added later behind the same `IExternalAuthProvider` seam if a consumer needs it.

12. **Provider icon lives on the frontend, mapped by `code`.** `IconUrl` is removed from the backend config, DTO, and endpoint (it was pure presentation — no stable URL contract, leaks referrer to third-party CDNs, needs a CSP allowance, breaks offline). The Angular lib ships `DEFAULT_EXTERNAL_PROVIDER_ICONS: Record<string, string>` (Google "G" mark as an inline `data:` URI — no network, no CSP, works offline). `ExternalLoginComponent` exposes `@Input() providerIcons: Record<string, string>` for per-code overrides; `iconFor(code)` resolves `providerIcons[code] ?? DEFAULT_EXTERNAL_PROVIDER_ICONS[code]`. When neither map has the code, the button renders label-only. `SpiderlyLoginComponent` forwards `providerIcons` down to `<spiderly-external-login>`.

    **Frontend component split** — the monolithic `AuthComponent` is **deleted** and replaced with three single-responsibility components:

    | Component | Selector | Responsibility |
    |---|---|---|
    | `AuthCardComponent` | `spiderly-auth-card` | Presentational shell: gradient card + logo/branding + content slots (`[auth-logo]` with fallback, default `<ng-content>`, `[auth-footer]`). Self-brands; no output back-channel. |
    | `ExternalLoginComponent` | `spiderly-external-login` | Fetches enabled providers, renders "or" separator + provider buttons, resolves icons via `iconFor()`, initiates the challenge redirect. Owns `@Input() providerIcons`. |
    | `SpiderlyLoginComponent` | `spiderly-login` | Page / route target (renamed from `LoginComponent`/`app-login`). Owns the email form + send-verification logic. Composes `<spiderly-auth-card>` + inline email form + `<spiderly-external-login>`. Exposes `@Input() providerIcons`, forwarded to `<spiderly-external-login>`. |

    **Consumer control levels:**

    | Level | How |
    |---|---|
    | 1 | Route directly to `SpiderlyLoginComponent` (`spiderly-login`). Built-in default icons, zero config. |
    | 1.5 | Thin app-owned wrapper renders `<spiderly-login [providerIcons]="…">` — overrides icons without touching lib internals. |
    | 2 | Compose a custom page from `<spiderly-auth-card>` + `<spiderly-external-login [providerIcons]>` + your own form/layout. |
    | 3 | Use `[auth-logo]` / `[auth-footer]` content slots on `<spiderly-auth-card>` directly (requires Level 2 — composing the card yourself). |

    **Slot caveat (important):** The `[auth-logo]` and `[auth-footer]` named slots live on `AuthCardComponent` and are available when you compose `<spiderly-auth-card>` directly (Level 2). The default `SpiderlyLoginComponent` page does **not** forward them — Angular has no native mechanism to re-project named content slots through an intermediate component. Logo/footer customization therefore requires composing `<spiderly-auth-card>` yourself.

## End-to-end flow

### Admin (Angular — server-side code flow, B2)

1. `ExternalLoginComponent` fetches `GET /api/security/GetExternalProviders` → renders a button per enabled provider (icon resolved from `providerIcons` or `DEFAULT_EXTERNAL_PROVIDER_ICONS`).
2. User clicks one → browser navigates to `GET /api/Security/ExternalLoginChallenge?provider=<code>&returnUrl=<url>&browserId=<id>`.
3. Backend redirects to the provider's authorization endpoint (state + nonce + PKCE stored in a Data-Protection-signed cookie).
4. Provider redirects to `GET /api/Security/ExternalLoginCallback` with the authorization code.
5. Backend exchanges the code for tokens (`client_secret` is server-side), validates the id_token via the generic OIDC validator, resolves/provisions the user via `ResolveExternalUser` (decision 7), issues an HttpOnly-cookie session.
6. Backend redirects to `returnUrl` (the admin frontend); the SPA picks up the session from the cookie.

### Storefront (Next.js — GIS id-token path, for Google only)

1. SPA fetches `GET /api/security/GetExternalProviders` → renders Google One-Tap / button via GIS.
2. GIS hands the browser an id_token directly.
3. SPA POSTs `ExternalProviderDTO { Provider, IdToken, BrowserId }` to `LoginExternalWithCookies`.
4. Backend: `registry[Provider].ValidateAsync(idToken)` → `ExternalIdentity` (validates signature/iss/aud/exp/nonce).
5. Resolve/provision per decision 7 (link by subject; auto-link/create on verified email).
6. Issue HttpOnly-cookie session.

## What gets deleted

- `Spiderly.Shared/Enums/ExternalProviderCodes.cs`
- `ExternalProviderOptions.UseGoogleAsExternalProvider` and `.GoogleClientId`
- `IUser.HasLoggedInWithGoogleAsExternalProvider` + the `ApplicationDbContext` `Ignore()` branch
- The `Google.Apis.Auth` package reference + `ValidateGoogleToken`
- `createFakeGoogleWrapper()` and the GIS-specific Google button
- `config.service.base.ts` `GoogleClientId` / `showGoogleAuth`
- `IconUrl` from `ExternalProviderConfig`, `ExternalProviderPublicInfo`, `ExternalProviderPublicDTO`, and `GetExternalProviders` projection — icon presentation moved entirely to the frontend
- The monolithic `AuthComponent` — replaced by `AuthCardComponent` + `ExternalLoginComponent` (see decision 12)

## Implementation plan (reviewable slices)

Sliced per the "break big tasks" preference — pause for review between slices.

**Slice 1 — Backend abstraction (no behavior change to data model yet).**
`IExternalAuthProvider` + `ExternalIdentity`; `GenericOidcExternalAuthProvider` (discovery + JWKS via `Microsoft.IdentityModel.*`); preset registry; new `ExternalProviders` config list + `ValidateOnStart`; keyed registry + DI registration; `ExternalProviderDTO` gains `Provider`. Delete `ExternalProviderCodes`, the flat options, and `Google.Apis.Auth`. Rewire `LoginExternal` to resolve via the registry. (Temporarily keep email-matching to isolate the change.)

**Slice 2 — Identity model + scaffolding.**
`IUserExternalLogin`; thread `TUserExternalLogin` through `SecurityServiceBase`/`SecurityBaseController`; implement login resolution by `(Provider, Subject)` with the decision-7 provisioning rules + `AutoLinkByVerifiedEmail`. Delete `HasLoggedInWithGoogleAsExternalProvider` from `IUser` + the DbContext `Ignore()`. Emit the scaffolded `UserExternalLogin` entity from `spiderly init` (`NetAndAngularFilesGenerator.cs`) and **update the e2e fixture** (init-template-drift hazard — see Spiderly `CLAUDE.md`). New `ApiErrorCodes` (`EmailNotVerified`, `ExternalProviderNotConfigured`) mirrored across the three contract files.

**Slice 3 — Config endpoint + frontend.**
`GET /api/security/external-providers` (returns `{ code, authority, clientId, label }` — no `IconUrl`). Replace `createFakeGoogleWrapper`/Google button with `ExternalLoginComponent` (config-driven button list from the providers endpoint); collapse `config.service.base.ts` flags into the fetched list. `AuthComponent` deleted and split into `AuthCardComponent` + `ExternalLoginComponent`. `LoginComponent`/`app-login` renamed to `SpiderlyLoginComponent`/`spiderly-login`. Provider icon moved to the frontend via `DEFAULT_EXTERNAL_PROVIDER_ICONS` + `providerIcons` input. See decision 12 for the full component design.

**Slice 4 — Consumer migration (PACMS) + docs.**
PACMS: scaffold `UserExternalLogin`, migration to drop the bool column (+ its `WithGoogleAsExternalProviderColumn` migration) and add the table (trivial — migrations get squashed pre-prod). PACMS admin routes `/login` to a thin Level 1.5 wrapper that passes `{ google: 'assets/icons/google.svg' }` to `<spiderly-login>`. Remove `IconUrl` from backend `appsettings.json`. Storefront login UI onto the providers endpoint. Update `spiderly-website` docs (required for public-API changes).
