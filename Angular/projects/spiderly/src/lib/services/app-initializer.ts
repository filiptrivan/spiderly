import { Observable, of } from 'rxjs';
import { AuthServiceBase, RefreshOutcome } from './auth.service.base';
import { isPlatformBrowser } from '@angular/common';

// At load the refresh signs in, a rejected one signs out, and one the server never answered signs
// this load alone out (AuthServiceBase.onRefreshFailed).
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
