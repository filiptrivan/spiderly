import { test, expect } from '@playwright/test';
import { authenticateBrowser } from '../helpers/auth';

// RouteEchoController sets [Route("/api/Echo/Route/[action]")]. The generated API service must call that URL,
// and not a URL from the class name (NgControllersGenerator.GetControllerUrlPath).
// query-param-echo.component.ts makes the call.
test('the generated API service calls the URL that the controller [Route] gives', async ({ page, request }) => {
  await authenticateBrowser(page, request);

  const echoRequest = page.waitForRequest((r) => r.url().includes('/api/Echo/Route/EchoControllerRoute'));
  await page.goto('/query-param-echo');

  await echoRequest;
  await expect(page.getByTestId('echoed-controller-route')).toHaveText('served');
});
