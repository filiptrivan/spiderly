# Spiderly Angular library (`spiderly` npm package)

This workspace builds the published `spiderly` Angular library (`projects/spiderly`). Build it with `npx ng build spiderly`.

## Regenerating `package-lock.json`

**Never hand-edit `package-lock.json`.** CI's `Verify Angular lockfile integrity` gate (`npm ci --dry-run`) rejects any drift — hand-edits are what broke CI repeatedly.

**Regenerate only under the pinned toolchain**, the node `.nvmrc` names and the npm `packageManager` pins (run `nvm use` first, or `npx npm@<that version>`). Older npm (e.g. `11.6.2`) **silently drops** the optional `cytoscape` / `d3-selection` nodes — the `mermaid` subtree pulled in via `ngx-markdown`'s `optionalDependencies` — producing a lock its own `npm ci` accepts but CI's npm rejects as `Missing: cytoscape ... from lock file`. A "passing" local gate under the wrong npm is a **false positive** — verify in a fresh, no-`node_modules` dir under the pinned npm.

To change a dep: edit `package.json`, run `npm install` under the pinned toolchain, commit `package.json` + `package-lock.json` together. Runtime `@angular/*` stay pinned exact: `CODING_STANDARDS.md` → "Angular library".

## Overlay content is teleported to body — never style it under `:host`

Styling content PrimeNG teleports to `document.body` (an open popover, a dropdown, calendar or multiselect panel), and its spec assert: `CODING_STANDARDS.md` → "Angular library".

## Fixed-chrome offset — the two viewport vars, and why there is no global smooth scroll

`styles/layout/_main.scss` declares `--spiderly-topbar-height` and `--spiderly-viewport-top-inset`; why two names and why zero specificity: its comment on them. Reading the inset from a component: `CODING_STANDARDS.md` → "Angular library".

**`html { scroll-behavior: smooth }` was removed (2026-08-29) and should not come back.** It silently applied to every programmatic scroll in every consuming app: with `scrollPositionRestoration: 'top'` (which pa-cms sets) each route change animated the whole way up through the new page's content, it did the same to `anchorScrolling` and native `#fragment` jumps, and it overrode `scrollIntoView({ behavior: 'auto' })` at every call site. It also ignored `prefers-reduced-motion`, which nothing in the library guards. A call site that genuinely wants animation passes `behavior: 'smooth'` itself.

## Always translate static UI text in library templates

Static text in a library template, and seeding its key in the init template: `CODING_STANDARDS.md` → "Angular library".

## Adding a new form control

A control is more than the Angular component — wiring spans the source generator and the init template. When adding one, mirror an existing control end-to-end (the Markdown control mirrored the Editor):

- Component under `controls/<name>/`, extending `BaseControl`; register it in `controls/spiderly-controls.module.ts` (imports **and** exports) and export it from `src/public-api.ts`.
- **Decide the control's commit mode.** If the new control's value can mutate through a body-teleported overlay or a non-focusable affordance (chip remove ×, clear ×) — interactions that can end without the host ever holding focus — a `'blur'` control never commits and the save silently ships the stale value (the 2026-08-06 multiselect deselect regression; full story at the `updateOn` assignment in `base-form.service.ts`). Make sure such a control's value shape lands in the `'change'` branch, and pin it with a spec like `spiderly-multiselect.component.spec.ts` (deselect → `getRawValue()` with no blur). Known open siblings on `'blur'`: enum dropdowns (schema type is the bare enum name) and `DateOnly`/`TimeOnly` calendars (schema type `'string'`).
- Add the enum value in **both** `Spiderly.Shared/Enums/UIControlTypeCodes.cs` (public, consumer-facing, with XML docs) **and** `Spiderly.SourceGenerators/Enums/UIControlTypeCodes.cs` (internal — `Enum.TryParse` resolves the attribute value against this one).
- Map it in the generator: `NgDetailsPropertyBlockGenerator` (selector string, control attributes, width default, ordering tier) and any shared image-upload/validator hooks (`Helpers.GetEditorImageProperties`, etc.).
- If the component pulls a new runtime dependency (the Markdown control added `ngx-markdown` + `marked`), declare it as a **peerDependency** in `projects/spiderly/package.json` and seed it (plus any required `provide*()` bootstrap call) in the init template's `package.json` and `app.config.ts`. The source generator cannot edit a consumer's hand-owned `app.config.ts`, so bootstrap providers have to be seeded by `spiderly init`, not injected at generation time.
