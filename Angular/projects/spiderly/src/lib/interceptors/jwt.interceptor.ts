import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { ConfigBaseService } from '../services/config-base.service';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(ConfigBaseService);

  const accessToken = localStorage.getItem('access_token');
  const isApiUrl = req.url.startsWith(config.apiUrl);

  if (accessToken && isApiUrl) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${accessToken}` },
    });
  }

  return next(req);
}