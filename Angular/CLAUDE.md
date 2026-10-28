# Spiderly Angular library (`spiderly` npm package)

This workspace builds the published `spiderly` Angular library (`projects/spiderly`). Build it with `npx ng build spiderly`.

## Where the rest lives

The library's rules are path-scoped, under the repo root's `.claude/rules/`: each loads with the files it governs, and each row names it for a session that reaches its branch another way.

| Key | When, and where |
|---|---|
| Regenerating `package-lock.json` | Changing a dependency (`package.json`, `npm install`, the lockfile): `.claude/rules/angular-dependencies.md`. |
| Overlay content is teleported to body — never style it under `:host` | Styling content PrimeNG teleports to `document.body` (an open popover, a dropdown, calendar or multiselect panel), and its spec assert: `.claude/rules/library-scss.md`. |
| Fixed-chrome offset — the two viewport vars, and why there is no global smooth scroll | `styles/layout/_main.scss` declares `--spiderly-topbar-height` and `--spiderly-viewport-top-inset`; why two names and why zero specificity: its comment on them. Reading the inset from a component, and the global `scroll-behavior: smooth` removed 2026-08-29: `.claude/rules/library-scss.md`. |
| Always translate static UI text in library templates | Static text in a library template, and seeding its key in the init template: `.claude/rules/library-template-text.md`. |
| Adding a new form control | `projects/spiderly/src/lib/controls/CLAUDE.md`. |
