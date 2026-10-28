## What is Spiderly

Spiderly is a .NET 9 + Angular 19 code generator. It reads EF Core entity classes decorated with custom attributes and generates: CRUD UI (Angular), API controllers, services, DTOs, mappers, FluentValidation rules, Angular validators, TypeScript entity classes, and more. Users extend generated base classes with custom logic.

Review a Spiderly or spiderly-website change against `CODING_STANDARDS.md` as well: it holds the rules a reviewer judges from the diff, and the rows below point into it.

Spiderly is a fast-moving startup — no backward compatibility needed. Make breaking changes freely.

Spiderly is a framework for any app, not for one. Even while PACMS is its only user, don't add code, names, settings or rules that exist for a single consumer. A feature belongs here only if any Spiderly app could use it as it stands. A consumer's own policy stays in the consumer. For example, PACMS banning `DateTime.UtcNow` stays in PACMS, while the `TimeProvider` that the stamps read is Spiderly's (Filip, 2026-10-21).

Every feature must be drivable by an AI agent without human intervention: the complete design principles are the `ai-agentic-design` skill (`.claude/skills/ai-agentic-design/SKILL.md`), contributor-only, intentionally kept out of the consumer-shipped `claude-plugins/skills/`.

Activate the hooks once per clone, `git config core.hooksPath .githooks`: the gated pre-commit then regenerates and stages the framework-metadata and agent-bundle artifacts a commit's sources change, and runs the C#↔TS mirror test.

## Where the rest lives

Each row is a branch only some sessions reach: its trigger, then its home.

| Key | When, and where |
|---|---|
| Config ↔ options binding is a reflective contract — guard it | Adding or reshaping an option: `CODING_STANDARDS.md` → "Options binding" (why a drifted shape binds silently: the summary on `OptionsBindingTests`). |
| Versioning | Bumping a version: `CODING_STANDARDS.md` → "Versions". A consumer app moving from one Spiderly release to another: the `spiderly-upgrade` skill, `claude-plugins/skills/spiderly-upgrade/SKILL.md`. |
| Documentation updates | A change that affects public API, attributes, generated output, or behavior: update the documentation in the `spiderly-website/` sibling repo accordingly. |
| API error codes | Adding, removing or renaming an `ApiErrorCodes` code (the `errorCode` of `ApiErrorDTO`) changes its Angular mirror in the same commit, pinned by `TsContractMirrorTests`; a consuming app's own TS copy (a storefront's api-error.ts, say) is a third mirror nothing here sees: `docs/framework-metadata-ssot.md` → "Cross-language mirrors". |
| Framework metadata SSOT — regenerate after contract changes | Changing any covered contract (`docs/framework-metadata-ssot.md` → "What's covered", plus the shape-axis data in `SpiderlyTypeRef`), **including only editing its XML `<summary>` docs**: run `tools/regen-metadata.sh`, then `node tools/build-agent-bundle.mjs`, and commit the artifacts they rewrite in the same commit. Which artifacts, and the pipeline: `docs/framework-metadata-ssot.md`. |
| Agent guidance bundle — regenerate after skill changes | Changing a doc or skill under `claude-plugins/`, or anything under `Angular/projects/spiderly/agent/`: `.claude/rules/agent-guidance-bundle.md`. |
| Coding conventions | C# conventions, naming, and the classification attribute a hand-written class carries: `CODING_STANDARDS.md` → "C# conventions". |
| Bundle co-required registrations behind one call | Registering services that only work together: `CODING_STANDARDS.md` → "Bundle co-required registrations behind one call". |
| Init template drift | Changing a framework public API (a DI registration, a `SpiderlyBuilder` method, a generated constructor, a built-in service): `CODING_STANDARDS.md` → "Init template drift". |
| `.spiderly/` project config | Changing an app's per-project config, under `.spiderly/` at the app root (it replaces the former root-level `spiderly.json`; `spiderly-upgrade` migrates existing apps). `.spiderly/config.json` (committed) is the `SpiderlyConfig` the source generators read via `AdditionalFiles`, registered in the generating project's csproj as `<AdditionalFiles Include=".spiderly/config.json" />` (the matcher: `Extensions.GetSpiderlyConfig`). `.spiderly/config.local.json` (gitignored) holds machine-local overrides, today `spiderly agent-sync`'s workspace root: `docs/agent-guidance-distribution.md`, its `--agent-root` section. |
| Regression tests must fail on the commit that adds them | Writing a regression test, or a detector (a harness, gate, lint rule or diagnostic): `CODING_STANDARDS.md` → "Tests". |
| Pull requests | A pull request touching `.github/workflows/**` cannot pass `claude-review`, since GitHub refuses the review app's token on a workflow diff: a maintainer admin-merges it. |
