import { Inject, Injectable, OnDestroy, PLATFORM_ID } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of, Subscription } from 'rxjs';
import { catchError, delay, finalize, map, tap } from 'rxjs/operators';
import {
  AuthResultWithCookies,
  Login,
  VerificationTokenRequest,
  UserBase,
} from '../entities/security-entities';
import { ConfigServiceBase } from './config.service.base';
import { ApiSecurityService } from './api.service.security';
import { InitCompanyAuthDialogDetails } from '../entities/init-company-auth-dialog-details';
import { isPlatformBrowser } from '@angular/common';

/**
 * Cookie-based session auth. The access/refresh JWTs live in HttpOnly cookies set by the backend
 * (so JS never holds them — XSS can't exfiltrate them); requests are authenticated via
 * `withCredentials` (see jwtInterceptor). The readable result only carries userId/email/expiry.
 */
@Injectable({
  providedIn: 'root',
})
export class AuthServiceBase implements OnDestroy {
  private readonly apiUrl: string = this.config.apiUrl;
  private timer?: Subscription;
  private accessTokenExpiresAt?: Date;

  protected _currentUserPermissionCodes = new BehaviorSubject<string[] | null>(
    undefined,
  );
  currentUserPermissionCodes$ = this._currentUserPermissionCodes.asObservable();

  protected _user = new BehaviorSubject<UserBase | null>(undefined);
  user$ = this._user.asObservable();

  constructor(
    protected router: Router,
    protected http: HttpClient,
    protected apiService: ApiSecurityService,
    protected config: ConfigServiceBase,
    @Inject(PLATFORM_ID) protected platformId: Object,
  ) {
    if (isPlatformBrowser(platformId)) {
      window.addEventListener('storage', this.storageEventListener);
      window.addEventListener('pageshow', this.pageShowListener);
    }
  }

  // Back/forward-cache guard. When the browser restores a page from bfcache (e.g. the user logs out,
  // navigates away, then clicks Back), the whole document is restored with its in-memory session
  // snapshot intact and route guards do NOT re-run — so a stale authenticated view can show even
  // though the session is gone. Re-validate against the cookie session on restore; if it's gone,
  // send the user to login (the same outcome a hard refresh already produces).
  private pageShowListener = (event: PageTransitionEvent) => {
    if (!event.persisted) {
      return;
    }
    this.refreshToken().subscribe((result) => {
      if (!result) {
        this.router.navigate([this.config.loginSlug]);
      }
    });
  };

  // Cross-tab sync. We store only marker values here (never tokens — those are HttpOnly cookies).
  private storageEventListener = (event: StorageEvent) => {
    if (event.storageArea === localStorage) {
      if (event.key === 'logout-event') {
        this.stopTokenTimer();
        this._user.next(null);
        this._currentUserPermissionCodes.next(null);
      }
      if (event.key === 'login-event') {
        this.refreshToken().subscribe();
      }
    }
  };

  sendLoginVerificationEmail(body: Login): Observable<any> {
    body.browserId = this.getBrowserId();
    return this.apiService.sendLoginVerificationEmail(body);
  }

  login(body: VerificationTokenRequest): Observable<AuthResultWithCookies> {
    body.browserId = this.getBrowserId();
    return this.apiService.loginWithCookies(body).pipe(
      map((result: AuthResultWithCookies) => {
        this.handleAuthResult(result);
        return result;
      }),
    );
  }

  // Establishes the in-memory session from a cookie auth result (login or refresh). No tokens are stored
  // in JS — only the user identity + the access-token expiry the backend reports (to schedule refresh).
  handleAuthResult(result: AuthResultWithCookies) {
    this._user.next({
      id: result.userId,
      email: result.email,
    });
    this.accessTokenExpiresAt = result.accessTokenExpiresAt
      ? new Date(result.accessTokenExpiresAt)
      : undefined;
    localStorage.setItem('login-event', 'login' + Math.random());
    this.startTokenTimer();
    this.setCurrentUserPermissionCodes().subscribe();
  }

  logout() {
    const browserId = this.getBrowserId();
    this.apiService
      .logoutWithCookies(browserId)
      .pipe(
        finalize(() => {
          this._user.next(null);
          localStorage.setItem('logout-event', 'logout' + Math.random());
          this.onAfterLogout();
          this.stopTokenTimer();
        }),
      )
      .subscribe();
  }

  onAfterLogout = () => {
    this._currentUserPermissionCodes.next(null);
    this.router.navigate([this.config.loginSlug]);
  };

  // Clears in-memory session state without calling the backend — used when a request comes back 401
  // (the backend has already cleared the auth cookies in that case).
  clearSession() {
    this.stopTokenTimer();
    this._user.next(null);
    this._currentUserPermissionCodes.next(null);
    localStorage.setItem('logout-event', 'logout' + Math.random());
  }

  // Called on app init and by the proactive timer. The refresh token is an HttpOnly cookie; a 401 ("no valid
  // session" — not logged in / expired) propagates from the interceptor and is handled by catchError below,
  // resolving the session to anonymous (null). map runs only for a real result, so _user is never partial.
  refreshToken(): Observable<AuthResultWithCookies | null> {
    const browserId = this.getBrowserId();
    return this.apiService.refreshTokenWithCookies(browserId).pipe(
      map((result: AuthResultWithCookies) => {
        if (result) {
          this._user.next({ id: result.userId, email: result.email });
          this.accessTokenExpiresAt = result.accessTokenExpiresAt
            ? new Date(result.accessTokenExpiresAt)
            : undefined;
          this.startTokenTimer();
          this.onAfterRefreshToken();
        }
        return result;
      }),
      catchError(() => {
        this._user.next(null);
        return of(null);
      }),
    );
  }

  onAfterRefreshToken = () => {
    this.setCurrentUserPermissionCodes().subscribe(); // after the session is re-established
  };

  getBrowserId(): string {
    let browserId = localStorage.getItem(this.config.browserIdKey); // not a token — a stable per-browser id
    if (!browserId) {
      browserId = crypto.randomUUID();
      localStorage.setItem(this.config.browserIdKey, browserId);
    }
    return browserId;
  }

  private getTokenRemainingTime(): number {
    if (!this.accessTokenExpiresAt) {
      return 0;
    }
    return this.accessTokenExpiresAt.getTime() - Date.now();
  }

  private startTokenTimer() {
    const timeout = this.getTokenRemainingTime();
    if (timeout <= 0) {
      return;
    }
    this.stopTokenTimer();
    this.timer = of(true)
      .pipe(
        delay(timeout),
        tap({
          next: () => this.refreshToken().subscribe(),
        }),
      )
      .subscribe();
  }

  private stopTokenTimer() {
    this.timer?.unsubscribe();
  }

  navigateToDashboard() {
    this.router.navigate(['/']);
  }

  initCompanyAuthDialogDetails =
    (): Observable<InitCompanyAuthDialogDetails> => {
      return of(
        new InitCompanyAuthDialogDetails({
          image: this.config.logoPath,
          companyName: this.config.companyName,
        }),
      );
    };

  setCurrentUserPermissionCodes(): Observable<string[]> {
    return this.apiService.getCurrentUserPermissionCodes().pipe(
      map((permissionCodes) => {
        this._currentUserPermissionCodes.next(permissionCodes);
        return permissionCodes;
      }),
    );
  }

  ngOnDestroy(): void {
    if (isPlatformBrowser(this.platformId)) {
      window.removeEventListener('storage', this.storageEventListener);
      window.removeEventListener('pageshow', this.pageShowListener);
    }

    this.onAfterNgOnDestroy();
  }

  onAfterNgOnDestroy = () => {};
}
