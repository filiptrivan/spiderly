---
name: verify-ui
description: Log past Spiderly's email-code auth wall to visually verify the running Angular admin panel — screenshot a page, click through a flow, or dogfood a UI change without writing a test. Use when asked to "verify the admin UI", "check this page renders", "screenshot the admin panel", "does this screen look right", or to eyeball a change in the live app. For authoring Playwright test suites or debugging CI traces, use the e2e-testing skill instead.
allowed-tools: Bash(node:*), Bash(npx:*), Bash(agent-browser:*), Bash(curl:*)
---

# Verify Spiderly Admin UI (past the auth wall)

The Spiderly admin panel sits entirely behind a passwordless **email-code** login, which blocks any agent that just opens `localhost:4200`. This skill gets you a logged-in session headlessly so you can look at the real UI.

**This is for ad-hoc visual verification, not test authoring.** If you're writing a Playwright suite or debugging a failing CI run, use the `e2e-testing` skill — it owns the canonical login helper and the PrimeNG selector quirks.

## Two auth flows — check which one first

- **Header flow** (`Login` + `RefreshTokenWithHeaders`): tokens in localStorage → recipe below.
- **Cookie flow** (`LoginWithCookies` + `RefreshTokenWithCookies`): the refresh token is an HttpOnly cookie — localStorage injection can't work. Symptom: bearer fetches return 200, yet every load bounces to `/login` with a 401 on `RefreshTokenWithCookies`. Log in **from inside the browser** with `credentials: 'include'` instead:

```js
// eval on the admin origin; email must have the Admin role
const browserId = 'verify-ui';
localStorage.setItem('browser_id', browserId); // must match the login below
const { verificationCode } = await (await fetch(API + '/api/Security/SendLoginVerificationEmail', {
  method: 'POST', headers: {'Content-Type':'application/json'}, credentials: 'include',
  body: JSON.stringify({ email, browserId }) })).json();
await fetch(API + '/api/Security/LoginWithCookies', { method: 'POST',
  headers: {'Content-Type':'application/json'}, credentials: 'include',
  body: JSON.stringify({ verificationCode, email, browserId }) });
// reload — boot's RefreshTokenWithCookies now finds the cookie
```

## The core idea — header flow (driver-agnostic)

In development, `SecurityService.SendLoginVerificationEmail` returns the verification code **in the response body** (when `ShouldShowVerificationCodeInNotification()` is true — i.e. `IWebHostEnvironment.IsDevelopment()` **and** SMTP is not fully configured, so `emailingService.IsConfigured()` is false). So no email inbox is needed. The flow is always:

1. `POST /api/Security/SendLoginVerificationEmail` `{ email, browserId }` → read `verificationCode`.
2. `POST /api/Security/Login` `{ verificationCode, email, browserId }` → get `accessToken` + `refreshToken`.
3. In the browser, set three `localStorage` keys on the admin origin, then reload:
   - `access_token`, `refresh_token`, `browser_id` (the last must equal the `browserId` used above).
4. Wait for `sidebar-menu` to appear — that confirms you're authenticated and in.

Step 1+2 are scripted for you: **`scripts/get-admin-token.mjs`** (pure Node, no dependencies, fails loudly).

```bash
node scripts/get-admin-token.mjs --email admin@example.com
# → { "accessToken": "...", "refreshToken": "...", "browserId": "verify-ui" }
```

Flags: `--email` (required — must have the **Admin role**, see gotcha below), `--api` (default `http://localhost:5000`), `--browser-id` (default `verify-ui`).

> **Don't trust the default ports — read them from the project.** The `:5000` (backend) and `:4200` (admin) values used throughout this skill are only the Spiderly scaffold defaults; any project can change them. The authoritative values live in **`Frontend/src/environments/environment.ts`**:
> - `apiUrl` (e.g. `http://localhost:5000/api`) → pass its **origin** (strip the trailing `/api`) to `--api`.
> - `frontendUrl` (e.g. `http://localhost:4200`) → the admin URL to open in the browser.
>
> The backend's actually-bound port is in **`Backend/<App>.WebAPI/Properties/launchSettings.json`** → `applicationUrl`. If a connection fails or the project looks non-standard, read those files first instead of assuming the defaults below.

> **CORS origin must match.** The backend allows one origin (`appsettings.json` → `Spiderly.Shared:FrontendUrl`). Serving the admin on a different port blocks every API call and surfaces as a misleading **"Connection Lost"** toast + bounce to `/login` (looks like an auth failure).

## Prerequisites — validate before doing anything

The token script already fails loudly on the first two. Confirm all four up front so you don't burn a turn on a half-running stack:

