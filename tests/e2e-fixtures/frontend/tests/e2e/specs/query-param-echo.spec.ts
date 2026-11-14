import { test, expect } from '@playwright/test';
import { authenticateBrowser } from '../helpers/auth';

// The generated API service sends a DateTime query param as an ISO 8601 string, and the backend binds the
// same instant. Interpolated as it is, a Date went on the wire as its toString() form, which the model
// binder cannot read, so the endpoint answered 400 (spiderly#395). query-param-echo.component.ts sends
// this instant.
test('a Date query param reaches the backend as the same instant', async ({ page, request }) => {
  await authenticateBrowser(page, request);

  const echoRequest = page.waitForRequest((r) => r.url().includes('/api/QueryParamEcho/EchoDateTimeQueryParam'));
  await page.goto('/query-param-echo');

  expect(new URL((await echoRequest).url()).searchParams.get('at')).toBe('2026-03-29T01:30:15.250Z');
  await expect(page.getByTestId('echoed-date-time')).toHaveText('2026-03-29T01:30:15.2500000Z');
});
