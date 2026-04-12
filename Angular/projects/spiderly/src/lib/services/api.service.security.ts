import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuthResult,
  AuthResultWithCookies,
  ExternalProvider,
  Login,
  RefreshTokenRequest,
  SendLoginVerificationEmailResult,
  UserBase,
  VerificationTokenRequest,
} from '../entities/security-entities';
import { ConfigServiceBase } from './config.service.base';

@Injectable({
  providedIn: 'root',
})
export class ApiSecurityService {
  constructor(
    protected http: HttpClient,
    protected config: ConfigServiceBase,
  ) {}

  //#region Authentication

  login = (request: VerificationTokenRequest): Observable<AuthResult> => {
    return this.http.post<AuthResult>(
      `${this.config.apiUrl}/Security/Login`,
      request,
      this.config.httpOptions,
    );
  };

  loginExternal = (
    externalProviderDTO: ExternalProvider,
  ): Observable<AuthResult> => {
    return this.http.post<AuthResult>(
      `${this.config.apiUrl}/Security/LoginExternal`,
      externalProviderDTO,
      this.config.httpOptions,
    );
  };

  sendLoginVerificationEmail = (
    loginDTO: Login,
  ): Observable<SendLoginVerificationEmailResult> => {
    return this.http.post<SendLoginVerificationEmailResult>(
      `${this.config.apiUrl}/Security/SendLoginVerificationEmail`,
      loginDTO,
      this.config.httpOptions,
    );
  };

  loginWithCookies = (
    request: VerificationTokenRequest,
  ): Observable<AuthResultWithCookies> => {
    return this.http.post<AuthResultWithCookies>(
      `${this.config.apiUrl}/Security/LoginWithCookies`,
      request,
      this.config.httpOptions,
    );
  };

  loginExternalWithCookies = (
    externalProviderDTO: ExternalProvider,
  ): Observable<AuthResultWithCookies> => {
    return this.http.post<AuthResultWithCookies>(
      `${this.config.apiUrl}/Security/LoginExternalWithCookies`,
      externalProviderDTO,
      this.config.httpOptions,
    );
  };

  logout = (browserId: string): Observable<any> => {
    return this.http.get<any>(
      `${this.config.apiUrl}/Security/Logout?browserId=${browserId}`,
    );
  };

  logoutWithCookies = (browserId: string): Observable<any> => {
    return this.http.get<any>(
      `${this.config.apiUrl}/Security/LogoutWithCookies?browserId=${browserId}`,
    );
  };

  refreshTokenWithHeaders = (request: RefreshTokenRequest): Observable<AuthResult> => {
    return this.http.post<AuthResult>(
      `${this.config.apiUrl}/Security/RefreshTokenWithHeaders`,
      request,
      this.config.httpOptions,
    );
  };

  refreshTokenWithCookies = (browserId: string): Observable<AuthResultWithCookies> => {
    return this.http.get<AuthResultWithCookies>(
      `${this.config.apiUrl}/Security/RefreshTokenWithCookies?browserId=${browserId}`,
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
