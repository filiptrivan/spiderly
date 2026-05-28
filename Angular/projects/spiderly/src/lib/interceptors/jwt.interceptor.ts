import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { ConfigServiceBase } from '../services/config.service.base';

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

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(ConfigServiceBase);

  const isApiUrl = req.url.startsWith(config.apiUrl);
  if (isApiUrl) {
    const isStateChanging = !SAFE_HTTP_METHODS.has(req.method.toUpperCase());
    req = req.clone(
      isStateChanging
        ? { withCredentials: true, setHeaders: { 'X-CSRF': '1' } }
        : { withCredentials: true }
    );
  }

  return next(req);
};
