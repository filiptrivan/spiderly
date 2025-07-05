import { Observable, of } from 'rxjs';
import { AuthBaseService } from './auth-base.service';
import { AuthResult } from '../entities/security-entities';
import { isPlatformBrowser } from '@angular/common';

export function authInitializer(authService: AuthBaseService, platformId): () => Observable<AuthResult> {
  if (isPlatformBrowser(platformId)) {
    return () => {
      return authService.refreshToken();
    };
  }
  return () => {
    return of();
  };
}