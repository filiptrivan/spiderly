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

  // External-login error code captured from the bootstrap URL (?externalAuthError=expired|failed) set by the
  // backend's OAuth callback on failure. Captured before routing can strip it, surfaced once by the login page.
  externalAuthErrorCode: string | null = null;

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
    }
  }

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
          // A re-established session makes any pending external-login error moot — drop it so it can't
          // surface as a stale toast on a later /login visit (e.g. after a subsequent logout).
          this.externalAuthErrorCode = null;
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

  // Reads ?externalAuthError= from the bootstrap URL (set by the backend OAuth callback on failure) and
  // strips it so a manual refresh won't re-trigger the message. Called from the app initializer, before the
  // router runs — otherwise an unauthenticated landing on "/" redirects to /login and drops the param.
  captureExternalAuthError() {
    if (isPlatformBrowser(this.platformId) === false) {
      return;
    }
    const params = new URLSearchParams(window.location.search);
    const code = params.get('externalAuthError');
    if (!code) {
      return;
    }
    this.externalAuthErrorCode = code;
    params.delete('externalAuthError');
    const query = params.toString();
    history.replaceState(
      history.state,
      '',
      window.location.pathname + (query ? `?${query}` : '') + window.location.hash,
    );
  }

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
    }

    this.onAfterNgOnDestroy();
  }

  onAfterNgOnDestroy = () => {};
}
