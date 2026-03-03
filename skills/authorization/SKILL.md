---
name: authorization
description: Set up permission-based authorization in Spiderly. Use when implementing user/role/permission entities, seeding permissions, using DoNotAuthorize or AuthGuard attributes, checking permissions in custom code, configuring frontend auth guards, or setting up Google OAuth.
---

# Authorization

## Entity Interfaces

Implement these on your User, Role, and Permission entities:

### IUser

```csharp
public interface IUser : IBusinessObject<long>
{
    string Email { get; set; }
    bool? HasLoggedInWithGoogleAsExternalProvider { get; set; }
    bool? IsDisabled { get; set; }
    IReadOnlyCollection<IRole> Roles { get; }
}
```

### IRole

```csharp
public interface IRole : IBusinessObject<int>
{
    string Name { get; set; }
    string Description { get; set; }
    IReadOnlyCollection<IUser> Users { get; }
    IReadOnlyCollection<IPermission> Permissions { get; }
}
```

### IPermission

```csharp
public interface IPermission : IReadonlyObject<int>
{
    string Name { get; set; }
    string Description { get; set; }
    string Code { get; set; }
    IReadOnlyCollection<IRole> Roles { get; }
}
```

### Real-World Entity Example

```csharp
[Index(nameof(Email), IsUnique = true)]
public class User : BusinessObject<long>, IUser
{
    [Required]
    [StringLength(70, MinimumLength = 5)]
    [CustomValidator("EmailAddress()")]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }

    public bool? HasLoggedInWithGoogleAsExternalProvider { get; set; }
    public bool? IsDisabled { get; set; }

    public virtual List<Role> Roles { get; } = new();
    IReadOnlyCollection<IRole> IUser.Roles => Roles;
}

public class Role : BusinessObject<int>, IRole
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; }

    [StringLength(400, MinimumLength = 1)]
    public string Description { get; set; }

    [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
    public virtual List<User> Users { get; } = new();
    IReadOnlyCollection<IUser> IRole.Users => Users;

    [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
    public virtual List<Permission> Permissions { get; } = new();
    IReadOnlyCollection<IPermission> IRole.Permissions => Permissions;
}

[UIDoNotGenerate]
[Index(nameof(Code), IsUnique = true)]
public class Permission : ReadonlyObject<int>, IPermission
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; }

    [StringLength(400, MinimumLength = 1)]
    public string Description { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Code { get; set; }

    public virtual List<Role> Roles { get; } = new();
    IReadOnlyCollection<IRole> IPermission.Roles => Roles;
}
```

## Permission Code Convention

Auto-generated per entity (via `PermissionCodesGenerator`):

| Code | Purpose |
|---|---|
| `Read{Entity}` | View list/details |
| `Update{Entity}` | Modify existing |
| `Insert{Entity}` | Create new |
| `Delete{Entity}` | Remove |

Generated as a partial class:

```csharp
public static partial class PermissionCodes
{
    public static string ReadProduct { get; } = "ReadProduct";
    public static string UpdateProduct { get; } = "UpdateProduct";
    public static string InsertProduct { get; } = "InsertProduct";
    public static string DeleteProduct { get; } = "DeleteProduct";
    // ... one set per entity
}
```

Extend with custom codes:

```csharp
public static partial class PermissionCodes
{
    public static string ExportReports { get; } = "ExportReports";
}
```

## Seeding Permissions

In `ApplicationDbContext.SeedData()`:

```csharp
private static void SeedData(ModelBuilder modelBuilder)
{
    DateTime seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    Permission[] permissions =
    [
        new Permission { Id = 1, Name = "View users", Code = "ReadUser" },
        new Permission { Id = 2, Name = "Edit users", Code = "UpdateUser" },
        new Permission { Id = 3, Name = "Add users", Code = "InsertUser" },
        new Permission { Id = 4, Name = "Delete users", Code = "DeleteUser" },
        new Permission { Id = 5, Name = "View products", Code = "ReadProduct" },
        // ... more permissions with sequential IDs
    ];

    modelBuilder.Entity<Permission>().HasData(permissions);

    modelBuilder.Entity<Role>().HasData(new Role
    {
        Id = 1,
        Name = "Admin",
        CreatedAt = seedDate,
        ModifiedAt = seedDate,
    });

    modelBuilder.Entity<Role>()
        .HasMany(r => r.Permissions)
        .WithMany(p => p.Roles)
        .UsingEntity(j => j.HasData(
            permissions.Select((p, i) => new { RoleId = 1, PermissionId = p.Id }).ToArray()
        ));
}
```

After adding new permissions: `spiderly add-migration AddNewPermissions` → `spiderly update-database`.

## Attributes

### `[DoNotAuthorize]`

Skip all permission checks for an entity:

```csharp
[DoNotAuthorize]
public class PaymentMethod : ReadonlyObject<byte> { ... }
```

Use for public lookup tables. Generated CRUD endpoints won't require login.

### `[AuthGuard]`

Require valid JWT on a controller action:

