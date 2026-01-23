import { inject, PLATFORM_ID } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { ConfigServiceBase } from '../services/config.service.base';
import { isPlatformBrowser } from '@angular/common';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(ConfigServiceBase);
  const platformId = inject(PLATFORM_ID);

  let accessToken = null;
  if (isPlatformBrowser(platformId)) {
    accessToken = localStorage.getItem('access_token');
  }

  const isApiUrl = req.url.startsWith(config.apiUrl);

  if (accessToken && isApiUrl) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${accessToken}` },
    });
  }

  return next(req);
};
