---
name: api-keys
description: Add the API-keys feature to a Spiderly app — per-key authentication via an X-Api-Key header, where each key is a first-class principal carrying its own roles. Use when a project needs machine/partner/agent access to its REST API (generate, scope-to-roles, expire, revoke). Opt-in; not part of the default app.
---

# Add API Keys

This builds the API-keys feature into a Spiderly consumer app: external clients authenticate with an `X-Api-Key` header, and each key is a **first-class principal** (`PrincipalKinds.ApiKey`) that carries its **own roles** — it is not an impersonation of a user. Authorization resolves a key's permissions through the same principal pipeline as a user, so `[HasPermission]` endpoints accept either a JWT or a key with no per-endpoint change.

**This is opt-in.** API keys are not part of a default Spiderly app — nothing here exists until you run this skill, and an app without it has zero API-key surface. Don't add it unless the project actually needs machine/partner/agent API access.

## Why a key is a principal (read once)

An API key must be **individually revocable** and **separately expirable** — a stateless token (e.g. a long-lived JWT) can't be revoked without either rotating the signing key (logging out every user) or a per-request denylist. So a key is an opaque random secret, stored **hashed**, checked against a row on every request. The framework owns that mechanism (`Spiderly.Security`); this skill assembles the per-app pieces on top.

Modeling the key as its own `ISecurityPrincipal` (rather than "resolve to the owning user + cap permissions") means:
- The key's authority = the union of **its own** roles' permissions. A role-less key has **no** permissions.
- The owning `User` is just **management metadata** (who created/lists/revokes it), decoupled from authority.
- No runtime permission-cap machinery — authorization treats the key like any other principal.

## Step 0 — Prerequisites

1. **Spiderly version** — confirm the compiled mechanism is present. It must expose `Spiderly.Security.Authentication.IApiKeyAuthenticator`, the `AddApiKeys<TApiKey>()` sub-builder on `SpiderlySecurityBuilder`, and `PrincipalKinds.ApiKey`. If those don't resolve, the app is on too old a Spiderly version — upgrade first (see the `spiderly-upgrade` skill).
2. **The app must already use Spiderly auth** — its `Startup.cs` calls `spiderly.AddSecurity<User, UserExternalLogin, AuthorizationService>(…)`, which registers the human `User` principal and enables authentication. API keys add a *second* principal kind alongside it.

Decide with the user up front: **do they want an admin UI** to manage keys, or **API/agent-only** (drive generation via the REST endpoints / a management skill)? The backend is identical either way; the UI is the optional Step 8.

## Step 1 — The `ApiKey` entity (+ junction + navs)

Create the entity in the Business project's `Entities/`. Adapt the namespace and the `User`/`Role` types to the app.

```csharp
[Index(nameof(KeyHash), IsUnique = true)]
[SpiderlyEntity]
public class ApiKey : BusinessObject<long>, IApiKey
{
    [UIDoNotGenerate]
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string KeyHash { get; set; }            // SHA-256 of the key; the plaintext is never stored

    [Required]
    [StringLength(100, MinimumLength = 1)]
    [DisplayName]
    public string Name { get; set; }

    // Creator — the principal (any kind) that minted this key. Audit metadata, auto-stamped at creation.
    // A soft reference (no FK): the creator can be any principal kind (User, ApiKey, ...), so it's stored
    // as principal id + kind rather than a typed navigation.
    [UIDoNotGenerate]
    public long CreatedById { get; set; }

    [UIDoNotGenerate]
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string CreatedByPrincipalKind { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool? IsRevoked { get; set; }

    // ISecurityPrincipal: the key's authority is the union of its own roles' permissions (M2M, like User.Roles).
    public virtual List<Role> Roles { get; } = new(); // M2M
    IReadOnlyCollection<IRole> ISecurityPrincipal.Roles => Roles;

    public bool? IsDisabled { get; set; }
}
```

`IApiKey` (which extends `ISecurityPrincipal`) is `Spiderly.Security.Interfaces`; `IRole` is `Spiderly.Shared.Interfaces`. The entity implements `IApiKey` so the framework's default authenticator (Step 2) reads it generically.

