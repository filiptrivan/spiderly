import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { jsonHttpInterceptor } from './json-parser.interceptor';

// Driven through a real HttpClient + HttpTestingController: the body the interceptor gets depends on
// the request's responseType, and only the real pipeline produces it.
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

  // A GET that returns a bare DateTime: the whole JSON body is one ISO string.
  it('turns a JSON body that is itself an ISO string into a Date', async () => {
    const response = firstValueFrom(http.get<Date>('/api/Order/GetLastSyncTime'));
    backend.expectOne('/api/Order/GetLastSyncTime').flush('2026-03-29T01:30:15.250Z');

    expect(await response).toEqual(new Date('2026-03-29T01:30:15.250Z'));
  });

  // A C# string action answers text, and its generated method promises a string.
  it('leaves a text body that holds an ISO string as the string', async () => {
    const response = firstValueFrom(http.get('/api/Report/GetStamp', { responseType: 'text' }));
    backend.expectOne('/api/Report/GetStamp').flush('2026-03-29T01:30:15.2500000Z');

    expect(await response).toBe('2026-03-29T01:30:15.2500000Z');
  });
});
