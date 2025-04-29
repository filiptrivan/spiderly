
# Spiderly.Security
Spiderly.Security package provides authentication and authorization features using JWT tokens.

## UI
When used in combination with our [Angular](https://github.com/filiptrivan/spiderly/tree/main/Angular) library, you can achieve a UI like this:
<div>
  <img src="https://github.com/filiptrivan/spiderly/blob/main/spiderly-login-demo.png" alt="Spiderly Login Demo UI"/>
</div>

## Customization

### Controller
If you want to override some of the Security library controller's behavior, you can do so in your controller (e.g., `SecurityController`), which extends our `SecurityBaseController`, like this:
```csharp
[HttpPost]
public override async Task<AuthResultDTO> Login(VerificationTokenRequestDTO request)
{
    // Your custom code...
    return _securityBusinessService.Login(request);
}
```

### Authorization
If you want to override some of the Security library authorization's behavior, you can do so in your authorization business service (e.g. `AuthorizationBusinessService`), which extends our `AuthorizationBusinessServiceGenerated`, like this:
```csharp
public override async Task AuthorizeUserExtendedReadAndThrow(long? userExtendedId)
{
    await _context.WithTransactionAsync(async () =>
    {
        bool hasAdminReadPermission = await IsAuthorizedAsync<UserExtended>(BusinessPermissionCodes.ReadUserExtended);
        bool isCurrentUser = _authenticationService.GetCurrentUserId() == userExtendedId;

        if (isCurrentUser == false && hasAdminReadPermission == false)
            throw new UnauthorizedException();
    });
}
```