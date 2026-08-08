import { HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ConfigServiceBase {
  /**
   * Consumer-facing only — nothing in the library reads it. The error handler and the HTTP
   * interceptor used to gate their `console.error` on it, which left a deployed app's client
   * errors with nowhere to go; both now log unconditionally. `spiderly init` still scaffolds the
   * `override`, so apps keep branching on it.
   */
  production = false;
  apiUrl: string;
  frontendUrl = 'http://localhost:4200';
  companyName = 'Company Name';
  primaryColor = '#111b2c';

  /* URLs */
  loginSlug = 'login';

  /* Local storage */
  accessTokenKey = 'access_token';
  refreshTokenKey = 'refresh_token';
  browserIdKey = 'browser_id';

  httpOptions = {};
  httpSkipSpinnerOptions = {
    headers: new HttpHeaders({ 'Content-Type': 'application/json' }),
    params: new HttpParams().set('X-Skip-Spinner', 'true'),
  };

  logoPath = 'assets/images/logo/logo.svg';

  /* Pagination */
  defaultPageSize = 10;

  constructor() {}
}
