# API key authentication — Design

> Status: **PROPOSED.** Models an API key as a first-class `ISecurityPrincipal` (a new `PrincipalKinds.ApiKey`) rather than a credential that resolves to a `User` with a capped role. This doc records *why* the principal model is the idiomatic fit, the compiled-framework vs. init-template split, and the behavior changes consumers must accept.
> Reference implementation being extracted from: PACMS (`prodavnicaalata.rs`), which built API keys the non-principal way (middleware + an `AuthorizationService` role-cap via `HttpContext.Items`) before the principal model existed.

## Problem

An external client (a partner integration, a CI bot, an AI agent that manages store data) needs to authenticate to a Spiderly app's REST API without going through the interactive email-code login. The standard mechanism is a long-lived secret presented in an `X-Api-Key` header, mapped to a set of permissions.

The naive implementation — the one PACMS shipped — treats the key as a way to **impersonate a user**: look the key up, resolve its owning `User`, set that user as the principal, and optionally *cap* the user's permissions to a role attached to the key. That works, but it fights the framework:

- It needs a parallel "role cap" path (`IsAllowedByApiKeyRole`) bolted onto every `AuthorizationService` authorize method, fed by a stringly-typed `HttpContext.Items["ApiKeyPermissionCodes"]` side channel set in a bespoke middleware that runs *before* `UseAuthentication`.
- It conflates the key's actions with the owning user's actions (audit, attribution).
- A role-less key silently inherits the *user's full powers* — an easy way to mint an over-privileged key by accident.

## The decision

**An API key is its own principal.** Spiderly's authorization core is already principal-kind-agnostic and was explicitly built for this:

- `ISecurityPrincipal`'s own contract names *"a human `IUser`, a machine/service account, an AI agent"* as intended kinds. It carries `Roles` + `IsDisabled` — nothing user-specific.
- `IsAuthorizedAsync(permissionCode)` resolves the current principal **kind** (`GetCurrentPrincipalKind()`), looks up the per-kind `IPrincipalPermissionResolver` from `IPrincipalRegistry`, and asks it. It does not care whether the caller is a `User` or a key.
- `RolePermissionResolver<TPrincipal>` already resolves *any* principal's `Roles → Permissions` generically. Adding a kind is **one** `AddSpiderlyPrincipal<ApiKey>(PrincipalKinds.ApiKey)` call with no new query code.
- `BusinessObject` has **no `CreatedBy`/`ModifiedBy` user FK** — only timestamps — so a non-`User` principal saving entities writes nothing that assumes a user row. No audit landmine.

So the key becomes `ApiKey : ISecurityPrincipal`, the auth handler stamps `principal_kind = ApiKey` + the key's id as the subject, and **the entire existing authorization stack resolves it with zero special cases.** The role-cap path, the `HttpContext.Items` side channel, and the `AuthorizationService` overrides all *disappear*.

## Architecture — two layers

Spiderly has two "built-in" mechanisms; the feature splits across both.

### Layer 1 — compiled framework (`Spiderly.Security`, shipped as NuGet)

App-agnostic plumbing, written once. **`Spiderly.Shared` cannot reference `Spiderly.Security`**, so registration lives in a Security-side extension the template calls — mirroring `AddSpiderlyPrincipal<T>` and `AddSpiderlyAuthorization<T>`, *not* a flag inside `AddSpiderly`.

