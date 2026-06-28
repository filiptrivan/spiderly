# Framework metadata — single source of truth

## Problem

Several framework facts are duplicated across three places that drift independently:

- **Code** — the authoritative C# / TS definitions (enums, attributes, controller endpoints, helpers).
- **Docs** — markdown tables in `claude-plugins/docs/**`.
- **Website docs** — MDX in the separate `spiderly-website` repo.

A 2026-05 audit found this is already real (e.g. the `authorization` skill's endpoint table was missing ~8 actions, `MatchModeCodes` was hand-listed in `filtering-patterns`, `ApiErrorCodes` was documented nowhere). The fix is to derive the docs from the code instead of hand-maintaining copies.

## The pipeline

```
Spiderly.MetadataExporter (C#)  ─┐
  reflects shipped assemblies +  │
  reads their .xml doc summaries │
                                 ├─► framework-metadata.json ─┬─► tools/gen-skill-docs.mjs ─► claude-plugins/docs/*/references/*.generated.md
tools/extract-ts-metadata.mjs   │     (committed; self-guarded) └─► (planned) spiderly-website imports + renders <ReferenceTable>
  ts-morph over the Angular lib ─┘
  MERGES TS facts into the JSON
```

- **`framework-metadata.json`** (repo root) is the single source of truth — a *committed* build artifact. The C# exporter writes it; the TS extractor reads it back and merges its sections; the renderer (and later the website) consume it.
- **Two trusted hops** (code → JSON, one per language); everything downstream derives from the JSON, so it can't drift from the JSON, and CI regenerates the JSON and fails on any `git diff`.
- **Descriptions** come free from the compiler-emitted XML doc files on the C# side (`GenerateDocumentationFile` is on in the shipped projects). The exporter **fails loudly**, listing *all* undocumented members at once, if any exported C# member lacks a `<summary>`. The TS side is **signature-first** (`helper-functions.ts` is largely undocumented; the signature is enough to prevent re-implementation), so descriptions there are optional.

## What's covered

| Contract | Source | Hosted in doc | Generated file |
|---|---|---|---|
| `ApiErrorCodes` | C# const-string class | authorization | `references/api-error-codes.generated.md` |
| `MatchModeCodes` | C# const-string class | filtering-patterns | `references/match-mode-codes.generated.md` |
| `UIControlTypeCodes` | C# enum | angular-customization | `references/ui-control-types.generated.md` |
| `SecurityBaseController` endpoints | C# reflection ([HttpX]/[AuthGuard]) | authorization | `references/security-endpoints.generated.md` |
| Spiderly attributes (45) | C# reflection (`Spiderly.Shared.Attributes.*`) | entity-design | `references/attributes.generated.md` |
| `helper-functions.ts` (30) | TS (ts-morph) | angular-customization | `references/helper-functions.generated.md` |
| `ValidatorAbstractService` (4) | TS (ts-morph) | angular-customization | `references/validators.generated.md` |
| `spiderly-*` controls (14) | TS (ts-morph) | angular-customization | `references/controls.generated.md` |

The C# exporter reads `UIControlTypeCodes` from **`Spiderly.Shared`** (the public contract), never the `Spiderly.SourceGenerators` copy that carries extra internal values (`Table`, `None`) — reading the right assembly *is* the public/internal filter.

## Regenerate

```bash
tools/regen-metadata.sh   # optional arg: build configuration (defaults to Debug; CI passes Release)
```

The script runs the three hops in order — the C# exporter (writes the JSON), `tools/extract-ts-metadata.mjs` (merges TS facts), `tools/gen-skill-docs.mjs` (renders `*.generated.md`) — running `npm ci` in `tools/` first if `node_modules` is missing (ts-morph). Output is deterministic (members sorted ordinally; LF line endings) so re-running is byte-identical — required for the diff guard.

## CI self-guard

`.github/workflows/ci.yml` → `unit-test` job installs `tools/` deps, regenerates all three artifacts, and runs `git diff --exit-code`. A contract changed in code without regenerating turns CI red, naming the regenerate command.

## Local pre-commit guard

`.githooks/pre-commit` mirrors the CI check so staleness fails at commit time instead of after a CI round-trip. It is gated: it inspects the staged file list and only runs the (dotnet-build-backed) regeneration when SSOT sources, pipeline files, or the artifacts themselves are touched — unrelated commits pay zero overhead. On staleness it regenerates and **auto-stages the artifacts into the commit** (the output is deterministic and derived, so there is nothing to review). It blocks instead in one case: when SSOT sources have unstaged or untracked changes, because the regen runs against the working tree and auto-staging would commit artifacts derived from sources outside the commit. It also runs `TsContractMirrorTests` when the hand-maintained C#↔TS mirror files are staged, catching `ApiErrorCodes`/`MatchModeCodes` divergence locally. Activate once per clone (git does not auto-install hooks):

```bash
git config core.hooksPath .githooks
```

Bypass for emergencies with `git commit --no-verify` — CI remains the backstop.

## Adding a new contract

**C# (enum / const class / controller / attribute):** add the type to the relevant list in `Spiderly.MetadataExporter/Program.cs`. Every exported member must have a `/// <summary>` or the exporter fails (listing all gaps).

**TypeScript:** add extraction to `tools/extract-ts-metadata.mjs` (ts-morph).

Either way: add the doc placement in `tools/gen-skill-docs.mjs` (it fails loud if a contract has no placement), add a one-line pointer from the host `index.md`, extend the `ssot_src` trigger regex in `.githooks/pre-commit` with the new source path, then regenerate + commit.

## Cross-language mirrors

`ApiErrorCodes` and `MatchModeCodes` are hand-maintained in TypeScript too (`api-error-codes.ts`, `match-mode-enum-codes.ts`); the Angular admin and storefronts switch on the wire values. Rather than *generate* those (which would reorder the enums away from their readable declaration order for no functional gain on a string contract), `Spiderly.Shared.Tests/TsContractMirrorTests` asserts the C# ↔ TS member sets match, so any divergence fails CI. The **downstream** storefront mirror in a consuming app lives in a separate repo — out of scope here, same as the website.

## Distribution to spiderly-website

The metadata deliberately carries **no `version` field** — it's decoupled from the release version bump so the byte-diff guard stays stable across `chore: bump version` commits (which the CI guard already skips); consumers stamp the version from the release tag instead.

On a **stable** release, `release.yml`'s `finalize` job pushes the committed `framework-metadata.json` into `spiderly-website` at `src/lib/framework-metadata.json` (reusing `PAT_TOKEN`, just before the existing develop→master merge so it ships in the same release; `continue-on-error` so a docs-sync hiccup never fails the release). No regeneration is needed in the release — the committed JSON is release-accurate.

## Roadmap (remaining)

- **Slice 4 (consume side)** — in `spiderly-website`, a `<ReferenceTable>` component imports `@/lib/framework-metadata.json` and renders the reference tables in the MDX docs. No drift guard needed there — it renders from committed data.
