// Build a pre-filled GitHub "new issue" URL for the Spiderly repo.
//
// Why a script: hand-encoding a multi-line issue body into a query string is
// error-prone (every newline, space, #, & must be escaped). URLSearchParams
// does it correctly every time. This script ONLY builds and prints the URL —
// it never opens a browser and never files anything. The user copies the
// printed URL, opens it, reviews the pre-filled form, and clicks Submit.
//
// Usage:
//   echo "<markdown body>" | node build-issue-url.mjs --title "..." [--labels "a,b"]
//   node build-issue-url.mjs --title "..." --body-file ./body.md
//
// Flags:
//   --title       (required) plain issue title, NO "[agent-reported]" prefix
//                 (the label carries provenance)
//   --labels      (optional) comma-separated; default "agent-reported,enhancement"
//   --body-file   (optional) read body from a file instead of stdin
//
// Fails loudly (non-zero exit) on a missing title or empty body.

import { readFileSync } from "node:fs";

const REPO = "filiptrivan/spiderly"; // hardcoded: this skill reports Spiderly gaps only
const URL_SOFT_LIMIT = 7500; // GitHub returns 414 around ~8 KB; warn before we get there

function arg(name) {
  const i = process.argv.indexOf(`--${name}`);
  return i !== -1 ? process.argv[i + 1] : undefined;
}

function fail(message) {
  console.error(`build-issue-url: ${message}`);
  process.exit(1);
}

async function readStdin() {
  if (process.stdin.isTTY) return ""; // nothing piped in
  process.stdin.setEncoding("utf8");
  let data = "";
  for await (const chunk of process.stdin) data += chunk;
  return data;
}

const title = arg("title");
if (!title || !title.trim()) {
  fail('missing --title. Pass a plain title, e.g. --title "No hook to override generated validator message"');
}
if (/^\s*\[agent-reported\]/i.test(title)) {
  fail('drop the "[agent-reported]" prefix from --title — the label already carries provenance');
}

const labels = (arg("labels") ?? "agent-reported,enhancement").trim();

const bodyFile = arg("body-file");
let rawBody;
if (bodyFile) {
  try {
    rawBody = readFileSync(bodyFile, "utf8");
  } catch (err) {
    fail(`could not read --body-file "${bodyFile}": ${err.message}`);
  }
} else {
  rawBody = await readStdin();
}
const body = rawBody.trim();
if (!body) {
  fail("empty body. Pipe the markdown body on stdin, or pass --body-file <path>.");
}

const params = new URLSearchParams({ title: title.trim(), body });
if (labels) params.set("labels", labels);

const url = `https://github.com/${REPO}/issues/new?${params.toString()}`;

if (url.length > URL_SOFT_LIMIT) {
  console.error(
    `build-issue-url: WARNING — URL is ${url.length} chars (soft limit ${URL_SOFT_LIMIT}). ` +
      "GitHub returns 414 around 8 KB. Trim the body and post any code snippet as a comment instead."
  );
}

console.log(url);
