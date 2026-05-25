import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { ConfigServiceBase } from '../services/config.service.base';

// Cookie-based auth: the session JWTs are HttpOnly cookies, so we just send credentials on API calls.
// The browser attaches/refreshes the cookies; JS never holds the tokens (XSS-safe).
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(ConfigServiceBase);

  const isApiUrl = req.url.startsWith(config.apiUrl);
  if (isApiUrl) {
    req = req.clone({ withCredentials: true });
  }

  return next(req);
};
