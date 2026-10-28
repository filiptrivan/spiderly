---
paths:
  - "Angular/package.json"
  - "Angular/package-lock.json"
---

# Angular dependencies and the lockfile

- **Never hand-edit `package-lock.json`.** CI's `Verify Angular lockfile integrity` gate
  (`npm ci --dry-run`) rejects any drift — hand-edits are what broke CI repeatedly.
- **Regenerate only under the pinned toolchain**, the node `.nvmrc` names and the npm
  `packageManager` pins (run `nvm use` first, or `npx npm@<that version>`). Older npm (e.g.
  `11.6.2`) **silently drops** the optional `cytoscape` / `d3-selection` nodes — the `mermaid`
  subtree pulled in via `ngx-markdown`'s `optionalDependencies` — producing a lock its own `npm ci`
  accepts but CI's npm rejects as `Missing: cytoscape ... from lock file`. A "passing" local gate
  under the wrong npm is a **false positive** — verify in a fresh, no-`node_modules` dir under the
  pinned npm.
- To change a dep: edit `package.json`, run `npm install` under the pinned toolchain, commit
  `package.json` + `package-lock.json` together.
- **Every runtime `@angular/*` in `Angular/package.json` is pinned exact**, at the version the init
  template in `NetAndAngularFilesGenerator.cs` emits, so the lock regenerates deterministically. The
  miss a caret range causes: a from-scratch `npm install` floats to a newer patch and dies on an
  ERESOLVE peer conflict against the pinned compiler.