| Requirement | How to confirm | If it fails |
|---|---|---|
| Backend reachable | the script's first POST succeeds (port from `launchSettings.json` → `applicationUrl`) | start the backend (`dotnet run` in the WebAPI project) |
| Dev-mode code exposure on | response contains `verificationCode` | run backend with `ASPNETCORE_ENVIRONMENT=Development` **and** SMTP unconfigured (`IsConfigured()` false) — a fully-configured SMTP setup does a real send even in Development |
| Admin SPA running | `frontendUrl` (from `environment.ts`, default `:4200`) responds | start the admin (`npm start` in `Frontend/`) |
| Account has Admin role | login lands on a populated sidebar, not an empty shell | use an Admin-roled email (see gotcha) |

## Driver A — agent-browser (preferred when available)

[agent-browser](https://www.skills.sh/vercel-labs/agent-browser/agent-browser) gives an agent live, interactive control — ideal for clicking through a flow and screenshotting. If it isn't installed, install once (or skip to Driver B):

```bash
npx skills add https://github.com/vercel-labs/agent-browser --skill agent-browser
npm i -g agent-browser && agent-browser install   # CLI + Chrome binary
```

Then log in and verify (replace `<AT>` / `<RT>` with the script's output, and `verify-ui` if you changed `--browser-id`):

```bash
# 1. Open the admin origin first so localStorage writes land on the right origin
agent-browser open http://localhost:4200

# 2. Inject the session (use --stdin so quoting can't corrupt the values)
agent-browser eval --stdin <<'EOF'
localStorage.setItem('access_token', '<AT>');
localStorage.setItem('refresh_token', '<RT>');
localStorage.setItem('browser_id', 'verify-ui');
EOF

# 3. Navigate straight to the page under review — this load reads the just-set
#    localStorage; the sidebar-menu wait confirms auth (it renders on every
#    authenticated route, so no separate dashboard round-trip is needed).
agent-browser open http://localhost:4200/administration/banner \
  && agent-browser wait "sidebar-menu" \
  && agent-browser wait --load networkidle \
  && agent-browser screenshot --full
```

From here use the normal agent-browser loop (`snapshot -i` → `click`/`fill` → re-snapshot) to drive a flow. Close the session when done: `agent-browser close`.

## Driver B — Playwright (guaranteed floor, zero extra install)

Every Spiderly app ships Playwright for e2e, so this always works without installing anything new. Run it **from the `Frontend/` directory** so `playwright` resolves. It reuses the same token script.

Save as `Frontend/verify-page.mjs` (gitignore it — it's a throwaway), then:

```bash
node verify-page.mjs --email admin@example.com \
  --url http://localhost:4200/administration/banner \
  --token-script <skill-dir>/scripts/get-admin-token.mjs --out ./_verify.png
```

It reuses the same token helper (`--token-script`, required — pass the absolute path to this skill's `scripts/get-admin-token.mjs`), so the localStorage key names and `sidebar-menu` wait stay defined in one place. Keep that injection block in sync with `e2e-testing`'s `authenticateBrowser` if either changes.

```js
import { chromium } from "playwright";
import { execFileSync } from "node:child_process";

function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i !== -1 ? process.argv[i + 1] : fallback;
}

const email = arg("email");
const url = arg("url", "http://localhost:4200");
const api = arg("api", "http://localhost:5000");
const out = arg("out", "./_verify.png");
const tokenScript = arg("token-script");
if (!email || !tokenScript) {
  throw new Error("Pass --email <admin-roled account> and --token-script <path to get-admin-token.mjs>");
}

const { accessToken, refreshToken, browserId } = JSON.parse(
  execFileSync("node", [tokenScript, "--email", email, "--api", api], { encoding: "utf8" })
);

const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  // Tokens must be set on the admin origin before the app reads them, so land there first.
  await page.goto(new URL(url).origin);
  await page.evaluate(
    ([at, rt, bid]) => {
      localStorage.setItem("access_token", at);
      localStorage.setItem("refresh_token", rt);
      localStorage.setItem("browser_id", bid);
    },
    [accessToken, refreshToken, browserId]
  );
  await page.goto(url);
  await page.locator("sidebar-menu").waitFor({ state: "visible", timeout: 15000 });
  await page.screenshot({ path: out, fullPage: true });
  console.log(`Saved ${out}`);
} finally {
  await browser.close();
}
```

## Gotcha — login can succeed but show an empty shell

`SendLoginVerificationEmail` mints a session for any email when `OnlyAdminCanAddUsers` is `false` (the default) — a not-yet-existing user is **auto-provisioned with no role**, so the sidebar comes up empty and most pages are inaccessible. If you can log in but see nothing useful, the account lacks permissions. Use an email that already has the **Admin role** (role/permission assignment is seeded per-project, e.g. via a `PermissionSeeder`). Permissions hang off roles, not off the user directly.

## Why a separate skill from e2e-testing

Same login mechanism, different job: `e2e-testing` is for *authoring* Playwright suites and debugging CI traces; this is for *looking at* the running admin UI ad-hoc. Keeping them separate keeps each skill's trigger sharp. The login flow lives in both only as a thin reference — `e2e-testing` holds the canonical Playwright helper (`authenticateBrowser`); this skill holds the dependency-free token script and the agent-browser path.
