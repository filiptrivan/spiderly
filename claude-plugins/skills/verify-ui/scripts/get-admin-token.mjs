#!/usr/bin/env node
// Obtain a Spiderly admin session token without an email inbox.
//
// In development, SecurityService.SendLoginVerificationEmail returns the
// verification code directly in the response body (when
// ShouldShowVerificationCodeInNotification() is true). This script uses that
// to complete the passwordless login flow headlessly and print the tokens an
// admin SPA expects in localStorage.
//
// The --api default (http://localhost:5000) is the Spiderly scaffold default only.
// The authoritative backend URL for a project is the origin of `apiUrl` in
// Frontend/src/environments/environment.ts (strip the trailing /api); the bound
// port is in Backend/<App>.WebAPI/Properties/launchSettings.json -> applicationUrl.
// Pass --api explicitly when a project overrides the default.
//
// Usage:
//   node get-admin-token.mjs --email admin@example.com
//   node get-admin-token.mjs --email admin@example.com --api http://localhost:5000 --browser-id verify-ui
//
// Output (stdout, JSON): { "accessToken": "...", "refreshToken": "...", "browserId": "..." }
// Exit code 0 on success, 1 on any failure (fails loudly — no partial success).

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];
    if (!token.startsWith("--")) continue;

    // Support both --flag=value and --flag value.
    const eq = token.indexOf("=");
    if (eq !== -1) {
      args[token.slice(2, eq)] = token.slice(eq + 1);
      continue;
    }

    const key = token.slice(2);
    const next = argv[i + 1];
    if (next !== undefined && !next.startsWith("--")) {
      args[key] = next;
      i++;
    } else {
      args[key] = true;
    }
  }
  return args;
}

function fail(message) {
  console.error(`[get-admin-token] ${message}`);
  process.exit(1);
}

const args = parseArgs(process.argv.slice(2));

const email = args.email;
const apiBaseUrl = (args.api || "http://localhost:5000").replace(/\/+$/, "");
const browserId = args["browser-id"] || "verify-ui";

if (!email || email === true) {
  fail(
    "Missing required --email. Pass an account that has the Admin role in the target DB.\n" +
      "  Usage: node get-admin-token.mjs --email admin@example.com [--api http://localhost:5000] [--browser-id verify-ui]"
  );
}

async function postJson(path, body) {
  let response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
  } catch (err) {
    fail(
      `Could not reach the backend at ${apiBaseUrl}${path}. Is the backend running?\n  ${err.message}`
    );
  }

  const text = await response.text();
  let json;
  try {
    json = text ? JSON.parse(text) : {};
  } catch {
    json = { raw: text };
  }

  if (!response.ok) {
    fail(`POST ${path} failed (HTTP ${response.status}): ${text || "(empty body)"}`);
  }

  return json;
}

const sendCodeResult = await postJson("/api/Security/SendLoginVerificationEmail", {
  email,
  browserId,
});

const verificationCode = sendCodeResult.verificationCode;
if (!verificationCode) {
  fail(
    "SendLoginVerificationEmail did not return a verificationCode.\n" +
      "  This means dev-mode code exposure is OFF (ShouldShowVerificationCodeInNotification() returned false).\n" +
      "  It defaults to IWebHostEnvironment.IsDevelopment(). Run the backend in Development, or override\n" +
      "  ShouldShowVerificationCodeInNotification() on your SecurityService for this environment."
  );
}

const loginResult = await postJson("/api/Security/Login", {
  verificationCode,
  email,
  browserId,
});

if (!loginResult.accessToken || !loginResult.refreshToken) {
  fail(
    `Login did not return tokens. Response: ${JSON.stringify(loginResult)}\n` +
      "  The email may not exist, or the code expired. Check the account and retry."
  );
}

process.stdout.write(
  JSON.stringify(
    {
      accessToken: loginResult.accessToken,
      refreshToken: loginResult.refreshToken,
      browserId,
    },
    null,
    2
  ) + "\n"
);
