import { Inject, Injectable, OnDestroy, PLATFORM_ID } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, filter, finalize, map, shareReplay } from 'rxjs/operators';
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
import { SKIP_ERROR_MESSAGE } from '../interceptors/http-context-tokens';

/**
 * How a refresh ended:
 * - `authenticated`: the server issued a new access token;
 * - `rejected`: the server said no, a 4xx (the backend's 401 `invalid_token` for a refresh token
 *   expired, revoked or reused, or its 403 for a security violation), so the sign-in is over;
 * - `transient`: the server never said no (no response, a 5xx, a NOT_A_NO_STATUSES answer, or a
 *   throw that is not an HTTP error), so the sign-in stands and the next request tries again.
 */
export type RefreshOutcome = 'authenticated' | 'rejected' | 'transient';

/**
 * The 4xx answers that ask for a pause rather than say no: Spiderly's GlobalLimiter
 * (SpiderlyAddRateLimiters) covers the refresh endpoint and answers 429, and a 408 is a timeout.
 */
const NOT_A_NO_STATUSES = new Set([408, 429]);

/**
 * A request leaving with less than this left on the access token, or none, waits on a refresh
 * first. With the backend's `JwtOptions.ClockSkewMinutes` at its default of 1, a browser clock up
 * to about 2 minutes slow still refreshes before the server or the browser drops the token.
 */
const ACCESS_TOKEN_REFRESH_MARGIN_MS = 60_000;

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
  private accessTokenExpiresAt?: Date;
  private refreshInFlight?: Observable<RefreshOutcome>;

  // External-login error code captured from the bootstrap URL (?externalAuthError=expired|failed) set by the
  // backend's OAuth callback on failure. Captured before routing can strip it, surfaced once by the login page.
  externalAuthErrorCode: string | null = null;

  protected _currentUserPermissionCodes = new BehaviorSubject<string[] | null>(
    undefined,
  );
  // The subject seeds with `undefined` (not yet loaded) and emits `null` on logout. Consumers only ever care
  // about a real code list, so filter both out here — subscribers get a clean `string[]`, and `firstValueFrom`
  // waits for the first loaded value instead of grabbing the `undefined` seed in a load race.
  currentUserPermissionCodes$: Observable<string[]> =
    this._currentUserPermissionCodes
      .asObservable()
      .pipe(filter((codes): codes is string[] => codes != null));

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

  // Establishes the in-memory session from a login's cookie auth result, and tells the other tabs.
  handleAuthResult(result: AuthResultWithCookies) {
    this.setSession(result);
    localStorage.setItem('login-event', 'login' + Math.random());
    this.setCurrentUserPermissionCodes().subscribe();
  }

  // No tokens are stored in JS — only the user identity + the access-token expiry the backend reports,
  // which refreshTokenIfExpiring reads.
  private setSession(result: AuthResultWithCookies) {
    this._user.next({
      id: result.userId,
      email: result.email,
    });
    this.accessTokenExpiresAt = result.accessTokenExpiresAt
      ? new Date(result.accessTokenExpiresAt)
      : undefined;
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
        }),
      )
      .subscribe();
  }

  onAfterLogout = () => {
    this._currentUserPermissionCodes.next(null);
    this.router.navigate([this.config.loginSlug]);
  };

  // Clears in-memory session state without calling the backend — used when a request comes back 401
  // or a refresh is rejected. The auth cookies stay as they are: the backend clears them only in
  // LogoutWithCookies. A session already signed out (null) has nothing to clear: a load's refresh
  // rejected with a 401 `invalid_token` reaches here twice, from unauthorizedInterceptor and from
  // onRefreshFailed, and the other tabs hear it once.
  clearSession() {
    if (this._user.value === null) {
      return;
    }
    this._user.next(null);
    this._currentUserPermissionCodes.next(null);
    localStorage.setItem('logout-event', 'logout' + Math.random());
  }

  /**
   * Asks the server for a new access token from the refresh-token cookie. Every caller (the check
   * before a request, `authInitializer`, the cross-tab `login-event`) shares the one refresh in
   * flight until it settles.
   */
  refreshToken(): Observable<RefreshOutcome> {
    return this.refresh(new HttpContext());
  }

  /**
   * The refresh `jwtInterceptor` makes an API request wait on, or null when the request may leave
   * at once.
   */
  refreshTokenIfExpiring(): Observable<RefreshOutcome> | null {
    if (!isPlatformBrowser(this.platformId) || !this._user.value || !this.accessTokenExpiresAt) {
      return null;
    }
    if (this.accessTokenExpiresAt.getTime() - Date.now() >= ACCESS_TOKEN_REFRESH_MARGIN_MS) {
      return null;
    }
    // No message of its own: whatever the outcome, the request leaves next and reports its own failure.
    return this.refresh(new HttpContext().set(SKIP_ERROR_MESSAGE, true));
  }

  // The context is the first caller's: a caller that joins a refresh in flight shares its messages.
  private refresh(context: HttpContext): Observable<RefreshOutcome> {
    this.refreshInFlight ??= this.apiService.refreshTokenWithCookies(this.getBrowserId(), context).pipe(
      map((result) => this.onRefreshAuthenticated(result)),
      catchError((error) => of(this.onRefreshFailed(error))),
      finalize(() => (this.refreshInFlight = undefined)),
      // refCount false: the refresh runs to its end even when every waiting request is cancelled,
      // since the server may already have rotated the refresh token.
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshInFlight;
  }

  private onRefreshAuthenticated(result: AuthResultWithCookies): RefreshOutcome {
    // A re-established session makes any pending external-login error moot — drop it so it can't
    // surface as a stale toast on a later /login visit (e.g. after a subsequent logout).
    this.externalAuthErrorCode = null;
    this.setSession(result);
    this.onAfterRefreshToken();
    return 'authenticated';
  }

  private onRefreshFailed(error: unknown): RefreshOutcome {
    if (
      error instanceof HttpErrorResponse &&
      error.status >= 400 &&
      error.status < 500 &&
      !NOT_A_NO_STATUSES.has(error.status)
    ) {
      this.clearSession();
      return 'rejected';
    }
    // The user, permission codes and expiry stay as they were. At load they were not known yet
    // (undefined), and the guards wait for a known user, so this load alone is signed out: no
    // logout and no cookie touched, so a reload signs back in.
    if (this._user.value === undefined) {
      this._user.next(null);
    }
    return 'transient';
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
