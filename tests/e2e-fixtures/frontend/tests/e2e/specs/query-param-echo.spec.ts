import { test, expect } from '@playwright/test';
import { authenticateBrowser } from '../helpers/auth';

// The backend binds the instant the generated API service sends (why ISO: NgControllersGenerator's
// GetQueryParamValue). query-param-echo.component.ts sends this instant.
test('a Date query param reaches the backend as the same instant', async ({ page, request }) => {
  await authenticateBrowser(page, request);

  const echoRequest = page.waitForRequest((r) => r.url().includes('/api/QueryParamEcho/EchoDateTimeQueryParam'));
  await page.goto('/query-param-echo');

  expect(new URL((await echoRequest).url()).searchParams.get('at')).toBe('2026-03-29T01:30:15.250Z');
  await expect(page.getByTestId('echoed-date-time')).toHaveText('2026-03-29T01:30:15.2500000Z');
});
