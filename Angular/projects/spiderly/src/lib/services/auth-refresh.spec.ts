import { HttpClient, HttpRequest, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';

import { jwtInterceptor } from '../interceptors/jwt.interceptor';
import { unauthorizedInterceptor } from '../interceptors/unauthorized.interceptor';
import { authInitializer } from './app-initializer';
import { AuthServiceBase } from './auth.service.base';
import { ConfigServiceBase } from './config.service.base';
import { SpiderlyMessageService } from './spiderly-message.service';

// The admin's HTTP path through Spiderly, driven end to end: a real HttpClient with the library's
// jwtInterceptor and unauthorizedInterceptor, the real AuthServiceBase and ApiSecurityService.
// HttpTestingController is the server and Jasmine's mock clock is time. Each case asserts only on
// what reaches the server, in which order, and on what user$ holds, which is what the guards read.
describe('the admin refreshes its sign-in', () => {
  const API_URL = 'https://api.test';
  const PRODUCTS = `${API_URL}/Product/GetProductList`;
  const PERMISSION_CODES = `${API_URL}/Security/GetCurrentUserPermissionCodes`;
  const MINUTE = 60_000;
  const OPERATOR = { userId: 7, email: 'operator@example.com' };

  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthServiceBase;

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-11-11T09:00:00Z'));
    spyOn(console, 'error');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([jwtInterceptor, unauthorizedInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ConfigServiceBase, useValue: Object.assign(new ConfigServiceBase(), { apiUrl: API_URL }) },
        {
          provide: SpiderlyMessageService,
          useValue: jasmine.createSpyObj('SpiderlyMessageService', ['warningMessage', 'errorMessage']),
        },
        { provide: TranslocoService, useValue: { translate: (key: string) => key } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthServiceBase);
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    backend.verify();
  });

  const isRefresh = (req: HttpRequest<unknown>) => req.url.startsWith(`${API_URL}/Security/RefreshTokenWithCookies`);

  /** The refresh answer a good refresh cookie gets: the user and a fresh 20-minute access token. */
  const authenticated = () => ({
    ...OPERATOR,
    accessTokenExpiresAt: new Date(Date.now() + 20 * MINUTE).toISOString(),
  });

  /** Answers the permission-code reads a sign-in sends, so they never stand in for the request under test. */
  const answerPermissionCodes = () =>
    backend.match((req) => req.url === PERMISSION_CODES).forEach((req) => req.flush([]));

  /** The admin opens with a good refresh cookie, as `authInitializer` runs it in every app. */
  const signIn = () => {
    authInitializer(auth, TestBed.inject(PLATFORM_ID))().subscribe();
    backend.expectOne(isRefresh).flush(authenticated());
    answerPermissionCodes();
  };

  const currentUser = () => {
    let user: unknown;
    auth.user$.subscribe((value) => (user = value)).unsubscribe();
    return user;
  };

  it('sends a request made 30 seconds before the access token expires only after a refresh', () => {
    signIn();
    jasmine.clock().tick(19.5 * MINUTE);

    http.get(PRODUCTS).subscribe();

    backend.expectNone(PRODUCTS);
    backend.expectOne(isRefresh).flush(authenticated());
    answerPermissionCodes();
    backend.expectOne(PRODUCTS).flush([]);
  });

  it('makes five requests sent at once wait on one refresh', () => {
    signIn();
    jasmine.clock().tick(19.5 * MINUTE);

    for (let i = 0; i < 5; i++) {
      http.get(PRODUCTS).subscribe();
    }

    backend.expectNone(PRODUCTS);
    backend.expectOne(isRefresh, 'one refresh for five requests').flush(authenticated());
    answerPermissionCodes();
    const sent = backend.match(PRODUCTS);
    expect(sent.length).toBe(5);
    sent.forEach((req) => req.flush([]));
  });

  // The laptop slept past the access token's expiry and woke before its Wi-Fi did, or the backend
  // was restarting during a deploy: the server never answered, so it never said no.
  const unanswered: [string, (req: TestRequest) => void][] = [
    ['a 503', (req) => req.flush(null, { status: 503, statusText: 'Service Unavailable' })],
    ['no response (status 0)', (req) => req.error(new ProgressEvent('error'))],
  ];
  for (const [answer, respond] of unanswered) {
    it(`leaves the user signed in when the refresh gets ${answer}`, () => {
      signIn();
      jasmine.clock().tick(21 * MINUTE);

      http.get(PRODUCTS).subscribe();

      respond(backend.expectOne(isRefresh));
      backend.expectOne(PRODUCTS).flush([]);
      expect(currentUser()).toEqual({ id: OPERATOR.userId, email: OPERATOR.email });
    });
  }
});
