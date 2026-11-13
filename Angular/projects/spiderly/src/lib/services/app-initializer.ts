import { Observable, of } from 'rxjs';
import { AuthServiceBase, RefreshOutcome } from './auth.service.base';
import { isPlatformBrowser } from '@angular/common';

// What each RefreshOutcome does at load: AuthServiceBase.onRefreshFailed.
export function authInitializer(
  authService: AuthServiceBase,
  platformId,
): () => Observable<RefreshOutcome> {
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
