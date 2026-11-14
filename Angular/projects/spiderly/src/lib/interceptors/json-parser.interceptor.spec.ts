import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { jsonHttpInterceptor } from './json-parser.interceptor';

// A real HttpClient and HttpTestingController drive these specs. The body the interceptor gets
// depends on the request's responseType, and only the real pipeline produces that body.
describe('jsonHttpInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([jsonHttpInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('turns an ISO string inside a JSON body into a Date', async () => {
    const response = firstValueFrom(http.get<{ createdAt: Date }>('/api/Order/GetOrder'));
    backend.expectOne('/api/Order/GetOrder').flush({ createdAt: '2026-03-29T01:30:15.250Z' });

    expect((await response).createdAt).toEqual(new Date('2026-03-29T01:30:15.250Z'));
  });

  it('turns the ISO strings inside a JSON array into Dates', async () => {
    const response = firstValueFrom(http.get<unknown[]>('/api/Order/GetOrderList'));
    backend
      .expectOne('/api/Order/GetOrderList')
      .flush([{ createdAt: '2026-03-29T01:30:15.250Z' }, '2026-03-30T08:00:00.000Z', 7]);

    expect(await response).toEqual([
      { createdAt: new Date('2026-03-29T01:30:15.250Z') },
      new Date('2026-03-30T08:00:00.000Z'),
      7,
    ]);
  });

  it('leaves a null JSON body null', async () => {
    const response = firstValueFrom(http.get('/api/Order/GetNothing'));
    backend.expectOne('/api/Order/GetNothing').flush(null);

    expect(await response).toBeNull();
  });

  // A GET that returns a bare DateTime.
  it('turns a JSON body that is itself an ISO string into a Date', async () => {
    const response = firstValueFrom(http.get<Date>('/api/Order/GetLastSyncTime'));
    backend.expectOne('/api/Order/GetLastSyncTime').flush('2026-03-29T01:30:15.250Z');

    expect(await response).toEqual(new Date('2026-03-29T01:30:15.250Z'));
  });

  // Why a text body stays text: jsonHttpInterceptor.
  it('leaves a text body that holds an ISO string as the string', async () => {
    const response = firstValueFrom(http.get('/api/Report/GetStamp', { responseType: 'text' }));
    backend.expectOne('/api/Report/GetStamp').flush('2026-03-29T01:30:15.2500000Z');

    expect(await response).toBe('2026-03-29T01:30:15.2500000Z');
  });
});
