import { Observable, of } from 'rxjs';
import { AuthServiceBase } from './auth.service.base';
import { AuthResult } from '../entities/security-entities';
import { isPlatformBrowser } from '@angular/common';

export function authInitializer(
  authService: AuthServiceBase,
  platformId,
): () => Observable<AuthResult> {
  if (isPlatformBrowser(platformId)) {
    return () => {
      authService.captureExternalAuthError(); // before the router can strip ?externalAuthError on the /login redirect
      return authService.refreshToken();
    };
  }
  return () => {
    return of();
  };
}
