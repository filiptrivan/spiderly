import { HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ConfigServiceBase {
  production = false;
  apiUrl: string;
  frontendUrl = 'http://localhost:4200';
  GoogleClientId: string;
  showGoogleAuth = false;
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
  pageSizeOptions: number[] = [10, 25, 50];

  constructor() {}
}
