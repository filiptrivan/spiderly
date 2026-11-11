import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuthResult,
  AuthResultWithCookies,
  ExternalProviderPublic,
  Login,
  RefreshTokenRequest,
  SendLoginVerificationEmailResult,
  UserBase,
  VerificationTokenRequest,
} from '../entities/security-entities';
import { ConfigServiceBase } from './config.service.base';
import { SKIP_REFRESH_CHECK } from '../interceptors/http-context-tokens';

@Injectable({
  providedIn: 'root',
})
export class ApiSecurityService {
  constructor(
    protected http: HttpClient,
    protected config: ConfigServiceBase,
  ) {}

  //#region Authentication

  // The options of a request that signs in, sends the verification email, refreshes or signs out:
  // it carries SKIP_REFRESH_CHECK, so it never waits on a refresh.
  private authRequestOptions<T extends object>(options?: T, context = new HttpContext()) {
    return { ...options, context: context.set(SKIP_REFRESH_CHECK, true) };
  }

  login = (request: VerificationTokenRequest): Observable<AuthResult> => {
    return this.http.post<AuthResult>(
      `${this.config.apiUrl}/Security/Login`,
      request,
      this.authRequestOptions(this.config.httpOptions),
    );
  };

  sendLoginVerificationEmail = (
    loginDTO: Login,
  ): Observable<SendLoginVerificationEmailResult> => {
    return this.http.post<SendLoginVerificationEmailResult>(
      `${this.config.apiUrl}/Security/SendLoginVerificationEmail`,
      loginDTO,
      this.authRequestOptions(this.config.httpOptions),
    );
  };

  loginWithCookies = (
    request: VerificationTokenRequest,
  ): Observable<AuthResultWithCookies> => {
    return this.http.post<AuthResultWithCookies>(
      `${this.config.apiUrl}/Security/LoginWithCookies`,
      request,
      this.authRequestOptions(this.config.httpOptions),
    );
  };

  getExternalProviders = (): Observable<ExternalProviderPublic[]> => {
    return this.http.get<ExternalProviderPublic[]>(
      `${this.config.apiUrl}/Security/GetExternalProviders`,
      this.config.httpSkipSpinnerOptions,
    );
  };

  logout = (browserId: string): Observable<any> => {
    return this.http.get<any>(
      `${this.config.apiUrl}/Security/Logout?browserId=${browserId}`,
      this.authRequestOptions(),
    );
  };

  logoutWithCookies = (browserId: string): Observable<any> => {
    return this.http.get<any>(
      `${this.config.apiUrl}/Security/LogoutWithCookies?browserId=${browserId}`,
      this.authRequestOptions(),
    );
  };

  refreshTokenWithHeaders = (request: RefreshTokenRequest): Observable<AuthResult> => {
    return this.http.post<AuthResult>(
      `${this.config.apiUrl}/Security/RefreshTokenWithHeaders`,
      request,
      this.authRequestOptions(this.config.httpOptions),
    );
  };

  // `context` carries the caller's own marks (AuthServiceBase's SKIP_ERROR_MESSAGE).
  refreshTokenWithCookies = (
    browserId: string,
    context?: HttpContext,
  ): Observable<AuthResultWithCookies> => {
    // POST, not GET: refresh rotates the single-use token (a state mutation), and a cacheable GET let
    // browsers replay a stale logged-in response on back/forward navigation (phantom dashboard after logout).
    return this.http.post<AuthResultWithCookies>(
      `${this.config.apiUrl}/Security/RefreshTokenWithCookies?browserId=${browserId}`,
      null,
      this.authRequestOptions({}, context),
    );
  };

  //#endregion

  //#region User

  getCurrentUserBase = (): Observable<UserBase> => {
    return this.http.get<UserBase>(
      `${this.config.apiUrl}/Security/GetCurrentUserBase`,
      this.config.httpSkipSpinnerOptions,
    );
  };

  getCurrentUserPermissionCodes = (): Observable<string[]> => {
    return this.http.get<string[]>(
      `${this.config.apiUrl}/Security/GetCurrentUserPermissionCodes`,
      this.config.httpSkipSpinnerOptions,
    );
  };

  //#endregion
}