| Piece | Responsibility |
| --- | --- |
| `PrincipalKinds.ApiKey` | The well-known kind value stamped on the principal. |
| `ApiKeyHelper` | Key generation (256-bit, hex) + SHA-256 hashing. The framework owns the algorithm so generation and verification can't drift. |
| `IApiKeyAuthenticator` | The one seam that must touch the consumer-visible `ApiKey` entity: `Task<long?> ResolveActiveApiKeyIdAsync(string keyHash)` — returns the key's id iff it is active (not revoked, not expired, not disabled), else `null`. Validity lives with the entity, on the consumer side. |
| `ApiKeyAuthenticationDefaults` / `Options` / `Handler` | A real ASP.NET `AuthenticationHandler` for the `ApiKey` scheme. Reads `X-Api-Key`, hashes it, calls the authenticator, and on success issues a ticket carrying `NameIdentifier = apiKeyId` + `principal_kind = ApiKey`. No permission lookup here — permissions resolve later through the registry. |
| `AddSpiderlyApiKeyAuthentication<TAuthenticator>()` | Registers the authenticator, the `ApiKey` scheme, and a **forwarding policy scheme** set as the default authenticate/challenge scheme: it forwards to `ApiKey` when the `X-Api-Key` header is present, else to the JWT `Bearer` scheme. So existing `[Authorize]`/`[AuthGuard]` endpoints accept either credential with no per-endpoint change. |

Why a real auth scheme instead of PACMS's middleware: it composes with `UseAuthentication()`, is independently testable, and removes the "runs before auth, sets `context.User` by hand" fragility.

### Layer 2 — opt-in scaffolding (added on demand, NOT baked into every `init`)

Entity-shaped pieces, emitted as source so the generators produce CRUD + admin UI. Most Spiderly apps don't need API keys, so this is **not** part of the default `spiderly init` scaffold — forcing an opinionated security surface into every new app is bloat you'd delete from half of them. It's added on demand (a CLI step or a documented recipe) by the apps that want it, and once added it's ordinary scaffolded code the app owns and can reshape:

- **`ApiKey` entity** implementing `ISecurityPrincipal`: `KeyHash` (unique), `Name` (`[DisplayName]`), `Roles` (M2M, via an `ApiKeyRole` junction, mirroring `User.Roles`), `IsDisabled`, `ExpiresAt`, `IsRevoked`, and an **owner `User`** FK that is pure management metadata (who created/lists/revokes it) — decoupled from authority. `ApiKeys` nav added to `Role` and `User`.
- **`IApiKeyAuthenticator` implementation** — the small service that queries `DbSet<ApiKey>()` for an active key by hash.
- **`ApiKeyService`** override — generate the plaintext key on insert, store only its hash, return the plaintext exactly once.
- **Generate / Revoke endpoints + DTOs**, including the issuance guard: an admin may only grant a key roles whose permissions the admin already holds (the "can't mint a key stronger than yourself" rule moves entirely to issuance — see below).
- **Angular admin** list + details pages + the "copy this key, it won't be shown again" modal.
- **Seed `ApiKey` permission codes** (Read/Insert/Update/Delete).
- **Wiring**: `AddSpiderlyPrincipal<ApiKey>(PrincipalKinds.ApiKey)` next to the existing `User` registration, plus `AddSpiderlyApiKeyAuthentication<ApiKeyAuthenticator>()`.

## Behavior changes to accept (vs. the resolve-to-User model)

1. **No runtime "cap to issuer."** The key carries its *own* roles; its authority no longer shrinks if the issuing user's roles later shrink. The "can't mint a key stronger than yourself" guarantee stays, but moves entirely to **issuance time**. For a machine identity this independent lifecycle is correct — revoke the key to kill it.
2. **Role-less key = no permissions** (deny), instead of "act as the user with the user's full powers." Safer and explicit; assign a role (e.g. an admin role) for a full-access key.
3. **`ApiKey.User` is ownership only**, not authority. The key's roles are M2M, like a user's.
4. **The key is a distinct identity.** Any endpoint meant to be API-key-callable must not assume the current principal is a `User` row (nothing that takes `GetCurrentUserId()` and loads a `User` / writes a user FK with it). Key-callable surface in practice is permission-gated admin/data endpoints, not user-identity endpoints like "my cart" — but audit before exposing.

## Multi-principal consequence

Registering a second kind makes the app formally multi-principal, so the `principal_kind` claim becomes mandatory (the registry has no single default to fall back to). The framework's email-login flow already stamps `PrincipalKinds.User` on the tokens it issues (`JwtAuthManagerService`), and the API-key handler stamps `PrincipalKinds.ApiKey`, so both paths satisfy the requirement.
