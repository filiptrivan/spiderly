import { Page, expect, APIRequestContext } from '@playwright/test';

export const API_BASE_URL = 'http://localhost:5000';

export async function login(request: APIRequestContext): Promise<{ accessToken: string; refreshToken: string }> {
  const sendCodeResponse = await request.post(
    `${API_BASE_URL}/api/Security/SendLoginVerificationEmail`,
    { data: { email: 'test@e2e.com', browserId: 'e2e-browser' } }
  );
  expect(sendCodeResponse.ok()).toBeTruthy();
  const sendCodeResult = await sendCodeResponse.json();
  const verificationCode = sendCodeResult.verificationCode;
  expect(verificationCode).toBeTruthy();

  const loginResponse = await request.post(
    `${API_BASE_URL}/api/Security/Login`,
    {
      data: {
        verificationCode,
        email: 'test@e2e.com',
        browserId: 'e2e-browser',
      },
    }
  );
  expect(loginResponse.ok()).toBeTruthy();
  const loginResult = await loginResponse.json();
  expect(loginResult.accessToken).toBeTruthy();

  return {
    accessToken: loginResult.accessToken,
    refreshToken: loginResult.refreshToken,
  };
}

export async function authenticateBrowser(page: Page, request: APIRequestContext): Promise<{ accessToken: string; refreshToken: string }> {
  const tokens = await login(request);

  await page.goto('/');
  await page.evaluate(([at, rt]: [string, string]) => {
    localStorage.setItem('access_token', at);
    localStorage.setItem('refresh_token', rt);
    localStorage.setItem('browser_id', 'e2e-browser');
  }, [tokens.accessToken, tokens.refreshToken]);
  await page.reload();
  await page.locator('sidebar-menu').waitFor({ state: 'visible', timeout: 15000 });

  return tokens;
}
