---
name: report-gap
description: Use the moment you're forced into a workaround because the clean Spiderly-native path didn't exist — you looked for a lifecycle hook, an override, an entity attribute, or a generator option and it was structurally missing, so you had to bypass or copy generated code, or reach into framework internals. Also use when the gap is in Spiderly's own skills, plugins, or docs — a skill that should have fired but didn't, or skill/doc content that was missing, stale, or wrong for the case you hit. Turns that gap into a pre-filled GitHub issue URL against filiptrivan/spiderly that the user copies and submits. Also invoke manually to report a Spiderly limitation. NOT for hacks in the consumer's own business logic, NOT for ordinary bugs in your own code.
allowed-tools: Bash(node:*)
---

# Report a Spiderly gap

When Spiderly forces you into a workaround — or its own guidance lets you down — that's signal the maintainer needs. This skill captures the gap as a **pre-filled GitHub issue URL** against `filiptrivan/spiderly`. It does **not** file anything — it prints a URL the user opens, reviews, and submits themselves.

## When this fires (and when it doesn't)

The trigger is sharp on purpose. Two shapes of gap qualify:

**A framework-code gap** — *all* of these are true:

- You wanted to do something the Spiderly way (a hook, an override, an entity attribute, a generator/CLI option).
- You looked for that clean path and it was **structurally missing** — not just unfamiliar.
- So you fell back to a hack: bypassing or **copying generated code to edit it**, reaching into framework internals, or duplicating logic Spiderly should own.

**A gap in Spiderly's own skills, plugins, or docs** — the guidance layer itself failed you:

- A Spiderly skill should have fired for your task but its description didn't match, so you worked blind until someone pointed you at it.
- Skill or doc content you followed was **missing the case you hit, stale** against current Spiderly behavior, **or outright wrong** — and following it cost you a wrong turn.
- The framework **had** the feature your task needed, but the topic's doc never mentions it — so you hand-rolled a duplicate or picked a worse seam until someone pointed at the built-in (e.g. an undocumented generated-template projection slot). The feature existing makes it a **documentation** gap, not an enhancement — still file it.

For these, use `--labels "agent-reported,documentation"` and name the skill/doc file in the body. The same sharpness applies: "the docs could say more about X" is not a gap; "the docs told me the wrong thing / the skill never fired / the doc omits the feature entirely" is.

Do **NOT** use it for:

- Workarounds in the **consumer's own business logic** — that's the consumer's problem, not a Spiderly gap.
- Ordinary bugs in code you wrote, or a clean path you simply hadn't found yet (look harder first). The boundary: if the relevant topic's doc **does cover** the path and you didn't read it, the miss is yours — no issue; if the docs bundle **never mentions it**, that's the documentation-gap shape above.
- **Non-interactive sessions** (a subagent, CI, a scheduled run): there's no one to copy the URL. Just note the gap in your final output and move on — never block.

## Flow

1. Confirm it's a real Spiderly gap against the bar above.
2. Fill the six-section body template (below), **sanitized**.
3. Build the URL with the helper script.
4. Print a short summary **and the URL** to the user. They copy it, open it, review the pre-filled form, and click Submit. Nothing is filed until they do.
5. If there's a workaround code snippet, **show it in chat** and tell the user to paste it as the first comment after the issue opens — keep it **out of the URL** (see the length gotcha).

## Be ruthlessly concise

Brevity is the whole game here — it keeps the URL well under the 414 ceiling **and** means a single short comment instead of several. Concretely:

- **One or two sentences per section.** No filler, no restating the title, no hedging. The maintainer reads fast.
- **Trim any snippet to the 1–3 lines that actually show the gap** — not the whole class. A trimmed snippet usually fits in **one** comment; never spread it across multiple.
- If everything is tight enough, you won't need a comment at all. Prefer that.

## Body template

Fill every section, one or two sentences each — prose only, no code snippet (that goes in a single comment if needed).

```markdown
### What I was trying to do
<the clean goal, one or two sentences>

### The clean Spiderly approach I expected
<the hook / override / attribute / option you looked for>

### Why it didn't work
<what was structurally missing>

### The workaround I used instead
<the hack, described in prose — code snippet posted as a comment below>

### Suggested fix
<what Spiderly could add: a new hook, attribute, generator option, ...>

### Context
Spiderly version: <from the consumer's package.json / .csproj>. Project type: <e.g. .NET 9 + Angular 19>. (No proprietary code included.)
```

## Sanitize — this is a public repo

The issue lands in a **public** repository, and you're running inside someone's (possibly private) codebase. Before building the URL, strip: secrets/keys, real entity/table/field names that reveal the business, customer data, internal URLs. Describe the *shape* of the problem, not the proprietary specifics. When in doubt, generalize.

## Build the URL

Title is a **plain summary with no `[agent-reported]` prefix** — the label carries provenance. Pipe the body on stdin:

```bash
node scripts/build-issue-url.mjs \
  --title "No hook to override generated validator message" \
  --labels "agent-reported,enhancement" <<'EOF'
### What I was trying to do
...
### Context
Spiderly version: 19.8.2. Project type: .NET 9 + Angular 19.
EOF
```

It prints the encoded `https://github.com/filiptrivan/spiderly/issues/new?...` URL. Defaults: `--labels` is `agent-reported,enhancement`; the repo is hardcoded. The script does **not** open a browser — print the URL for the user to copy.

> **Why a script instead of building the URL by hand:** encoding a multi-line body into a query string by hand (`%0A` for every newline, escaping `#`, `&`, spaces) is error-prone, and one wrong escape gives the user a broken form. `URLSearchParams` gets it right every time. If Node is somehow unavailable, encode by hand as a last resort — but Node ships with every Spiderly app.

## Gotcha — URL length (414)

GitHub returns **414 URI Too Long** around **8 KB**, and URL-encoding inflates the body ~3×. A code snippet in the body blows past this fast. So the snippet **never goes in the URL** — show it in chat and have the user paste it as a single comment. The script warns on stderr if the URL crosses ~7500 chars; if you see that warning, you wrote too much — **cut the prose down**, don't move it to comments.

## The `agent-reported` label

The pre-filled `&labels=agent-reported` only sticks if that label **exists** in the repo — GitHub silently drops unknown labels (you'd still get `enhancement`). **In `filiptrivan/spiderly` the label already exists — there is no setup left to do, so never prompt the maintainer to create it.** Only when targeting a fork that lacks it, create it once (authenticated `gh`):

```bash
gh label create agent-reported \
  --repo <owner>/<fork> \
  --color BFD4F2 \
  --description "Gap surfaced by a coding agent forced into a Spiderly workaround"
```
