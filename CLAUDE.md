## What is Spiderly

Spiderly is a .NET 9 + Angular 19 code generator. It reads EF Core entity classes decorated with custom attributes and generates: CRUD UI (Angular), API controllers, services, DTOs, mappers, FluentValidation rules, Angular validators, TypeScript entity classes, and more. Users extend generated base classes with custom logic.

Review a Spiderly or spiderly-website change against `CODING_STANDARDS.md` as well: it holds the rules a reviewer judges from the diff, so a writer opens it at its triggers too: C# naming and the classification attribute a hand-written class carries ("C# conventions"), adding or reshaping an option ("Options binding"), registering services that only work together ("Bundle co-required registrations behind one call"), changing a framework public API (a DI registration, a `SpiderlyBuilder` method, a generated constructor, a built-in service), which `spiderly init`'s template mirrors ("Init template drift"), writing a regression test or a detector ("Tests").

Spiderly is a fast-moving startup: make breaking changes freely.

Spiderly is a framework for any app, not for one, even while PACMS is its only user: code, a name, a setting or a rule belongs here only if any Spiderly app could use it as it stands, and a consumer's own policy stays in the consumer. For example, PACMS banning `DateTime.UtcNow` stays in PACMS, while the `TimeProvider` that the stamps read is Spiderly's (Filip, 2026-10-21).

Every feature must be drivable by an AI agent without human intervention: the design principles are the `ai-agentic-design` skill (`.claude/skills/ai-agentic-design/SKILL.md`), contributor-only, so outside the consumer-shipped `claude-plugins/skills/`.

The framework metadata and the agent bundle are committed **artifacts**, regenerated in the commit that changes their sources. Activate the hooks once per clone, `git config core.hooksPath .githooks`: the gated pre-commit then regenerates and stages the artifacts a commit's sources change, and runs the C#↔TS mirror test.

## Where the rest lives

Each row is a branch only some sessions reach: its trigger, then its home.

| Key | When, and where |
|---|---|
| Versioning | Bumping a version: `CODING_STANDARDS.md` → "Versions". A consumer app moving from one Spiderly release to another: the `spiderly-upgrade` skill, `claude-plugins/skills/spiderly-upgrade/SKILL.md`. |
| Documentation updates | A change that affects public API, attributes, generated output, or behavior: update the documentation in the `spiderly-website/` sibling repo. |
| API error codes | Adding, removing or renaming an `ApiErrorCodes` code: its Angular mirror changes in the same commit (`TsContractMirrorTests` pins it), and a consuming app's own TS copy (a storefront's api-error.ts, say) is a third mirror nothing here sees: `docs/framework-metadata-ssot.md` → "Cross-language mirrors". |
| Framework metadata SSOT | Changing any covered contract (`docs/framework-metadata-ssot.md` → "What's covered", plus the shape-axis data in `SpiderlyTypeRef`), **its XML `<summary>` docs alone included**, changes artifacts, which the hook regenerates, or by hand `tools/regen-metadata.sh`, then `node tools/build-agent-bundle.mjs`. Which artifacts, and the pipeline: that doc. |
| Agent guidance bundle — regenerate after skill changes | Changing a doc or skill under `claude-plugins/`, or anything under `Angular/projects/spiderly/agent/`, by a command rather than an edit (a move, a delete): `.claude/rules/agent-guidance-bundle.md`, which an edit there loads by itself. |
| Pull requests | A pull request touching `.github/workflows/**` is admin-merged by a maintainer: GitHub refuses the review app's token on a workflow diff, so `claude-review` cannot pass it. |
