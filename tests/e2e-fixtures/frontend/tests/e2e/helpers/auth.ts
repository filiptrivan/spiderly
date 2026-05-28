import { Page, expect, APIRequestContext } from '@playwright/test';

export const API_BASE_URL = 'http://localhost:5000';

/**
 * Shared 2FA dance: request a verification code (returned inline in the
 * Development response body when SMTP is unconfigured) and POST it to the
 * given login endpoint. `endpoint` selects between `Login` (tokens-only,
 * body response) and `LoginWithCookies` (sets the refresh token as an
 * HttpOnly cookie via SetAuthResultCookie on the response).
 */
async function requestLogin(request: APIRequestContext, endpoint: 'Login' | 'LoginWithCookies') {
  const sendCodeResponse = await request.post(
    `${API_BASE_URL}/api/Security/SendLoginVerificationEmail`,
    { data: { email: 'test@e2e.com', browserId: 'e2e-browser' } }
  );
  expect(sendCodeResponse.ok()).toBeTruthy();
  const { verificationCode } = await sendCodeResponse.json();
  expect(verificationCode).toBeTruthy();

  const loginResponse = await request.post(
    `${API_BASE_URL}/api/Security/${endpoint}`,
    { data: { email: 'test@e2e.com', browserId: 'e2e-browser', verificationCode } }
  );
  expect(loginResponse.ok()).toBeTruthy();
  const body = await loginResponse.json();
  expect(body.accessToken).toBeTruthy();
  return body;
}

/**
 * API-only login: returns access/refresh tokens in the body. Use for tests
 * that hit the backend directly with an Authorization header and never touch
 * the browser. Browser tests must use {@link authenticateBrowser} instead —
 * the refresh token now lives in an HttpOnly cookie, not a body field.
 */
export async function login(request: APIRequestContext): Promise<{ accessToken: string; refreshToken: string }> {
  const tokens = await requestLogin(request, 'Login');
  return { accessToken: tokens.accessToken, refreshToken: tokens.refreshToken };
}

/**
 * Browser login: drives the cookie-based session the Spiderly admin uses at
 * bootstrap. The app calls /Security/RefreshTokenWithCookies on init, which
 * reads the refresh token from an HttpOnly cookie and uses ?browserId=X as
 * the binding key. Three things must hold before the first navigation:
 *
 *   1. Hit /Security/LoginWithCookies (not /Security/Login) so the backend
 *      issues Set-Cookie for the refresh token.
 *   2. Issue it through `page.request`, not the standalone `request` fixture —
 *      `request` has its own cookie jar that the page does not see.
 *   3. Seed `browser_id` into localStorage via addInitScript so the very first
 *      bootstrap call to RefreshTokenWithCookies sends ?browserId=e2e-browser
 *      (matching what the cookie was issued for, not a freshly-generated GUID).
 *
 * NOTE: The unused `_request` parameter is kept for call-site compatibility —
 * existing specs pass the standalone `request` fixture; we ignore it because
 * its cookie jar is the wrong one.
 */
export async function authenticateBrowser(page: Page, _request: APIRequestContext): Promise<{ accessToken: string }> {
  // (3) Seed browser_id before any page bootstrap.
  await page.addInitScript(() => {
    localStorage.setItem('browser_id', 'e2e-browser');
  });

  // (1) + (2) LoginWithCookies through page.request so Set-Cookie lands in
  // the BrowserContext jar that the page uses on subsequent navigations.
  const tokens = await requestLogin(page.request, 'LoginWithCookies');

  // App bootstrap now: addInitScript seeds browser_id → app calls
  // RefreshTokenWithCookies?browserId=e2e-browser with the refresh cookie →
  // backend returns fresh tokens → authenticated layout renders.
  await page.goto('/');
  await page.locator('sidebar-menu').waitFor({ state: 'visible', timeout: 15000 });

  return { accessToken: tokens.accessToken };
}
