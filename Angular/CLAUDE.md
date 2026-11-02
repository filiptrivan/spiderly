# Spiderly Angular library (`spiderly` npm package)

This workspace builds the published `spiderly` Angular library (`projects/spiderly`). Build it with `npx ng build spiderly`.

## Where the rest lives

The library's rules are path-scoped, under the repo root's `.claude/rules/`: each loads when a file it governs is read or written, and each row names it for a session that reaches its branch another way (a shell command, a TypeScript file, a code comment citing the row).

| Key | When, and where |
|---|---|
| Regenerating `package-lock.json` | Changing a dependency (`package.json`, `npm install`, the lockfile): `.claude/rules/angular-dependencies.md`. |
| Overlay styling | Styling content PrimeNG teleports to `document.body` (an open popover, a dropdown, calendar or multiselect panel), and its spec assert: `.claude/rules/library-scss.md`. |
| Fixed-chrome offset | Reading `--spiderly-viewport-top-inset` from a component (declared beside `--spiderly-topbar-height` in `styles/layout/_main.scss`, whose comment says why two names and why zero specificity), and the global `scroll-behavior: smooth` removed 2026-08-29: `.claude/rules/library-scss.md`. |
| Always translate static UI text in library templates | Static text in a library template, and seeding its key in the init template: `.claude/rules/library-template-text.md`. |
| Adding a new form control | `projects/spiderly/src/lib/controls/CLAUDE.md`. |
