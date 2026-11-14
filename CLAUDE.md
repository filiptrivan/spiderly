A change follows `CODING_STANDARDS.md`: read its section before C# naming, an option, a service registration, a public API change, moving a docs page, or a regression test.

Spiderly is a fast-moving startup: make breaking changes freely.

Spiderly is a framework for any app, not for one, even while one app is its only user: code, a name, a setting or a rule belongs here only if any Spiderly app could use it as it stands, and a consumer's own policy stays in the consumer.

## Where the rest lives

Each row is a branch only some sessions reach: its trigger, then its home.

| Key | When, and where |
|---|---|
| Documentation updates | A change that affects public API, attributes, generated output, or behavior: update the documentation in the `spiderly-website/` sibling repo. |
| API error codes | Adding, removing or renaming an `ApiErrorCodes` code: its Angular mirror changes in the same commit (`TsContractMirrorTests` pins it), and a consuming app's own TS copy (a storefront's api-error.ts, say) is a third mirror nothing here sees: `docs/framework-metadata-ssot.md` → "Cross-language mirrors". |