Add the M2M junction (mirrors the app's `UserRole`):

```csharp
[M2M]
[SpiderlyEntity]
public class ApiKeyRole
{
    [M2MWithMany(nameof(ApiKey.Roles))]
    public virtual ApiKey ApiKey { get; set; }

    [M2MWithMany(nameof(Role.ApiKeys))]
    public virtual Role Role { get; set; }
}
```

Add the inverse M2M nav `public virtual List<ApiKey> ApiKeys { get; } = new(); // M2M` on **`Role`**. There is **no** nav on `User` — the creator is a soft reference (id + kind), not a navigation.

## Step 2 — The authenticator (you don't write one)

The framework ships `DefaultApiKeyAuthenticator<TApiKey>`, which does the hash→active-id lookup over your `ApiKey` entity generically (it reads it via the `IApiKey` interface, so it stays decoupled from your namespace). You get it for free from the Step 4 registration — there is **no authenticator to hand-write**.

Only implement your own `IApiKeyAuthenticator` for a non-standard lookup (e.g. an external key store). If you do, register it **before** the `AddApiKeys<ApiKey>()` call (Step 4) — the framework adds the default with `TryAdd`, so your registration wins.

## Step 3 — `ApiKeyService` hooks (generate + hash + show-once)

Extend the generated `ApiKeyServiceGenerated` so the admin/CRUD save path mints the key, stores only its hash, and returns the plaintext **exactly once**:

```csharp
[SpiderlyService]
public class ApiKeyService : ApiKeyServiceGenerated
{
    private readonly AuthenticationService _authenticationService;
    private readonly AuthorizationService _authorizationService;   // the app's most-derived authorization service
    private string _generatedPlainTextKey;

    public ApiKeyService(EntityServiceDependencies deps, AuthenticationService authenticationService, AuthorizationService authorizationService) : base(deps)
    {
        _authenticationService = authenticationService;
        _authorizationService = authorizationService;
    }

    protected override async Task OnBeforeSaveApiKeyAndReturnMainUIFormDTO(ApiKeySaveBodyDTO saveBodyDTO)
    {
        if (saveBodyDTO.ApiKeyDTO.Id <= 0)
        {
            string plainTextKey = ApiKeyHelper.GenerateRandomKey();
            saveBodyDTO.ApiKeyDTO.KeyHash = ApiKeyHelper.ComputeSha256Hash(plainTextKey);
            _generatedPlainTextKey = plainTextKey;
            saveBodyDTO.ApiKeyDTO.CreatedById = _authenticationService.GetCurrentUserId();
            saveBodyDTO.ApiKeyDTO.CreatedByPrincipalKind = _authenticationService.GetCurrentPrincipalKind();
        }
        else
        {
            saveBodyDTO.ApiKeyDTO.KeyHash = await _deps.Context.DbSet<ApiKey>()
                .AsNoTracking().Where(x => x.Id == saveBodyDTO.ApiKeyDTO.Id).Select(x => x.KeyHash).FirstAsync();
        }
    }

    protected override async Task OnAfterSaveApiKeyAndReturnMainUIFormDTO(ApiKeySaveBodyDTO saveBodyDTO, ApiKeyMainUIFormDTO mainUIFormDTO)
    {
        if (_generatedPlainTextKey != null)
        {
            mainUIFormDTO.PlainTextKey = _generatedPlainTextKey;   // add this property via a partial ApiKeyMainUIFormDTO (Step 6)
            _generatedPlainTextKey = null;
        }
    }

    // Guard EVERY role-assignment path, not just the Generate endpoint: an admin may only grant a key roles
    // whose permissions they themselves hold. This is the M2M role-mutation method, so guarding it here covers
    // the admin roles editor (and any future caller) by construction.
    public override async Task UpdateRolesForApiKey(long id, List<int> selectedIds)
    {
        await _authorizationService.AuthorizeApiKeyRoleAssignmentAsync(selectedIds);
        await base.UpdateRolesForApiKey(id, selectedIds);
    }
}
```

`ApiKeyHelper` is `Spiderly.Security.Authentication`. If the app's authorization service has a different class name, inject that.

## Step 4 — Register the key (API-keys sub-builder)

Pass an `s => s.AddApiKeys<ApiKey>()` configurator to the existing `spiderly.AddSecurity<…>(…)` call in `Startup.cs`:

```csharp
spiderly.AddSecurity<User, UserExternalLogin, AuthorizationService>(s => s.AddApiKeys<ApiKey>());
```

`AddApiKeys<ApiKey>()` registers the `ApiKey` principal kind **and** the authentication scheme in one call — the `ApiKey` scheme plus a forwarding policy scheme set as the default, so `X-Api-Key` requests route to the key handler and everything else to JWT, with no per-endpoint `[Authorize(AuthenticationSchemes=...)]` churn. Registering a second principal kind makes the app multi-principal; the JWT login already stamps `PrincipalKinds.User`, the key handler stamps `PrincipalKinds.ApiKey`, so both satisfy the now-required `principal_kind` claim.

## Step 5 — The issuance guard (authorization service)

In the app's authorization service (the `[SpiderlyService]` extending `AuthorizationServiceGenerated`), add the guard the `UpdateRolesForApiKey` override and the Generate endpoint both call:

```csharp
/// <summary>
/// Issuance guard: the principal creating a key may only grant it roles whose permissions that principal
/// already holds, so a key can never be minted more powerful than its creator. The creator can be any
/// principal kind (a user, a service account, another key), so the ceiling is resolved principal-agnostically
/// via GetCurrentPrincipalPermissionCodesAsync() (not the User-typed GetCurrentUserPermissionCodes). No
/// runtime cap — a key authorizes through its own roles — so this is enforced once, at assignment.
/// </summary>
public async Task AuthorizeApiKeyRoleAssignmentAsync(List<int> roleIds)
{
    if (roleIds == null || roleIds.Count == 0)
        return;

    HashSet<string> creatorPermissions = (await GetCurrentPrincipalPermissionCodesAsync()).ToHashSet();

    List<string> targetRolePermissions = await _context.DbSet<Role>()
        .AsNoTracking()
        .Where(r => roleIds.Contains(r.Id))
        .SelectMany(r => r.Permissions)
        .Select(p => p.Code)
        .Distinct()
        .ToListAsync();

    if (!targetRolePermissions.All(p => creatorPermissions.Contains(p)))
        throw new BusinessException(_localizer["ApiKeyRoleExceedsYourPermissionsException"]);
}
```

Add the `ApiKeyRoleExceedsYourPermissionsException` translation key (see the `backend-localization` doc).

## Step 6 — Generate / Revoke endpoints + DTOs

These are the machine-facing REST surface (also what a management skill drives). Add to a `[SpiderlyController]` (e.g. the security controller). DTOs go in the Admin DTO folder; the `PlainTextKey` extension is a **partial** of the generated `ApiKeyMainUIFormDTO` and must use the bare `...Business.DTO` namespace (see the `entity-design` doc on partial-DTO namespacing).

```csharp
// GenerateApiKeyRequestDTO:  string Name; int? ExpiresInDays; [Required] List<int> RoleIds = new();
// GenerateApiKeyResponseDTO: string Key; string Name; DateTime? ExpiresAt; [Required] List<int> RoleIds = new(); [Required] List<string> RoleNames = new();
// RevokeApiKeyRequestDTO:    [Required] long ApiKeyId;
// partial ApiKeyMainUIFormDTO (bare DTO namespace): [UIDoNotGenerate][StringLength(64)] string PlainTextKey;

[HttpPost, AuthGuard]
public async Task<GenerateApiKeyResponseDTO> GenerateApiKey([FromBody] GenerateApiKeyRequestDTO request)
{
    await _authorizationService.AuthorizeAndThrowAsync(PermissionCodes.InsertApiKey);   // principal-agnostic — any principal kind can create a key
    long currentUserId = _authenticationService.GetCurrentUserId();

    List<Role> roles = new();
    if (request.RoleIds != null && request.RoleIds.Count > 0)
    {
        roles = await _context.DbSet<Role>().Where(x => request.RoleIds.Contains(x.Id)).ToListAsync();
        List<int> missing = request.RoleIds.Except(roles.Select(r => r.Id)).ToList();
        if (missing.Count > 0) throw new BusinessException($"Role(s) not found: {string.Join(", ", missing)}.");
        await _authorizationService.AuthorizeApiKeyRoleAssignmentAsync(request.RoleIds);
    }

    string plainTextKey = ApiKeyHelper.GenerateRandomKey();
    DateTime? expiresAt = request.ExpiresInDays.HasValue ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value) : null;

    ApiKey apiKey = new() { KeyHash = ApiKeyHelper.ComputeSha256Hash(plainTextKey), Name = request.Name,
        CreatedById = currentUserId, CreatedByPrincipalKind = _authenticationService.GetCurrentPrincipalKind(),
        ExpiresAt = expiresAt, IsRevoked = false };
    apiKey.Roles.AddRange(roles);
    _context.DbSet<ApiKey>().Add(apiKey);
    await _context.SaveChangesAsync();

    return new GenerateApiKeyResponseDTO { Key = plainTextKey, Name = request.Name, ExpiresAt = expiresAt,
        RoleIds = roles.Select(r => r.Id).ToList(), RoleNames = roles.Select(r => r.Name).ToList() };
}

[HttpPost, AuthGuard]
public async Task RevokeApiKey([FromBody] RevokeApiKeyRequestDTO request)
{
    await _authorizationService.AuthorizeAndThrowAsync(PermissionCodes.UpdateApiKey);
    ApiKey apiKey = await _context.DbSet<ApiKey>().FirstOrDefaultAsync(x => x.Id == request.ApiKeyId)
        ?? throw new BusinessException("API key not found.");
    apiKey.IsRevoked = true;
    await _context.SaveChangesAsync();
}
```

## Step 7 — Seed the `ApiKey` permissions + migrate

1. **Seed permissions** so the `PermissionCodes.{Read,Insert,Update,Delete}ApiKey` constants generate — add `Permission` seed rows (codes `ReadApiKey`/`InsertApiKey`/`UpdateApiKey`/`DeleteApiKey`) in the app's DbContext seed data, alongside the existing User/Role permission seeds, and grant them to the admin role.
2. **Migration** — add + apply an EF migration for the new `ApiKey` + `ApiKeyRole` tables (see the `ef-migrations` skill). If you're converting an *existing* single-`Role` API-key table to this M2M model, make the migration **data-preserving**: create the junction first, `INSERT ... SELECT` the old `RoleId`s into it, then drop the old column.

## Step 8 — (Optional) admin UI

Only if the user wants a clickable admin surface (otherwise skip — keys are managed via the endpoints / a management skill). Scaffold the list + details pages for the `ApiKey` entity (see the `add-entity` skill for pages/routes/menu), then customize the details page to show the generated key **once**: intercept the save response, and if it carries `plainTextKey`, show it in a read-only "copy this — it won't be shown again" dialog and suppress the auto-reroute until the dialog closes. A roles editor on the form routes through `UpdateRolesForApiKey`, which Step 3 already guards.

To keep the feature **headless** instead, add `[UIDoNotGenerate]` to the `ApiKey` class and don't create pages.

## Step 9 — Verify

Build the backend (`dotnet build` the Business project, then the WebAPI project). Then sanity-check the flow: generate a key (admin JWT) → call any `[HasPermission]` endpoint the key's roles cover with `X-Api-Key: <key>` → revoke it → confirm the same call now returns 401.

## Invariants to preserve

- **Store only the hash.** The plaintext is returned once at generation and never persisted.
- **Guard every role-assignment path.** Both the Generate endpoint and `UpdateRolesForApiKey` call `AuthorizeApiKeyRoleAssignmentAsync` — a key can never be minted more powerful than its creator.
- **A role-less key has no permissions** (deny), by design. Assign an admin role for a full-access key.
- **Revocation/expiry/disabled are checked live** in `DefaultApiKeyAuthenticator` on every request — that's the whole point of opaque keys over JWTs.
