import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { switchMap } from 'rxjs/operators';
import { AuthServiceBase } from '../services/auth.service.base';
import { ConfigServiceBase } from '../services/config.service.base';
import { SKIP_REFRESH_CHECK } from './http-context-tokens';

// Cookie-based auth: the session JWTs are HttpOnly cookies, so we just send credentials on API calls.
// The browser attaches/refreshes the cookies; JS never holds the tokens (XSS-safe).
//
// CSRF: state-changing requests (POST/PUT/DELETE/PATCH) authenticated via cookie must include the
// X-CSRF header, otherwise Spiderly.Shared/Attributes/AuthGuardAttribute.cs returns 403 Forbidden
// (the server-side check was added in commit 92f238d but the matching client-side header was never
// emitted, so every cookie-authed write was failing in the admin). The check is presence-only —
// any non-empty value works — and the protection comes from the fact that a cross-origin form
// submission cannot set custom request headers without a CORS preflight.
const SAFE_HTTP_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * Sends credentials on every API request, and makes an API request whose access token is about to
 * expire wait on a refresh first (`AuthServiceBase.refreshTokenIfExpiring`). That check is the only
 * thing that refreshes a signed-in session: a browser timer fires late under a closed laptop lid
 * or in a long-backgrounded tab, and every request in that gap left with no access cookie. A
 * request that still gets a 401 is not refreshed and sent again (decided 2026-11-11): a browser
 * clock more than about 2 minutes slow, or a sign-out by another app sharing the cookies, shows the
 * `LoginRequired` warning on that request.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(ConfigServiceBase);

  const isApiUrl = req.url.startsWith(config.apiUrl);
  if (!isApiUrl) {
    return next(req);
  }

  const isStateChanging = !SAFE_HTTP_METHODS.has(req.method.toUpperCase());
  req = req.clone({
    withCredentials: true,
    ...(isStateChanging && { setHeaders: { 'X-CSRF': '1' } }),
  });

  if (req.context.get(SKIP_REFRESH_CHECK)) {
    return next(req);
  }
  const refresh = inject(AuthServiceBase).refreshTokenIfExpiring();
  return refresh ? refresh.pipe(switchMap(() => next(req))) : next(req);
};
