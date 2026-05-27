---
name: e2e-testing
description: End-to-end testing a Spiderly app with Playwright — log in via the dev-mode verification-code helper, navigate PrimeNG v19 selector quirks, debug failing CI runs from trace artifacts, and seed/clean test data. Use when writing Playwright tests against a Spiderly app, automating login from tests, debugging selectors that won't match, or pulling trace screenshots from a failed CI run.
---

# E2E Testing

## Logging in from a test (no SMTP needed)

Spiderly's `SendLoginVerificationEmail` endpoint returns the verification code in the response body when `ShouldShowVerificationCodeInNotification()` returns true. The gate is `IWebHostEnvironment.IsDevelopment() && !emailingService.IsConfigured()` — it's on **only** when the backend runs in the Development environment **and** SMTP is not fully configured. `IsConfigured()` requires all four of `EmailSender.Email`, `EmailSenderPassword`, `SmtpHost`, and `SmtpPort > 0`; if all four are present, the backend does a **real email send** instead — even in Development — and `verificationCode` is absent from the response. So run the test backend with `ASPNETCORE_ENVIRONMENT=Development` and SMTP left unconfigured. That lets a test complete the 2FA flow without ever sending an email.

```ts
import { APIRequestContext, Page, expect } from '@playwright/test';

// Scaffold default only. The authoritative backend URL is the origin of `apiUrl`
// in Frontend/src/environments/environment.ts (strip the trailing /api); the bound
// port is in Backend/<App>.WebAPI/Properties/launchSettings.json -> applicationUrl.
const API_BASE_URL = 'http://localhost:5000';

export async function login(request: APIRequestContext) {
  const sendCodeResponse = await request.post(
    `${API_BASE_URL}/api/Security/SendLoginVerificationEmail`,
    { data: { email: 'test@e2e.com', browserId: 'e2e-browser' } }
  );
  expect(sendCodeResponse.ok()).toBeTruthy();
  const { verificationCode } = await sendCodeResponse.json();
  expect(verificationCode).toBeTruthy();

  const loginResponse = await request.post(
    `${API_BASE_URL}/api/Security/Login`,
    { data: { email: 'test@e2e.com', browserId: 'e2e-browser', verificationCode } }
  );
  expect(loginResponse.ok()).toBeTruthy();
  return loginResponse.json(); // { accessToken, refreshToken }
}

export async function authenticateBrowser(page: Page, request: APIRequestContext) {
  const tokens = await login(request);
  await page.goto('/');
  await page.evaluate(([at, rt]) => {
    localStorage.setItem('access_token', at);
    localStorage.setItem('refresh_token', rt);
    localStorage.setItem('browser_id', 'e2e-browser');
  }, [tokens.accessToken, tokens.refreshToken]);
  await page.reload();
  await page.locator('sidebar-menu').waitFor({ state: 'visible', timeout: 15000 });
  return tokens;
}
```

The two conditions above are the only levers — `ShouldShowVerificationCodeInNotification()` is `private`, so you can't override it on your `SecurityService`. To **get** the code in the response (tests, local dev): run with `ASPNETCORE_ENVIRONMENT=Development` and no complete SMTP config. To **turn it off** (production, or any environment that must send real emails): run a non-Development environment, or fully configure SMTP. Without `ASPNETCORE_ENVIRONMENT=Development` the code is never returned, regardless of SMTP config.

## PrimeNG v19 selector pitfalls

Spiderly's admin UI is built on PrimeNG v19. A few selectors that look obvious from the docs do not work — match what's actually rendered.

- **Filter Apply / Clear buttons have no identifying class.** PrimeNG's documented `pcFilterApplyButton` / `pcFilterClearButton` style classes are not applied to the rendered `<p-button>` elements. Match by accessible name:
  ```ts
  overlay.getByRole('button', { name: 'Apply' })
  ```
- **Match-mode dropdown is `<p-select>`, not `<p-dropdown>`** — PrimeNG renamed Dropdown to Select in v19. Spiderly's `<spiderly-dropdown>` wraps `<p-select>` internally.
- **Boolean filter is `<p-checkbox [binary]="true" [indeterminate]="value === null">`**, not `pTriStateCheckbox`. Initial state is `null` (rendered as a horizontal dash); each click cycles `null → true → false → null`.
- **Filter overlays for the rightmost column get clipped against the viewport.** PrimeNG repositions the overlay frame-by-frame, so Playwright's stability check on inner elements fails (`waiting for element to be visible, enabled and stable`). Pass `click({ force: true })` to bypass the stability gate. Apply/Clear buttons (matched by role) do not need this — only the elements *inside* the overlay (e.g. `.p-checkbox-box`).

## Match-mode column configuration

For column-config behavior (when the match-mode dropdown renders, how labels resolve), see `Angular/projects/spiderly/src/lib/components/spiderly-data-table/CLAUDE.md`. Two points that commonly bite test authors:

- **Numeric and date columns need `showMatchModes: true`** on the `Column<T>` for the match-mode `<p-select>` to render at all. Without it the match-mode UI is silently absent and Playwright selectors for "More than" / "Less than" will time out.
- **Match-mode option labels are transloco output** (`'More than'`, `'Less than'`), not `MatchModeCodes` keys. Match Playwright selectors against the value in your `en.json` (or the locale your test runs under).

## Test data: seed and clean

Tests should own their data. Two patterns:

- **Per-suite seed in `beforeAll` / cleanup in `afterAll`** — when multiple tests in the same `describe` reuse the same fixtures.
- **In-test seed + describe-scoped cleanup array + `afterAll`** — when only one test needs the data. Track inserted IDs in an array and tear them down at the end.

Always use `Promise.all` for seed and cleanup batches. Sequential 40× HTTP round-trips noticeably slow CI; the database has no problem with the concurrency.

## Generated lists ship with the Id column only

`spiderly add-new-entity` produces a list component with a single numeric Id column plus Details/Delete actions. If your test needs to drive text/numeric/boolean filters, you have two options:

1. **Extend the list component** in your app — add the columns you want to filter on (text, numeric, boolean). The Spiderly admin then has the filter UI your test can target.
2. **Drive filtering through the API directly** — call the paginated-list endpoint with a `FilterDTO` payload and assert on the response, skipping the UI. Faster, less brittle, but doesn't exercise the column-config code path.

## Debugging a failing Playwright test in CI

When a selector times out the failure log rarely shows enough context. Pull the trace artifact and look at the last screenshot — it reveals whether the element is missing, off-screen, occluded, or labeled differently than expected.

```bash
gh run download <run-id> --dir /tmp/ci-<run-id>
cd /tmp/ci-<run-id>/playwright-report/data
for z in *.zip; do unzip -o -d /tmp/traces/"${z%.zip}" "$z"; done
# Find the trace folder for the failing test (replace <spec-file>:<line>):
for d in /tmp/traces/*/; do grep -lc "<spec-file>:<line>" "$d"*.trace 2>/dev/null | head -1; done
# View the last screenshot in that folder:
ls /tmp/traces/<picked>/resources/ | grep jpeg | sort | tail -1
```