```csharp
[HttpGet]
[AuthGuard]
public async Task<UserBaseDTO> GetProfile() { ... }
```

Validates JWT from `Authorization: Bearer {token}` header. Returns 401 if invalid. Applied automatically on all generated CRUD endpoints (unless entity has `[DoNotAuthorize]`).

## Generated Authorization Service

`AuthorizationServicesGenerator` creates per-entity authorization methods:

```csharp
// Generated — override in your AuthorizationService to customize
public virtual async Task AuthorizeProductReadAndThrow(long? productIdToRead)
{
    await AuthorizeAndThrowAsync<User>(PermissionCodes.ReadProduct);
}

public virtual async Task AuthorizeProductUpdateAndThrow(ProductDTO dto)
{
    await AuthorizeAndThrowAsync<User>(PermissionCodes.UpdateProduct);
}

public virtual async Task AuthorizeProductInsertAndThrow(ProductDTO dto)
{
    await AuthorizeAndThrowAsync<User>(PermissionCodes.InsertProduct);
}

public virtual async Task AuthorizeProductDeleteAndThrow(long id)
{
    await AuthorizeAndThrowAsync<User>(PermissionCodes.DeleteProduct);
}
```

## Checking Permissions in Custom Code

```csharp
// In BusinessService or custom service:
await _authorizationService.AuthorizeAndThrowAsync<User>(PermissionCodes.ExportReports);

// Check without throwing
bool canExport = await _authorizationService.IsAuthorizedAsync<User>(PermissionCodes.ExportReports);
```

## Authentication Flow

Spiderly uses **email-based login** (no passwords):

```
1. Client sends email → SendLoginVerificationEmail
2. Server sends 6-digit code via email (or shows in dev mode)
3. Client sends code → Login
4. Server returns access token (JWT, 20 min) + refresh token (24h)
5. Auto-refresh 5 seconds before expiration
```

### SecurityServiceBase Hooks

```csharp
public class SecurityService : SecurityServiceBase<User>
{
    public override async Task OnAfterLogin(AuthResultDTO authResultDTO)
    {
        // Custom post-login logic (analytics, logging, etc.)
    }
}
```

### SecurityBaseController Endpoints

| Endpoint | Method | Auth | Purpose |
|---|---|---|---|
| `SendLoginVerificationEmail` | POST | No | Send 6-digit code |
| `Login` | POST | No | Verify code, get tokens |
| `LoginExternal` | POST | No | Google OAuth login |
| `RefreshTokenWithHeaders` | POST | No | Refresh access token |
| `Logout` | GET | Yes | Invalidate refresh token |
| `GetCurrentUserBase` | GET | Yes | Get current user info |
| `GetCurrentUserPermissionCodes` | GET | Yes | Get permission code list |

## Google OAuth Setup

1. Get a Google Client ID from Google Developer Console
2. Set in `Backend/appsettings.json`:
   ```json
   { "AppSettings": { "Spiderly.Shared": { "GoogleClientId": "..." } } }
   ```
3. Set in `Frontend/src/environments/environment.ts`:
   ```typescript
   GoogleClientId: '...'
   ```
4. Enable in config service:
   ```typescript
   override showGoogleAuth = true;
   ```

Flow: Google returns JWT → `LoginExternal` validates → auto-creates user if new → returns tokens.

## Frontend Auth

### AuthServiceBase

Key observables:

```typescript
user$: Observable<UserBase | null>                    // Current user
currentUserPermissionCodes$: Observable<string[]>     // Permission codes
```

Key methods:

```typescript
login(body: VerificationTokenRequest): Observable<Promise<AuthResult>>
loginExternal(body: ExternalProvider): Observable<Promise<AuthResult>>
logout()
refreshToken(): Observable<AuthResult>
```

Overridable hooks:

```typescript
onAfterLoginExternal = () => { ... }
onAfterLogout = () => { ... }
onAfterRefreshToken = () => { ... }
```

### Route Guards

```typescript
// Protect authenticated routes
{ path: 'dashboard', component: DashboardComponent, canActivate: [AuthGuard] }

// Protect login page from logged-in users
{ path: 'login', component: LoginComponent, canActivate: [NotAuthGuard] }
```

### Permission-Based Menu Visibility

```typescript
menu: SpiderlyMenuItem[] = [
  {
    label: 'Users',
    routerLink: ['/user-list'],
    hasPermission: (codes) => codes.includes('ReadUser'),
  },
];
```

### Multi-Tab Sync

Login/logout events sync across browser tabs via `localStorage` events. `getBrowserId()` generates a UUID per browser — server limits to 5 concurrent sessions per user.

## Settings Reference

```csharp
AccessTokenExpiration = 20          // minutes
RefreshTokenExpiration = 1440       // minutes (24h)
VerificationTokenExpiration = 5     // minutes
AllowedBrowsersForTheSingleUser = 5
OnlyAdminCanAddUsers = false        // true = block self-registration
AllowTheUseOfAppWithDifferentIpAddresses = true
```
