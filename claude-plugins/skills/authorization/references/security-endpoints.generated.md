<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# SecurityBaseController endpoints

A base controller providing core security functionalities such as authentication, user management, and role-based access control. It leverages various services for handling user authentication (login, registration, logout, token refresh). This controller is designed to be extended for specific user types.

| Endpoint | Method | Auth | Description |
| --- | --- | --- | --- |
| `ExternalLoginCallback` | GET | No | Server-side external-login step 2: the provider redirects here with the code. Exchanges it for the id token (server-side), validates + links the user, issues the session as HttpOnly cookies, and redirects back to the originating app (returnUrl). This is a top-level browser navigation, so failures must redirect back to the app (never render a JSON error onto the API origin). The app surfaces a friendly message via the externalAuthError hint: expired (the state cookie lapsed — the user lingered on the provider's picker) or failed (invalid state/nonce, failed code exchange, or denied consent). |
| `ExternalLoginChallenge` | GET | No | Server-side external-login step 1: redirects the browser to the provider's authorize endpoint. The state/nonce/PKCE-verifier are stored in a short-lived, Data-Protection-signed HttpOnly cookie. |
| `GetCurrentUserBase` | GET | Yes | Returns the authenticated user's base profile (id, email, core fields). Requires a valid access token. |
| `GetCurrentUserPermissionCodes` | GET | Yes | Returns the permission codes granted to the authenticated user via their roles. Requires a valid access token. |
| `GetExternalLoginNonce` | GET | No | Issues a one-time nonce for the client-side (GIS / id-token) external-login flow: returns the raw nonce for the SPA to pass to the provider's sign-in call (so it is echoed into the id token), and stores a signed copy in a short-lived HttpOnly cookie that LoginExternal / LoginExternalWithCookies verify the returned id token against. Anonymous. SameSite=None so the cookie rides the cross-site login POST. |
| `GetExternalProviders` | GET | No | Public list of enabled external providers (code + OIDC authority + client id + button display), so the frontend can render sign-in buttons and run the client OIDC flow. Anonymous — the values are public by OIDC design. |
| `Login` | POST | No | Passwordless login step 2: verifies the emailed code and, on success, returns the access + refresh tokens in the response body. Anonymous. |
| `LoginExternal` | POST | No | Client-side external (OIDC) login: validates the provider id token against the single-use nonce cookie and returns the access + refresh tokens in the response body. Anonymous. |
| `LoginExternalWithCookies` | POST | No | Like LoginExternal, but issues the session as HttpOnly cookies instead of returning the tokens in the response body. Anonymous. |
| `LoginWithCookies` | POST | No | Like Login, but issues the session as HttpOnly cookies instead of returning the tokens in the response body. Anonymous. |
| `Logout` | GET | Yes | Invalidates the current user's refresh token for the given browser session. Requires a valid access token. |
| `LogoutWithCookies` | GET | Yes | Like Logout, and additionally clears the auth HttpOnly cookies. Requires a valid access token. |
| `RefreshTokenWithCookies` | POST | No | Refreshes the access token using the refresh token stored in an HttpOnly cookie. POST (not GET) because it mutates server state — it rotates the single-use refresh token. A safe/idempotent GET was cacheable, so browsers replayed a stale "logged-in" body on back/forward navigations (phantom dashboard after logout); POST is never cached, and the controller-level no-store is belt-and-braces. |
| `RefreshTokenWithHeaders` | POST | No | Refreshes the access token using the refresh token supplied in the request body, returning a new access + refresh token pair. Anonymous — the refresh token is itself the credential. |
| `SendLoginVerificationEmail` | POST | No | Passwordless login step 1: sends a short-lived numeric verification code to the user's email. Anonymous. |
