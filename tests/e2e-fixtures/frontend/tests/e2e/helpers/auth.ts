import { Page, expect, APIRequestContext } from '@playwright/test';

export const API_BASE_URL = 'http://localhost:5000';

const TEST_EMAIL = 'test@e2e.com';
const TEST_BROWSER_ID = 'e2e-browser';

/**
 * Shared 2FA dance: requests a verification code and returns it. In the
 * Development environment with no SMTP configured the backend returns the
 * code inline in the response body (no email is actually sent).
 */
async function sendVerificationCode(request: APIRequestContext): Promise<string> {
  const response = await request.post(
    `${API_BASE_URL}/api/Security/SendLoginVerificationEmail`,
    { data: { email: TEST_EMAIL, browserId: TEST_BROWSER_ID } }
  );
  expect(response.ok()).toBeTruthy();
  const { verificationCode } = await response.json();
  expect(verificationCode).toBeTruthy();
  return verificationCode;
}

/**
 * API-only login: hits /Security/Login and returns the access/refresh tokens
 * in the response body. Use for tests that exercise the backend with an
 * Authorization header and never touch the browser. Browser tests must use
 * {@link authenticateBrowser} — the admin's session restoration is
 * cookie-based and ignores body tokens.
 */
export async function login(request: APIRequestContext): Promise<{ accessToken: string; refreshToken: string }> {
  const verificationCode = await sendVerificationCode(request);
  const loginResponse = await request.post(
    `${API_BASE_URL}/api/Security/Login`,
    { data: { email: TEST_EMAIL, browserId: TEST_BROWSER_ID, verificationCode } }
  );
  expect(loginResponse.ok()).toBeTruthy();
  const body = await loginResponse.json();
  expect(body.accessToken).toBeTruthy();
  return { accessToken: body.accessToken, refreshToken: body.refreshToken };
}

/**
 * Browser login: drives the cookie-based session the Spiderly admin uses at
 * bootstrap. /Security/LoginWithCookies sets THREE cookies on the response —
 * access_token (HttpOnly), refresh_token (HttpOnly), and AuthResult
 * (JS-readable, holds { userId, email, accessTokenExpiresAt }). The response
 * body is just the AuthResult payload; the tokens are in cookies, not body.
 *
 * Three things must hold before the first navigation:
 *
 *   1. Hit /Security/LoginWithCookies (not /Security/Login) so the backend
 *      sets the three cookies (only the WithCookies variant does this).
 *   2. Issue it via page.request so Set-Cookie lands in the BrowserContext
 *      jar the page actually uses — the standalone `request` fixture has its
 *      own jar.
 *   3. Seed browser_id into localStorage via addInitScript so the very first
 *      bootstrap call to RefreshTokenWithCookies sends ?browserId=e2e-browser
 *      (matching what the cookies were issued for; otherwise the app generates
 *      a fresh GUID and the server rejects the refresh as bound to nothing).
 *
 * The unused `_request` param is kept for call-site stability with existing
 * specs that pass the standalone `request` fixture — it has the wrong cookie
 * jar so we deliberately ignore it.
 */
export async function authenticateBrowser(page: Page, _request: APIRequestContext): Promise<void> {
  // (3) Seed browser_id before any page bootstrap.
  await page.addInitScript((browserId: string) => {
    localStorage.setItem('browser_id', browserId);
  }, TEST_BROWSER_ID);

  // (1) + (2) LoginWithCookies through page.request so Set-Cookie lands in
  // the BrowserContext jar.
  const verificationCode = await sendVerificationCode(page.request);
  const loginResponse = await page.request.post(
    `${API_BASE_URL}/api/Security/LoginWithCookies`,
    { data: { email: TEST_EMAIL, browserId: TEST_BROWSER_ID, verificationCode } }
  );
  expect(loginResponse.ok()).toBeTruthy();

  // App bootstrap now: addInitScript seeds browser_id → app reads the
  // AuthResult cookie + sends the refresh cookie on RefreshTokenWithCookies →
  // authenticated layout renders.
  await page.goto('/');
  await page.locator('sidebar-menu').waitFor({ state: 'visible', timeout: 15000 });
}
