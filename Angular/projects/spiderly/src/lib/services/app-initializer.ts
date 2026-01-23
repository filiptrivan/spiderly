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
      return authService.refreshToken();
    };
  }
  return () => {
    return of();
  };
}
