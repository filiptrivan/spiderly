import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';

import { AuthServiceBase } from '../services/auth.service.base';
import { SpiderlyMessageService } from '../services/spiderly-message.service';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

// Driven through a real HttpClient + HttpTestingController rather than by calling the function:
// the claim under test is about what Angular hands the interceptor as `err.error` for each
// responseType, and only the real pipeline produces that (a Blob for 'blob', a string for 'text').
describe('unauthorizedInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let messageService: { warningMessage: jasmine.Spy; errorMessage: jasmine.Spy };

  beforeEach(() => {
    spyOn(console, 'error');
    messageService = {
      warningMessage: jasmine.createSpy('warningMessage'),
      errorMessage: jasmine.createSpy('errorMessage'),
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
        { provide: SpiderlyMessageService, useValue: messageService },
        { provide: TranslocoService, useValue: { translate: (key: string) => key } },
        { provide: AuthServiceBase, useValue: { clearSession: jasmine.createSpy('clearSession') } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  /** A request that is expected to fail, resolved with the HttpErrorResponse the interceptor rethrew. */
  const failing = (url: string, responseType?: 'blob' | 'text'): Promise<HttpErrorResponse> =>
    new Promise((resolve) => {
      // Three calls rather than one with a variable: HttpClient.get's overloads key on the literal.
      const request =
        responseType === 'blob'
          ? http.get(url, { responseType: 'blob' })
          : responseType === 'text'
            ? http.get(url, { responseType: 'text' })
            : http.get(url);
      request.subscribe({ error: resolve });
    });

  // A download endpoint answering a BusinessException: the body is JSON, but the request asked
  // for a Blob, so that is what Angular hands back — and its text is only readable asynchronously.
  // Before the Blob branch existed the operator got the generic 'BadRequestDetails' toast for a
  // sentence the server had spelled out.
  it('reads the server message out of a Blob error body and toasts it', async () => {
    const failed = failing('/api/Order/PrintPickupList', 'blob');
    const body = new Blob([JSON.stringify({ message: 'Sve izabrane pošiljke (40) već imaju zakazano preuzimanje.' })], {
      type: 'application/json',
    });
    backend.expectOne('/api/Order/PrintPickupList').flush(body, { status: 400, statusText: 'Bad Request' });

    const err = await failed;

    expect(err.status).toBe(400);
    expect(messageService.warningMessage).toHaveBeenCalledWith(
      'Sve izabrane pošiljke (40) već imaju zakazano preuzimanje.',
      'Warning',
    );
  });

  it('falls back to the generic message when the Blob is not JSON', async () => {
    const failed = failing('/api/Order/PrintPickupList', 'blob');
    backend
      .expectOne('/api/Order/PrintPickupList')
      .flush(new Blob(['<html>Bad Gateway</html>'], { type: 'text/html' }), { status: 400, statusText: 'Bad Request' });

    await failed;

    expect(messageService.warningMessage).toHaveBeenCalledWith('BadRequestDetails', 'Warning');
  });

  // The pre-existing behaviours the Blob branch must not disturb.
  it('still parses a string error body on a text request', async () => {
    const failed = failing('/api/x', 'text');
    backend.expectOne('/api/x').flush(JSON.stringify({ message: 'Spelled out' }), { status: 400, statusText: 'Bad Request' });

    await failed;

    expect(messageService.warningMessage).toHaveBeenCalledWith('Spelled out', 'Warning');
  });

  it('still toasts a JSON error body on a json request, and rethrows', async () => {
    const failed = failing('/api/x');
    backend.expectOne('/api/x').flush({ message: 'Spelled out', traceId: 'abc' }, { status: 400, statusText: 'Bad Request' });

    const err = await failed;

    expect(err).toBeInstanceOf(HttpErrorResponse);
    expect(messageService.warningMessage).toHaveBeenCalledWith('Spelled out ErrorReference', 'Warning');
  });
});
