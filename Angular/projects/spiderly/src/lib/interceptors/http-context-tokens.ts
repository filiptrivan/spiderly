import { HttpContextToken } from '@angular/common/http';

// The marks a request carries for the library's interceptors. They live apart from the
// interceptors that read them because AuthServiceBase and ApiSecurityService set them, and the
// interceptors import AuthServiceBase: one file each would be an import cycle.

/**
 * Marks a request that itself signs in, sends the verification email, refreshes or signs out, so
 * `jwtInterceptor` never makes it wait on a refresh (the refresh would wait on itself).
 * `ApiSecurityService` sets it on those requests; a consuming app sets it on an auth request of
 * its own. The mark is on the request, never a list of URLs, so renaming an endpoint cannot lose it.
 */
export const SKIP_REFRESH_CHECK = new HttpContextToken<boolean>(() => false);

/**
 * Marks a request whose failure its caller handles: `unauthorizedInterceptor` logs it and does
 * nothing else, no message and no sign-out. `AuthServiceBase` sets it on the refresh a request
 * waits on, whose outcome signs out on a 4xx, so an outage shows once, on the request behind it.
 */
export const SKIP_ERROR_MESSAGE = new HttpContextToken<boolean>(() => false);
