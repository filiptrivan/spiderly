# Spiderly Angular library (`spiderly` npm package)

This workspace builds the published `spiderly` Angular library (`projects/spiderly`). Build it with `npx ng build spiderly`.

## Regenerating `package-lock.json`

**Never hand-edit `package-lock.json`.** CI's `Verify Angular lockfile integrity` gate (`npm ci --dry-run`) rejects any drift — hand-edits are what broke CI repeatedly.

**Regenerate only under the `.nvmrc` node/npm** (run `nvm use` first, or `npx npm@<x>` matching the npm that node `.nvmrc` ships — currently node `24.16.0` → npm `11.13.0`, also pinned via `packageManager`). Older npm (e.g. `11.6.2`) **silently drops** the optional `cytoscape` / `d3-selection` nodes — the `mermaid` subtree pulled in via `ngx-markdown`'s `optionalDependencies` — producing a lock its own `npm ci` accepts but CI's npm rejects as `Missing: cytoscape ... from lock file`. A "passing" local gate under the wrong npm is a **false positive** — verify in a fresh, no-`node_modules` dir under the pinned npm.

All runtime `@angular/*` are pinned **exact `19.2.13`** (matching the init template in `NetAndAngularFilesGenerator.cs`) so the lock regenerates deterministically. Don't reintroduce caret ranges on them — a from-scratch `npm install` then floats to a newer patch and dies on an ERESOLVE peer conflict against the pinned compiler.

To change a dep: edit `package.json`, run `npm install` under the pinned toolchain, commit `package.json` + `package-lock.json` together.

## Overlay content is teleported to body — never style it under `:host`

PrimeNG appends open overlays to `document.body` (`appendTo` default: `p-popover`, dropdown/calendar/multiselect panels, `p-menu popup`, …). A `:host .foo` rule compiles under emulated encapsulation to a `[_nghost] … [_ngcontent]` descendant selector that stops matching the moment the content leaves the host subtree — the rules die silently, and builds and behavior specs stay green (the data-table column chooser shipped exactly this way). Declare overlay-content rules at the SCSS **top level**: they keep only the `[_ngcontent]` attribute selector, which travels with the teleported nodes, so they stay encapsulated without `::ng-deep`. Any rule targeting teleported content also needs a computed-style assert in the component's spec — see the data-table spec's "chooser styles survive the body teleport" describe for the table-driven pattern.

## Fixed-chrome offset — `--spiderly-topbar-height`, and why there is no global smooth scroll

`styles/layout/_main.scss` declares `--spiderly-topbar-height` on `html`. Four things derive from it: `.layout-topbar`'s own height, `.layout-main-container`'s top padding, `.layout-sidebar`'s `top`, and — outside this stylesheet — any component that scrolls content back into view (`spiderly-data-table` consumes it through its container's `scroll-margin-top`). Change the topbar height there, never in `_topbar.scss`.

**A component must treat the var as absent by default.** Library components are usable without this layout, so always write `var(--spiderly-topbar-height, 0px)` and never a hardcoded rem — the fallback is what stops a consumer with no fixed header from getting a phantom gap.

**`html { scroll-behavior: smooth }` was removed (2026-08-29) and should not come back.** It silently applied to every programmatic scroll in every consuming app: with `scrollPositionRestoration: 'top'` (which pa-cms sets) each route change animated the whole way up through the new page's content, and it overrode `scrollIntoView({ behavior: 'auto' })` at every call site. It also ignored `prefers-reduced-motion`, which nothing in the library guards. A call site that genuinely wants animation passes `behavior: 'smooth'` itself.

## Always translate static UI text in library templates

Any user-facing string baked into a control/component template here (button labels, tab labels, placeholders, headings) **must** go through Transloco — never hardcode English. The admin app this library powers is fully localized (e.g. PACMS runs Serbian), so a hardcoded English word would be the one untranslated thing on the screen.

Pattern (see `controls/spiderly-markdown` and `components/spiderly-buttons/return-button`):

```ts
import { TranslocoDirective } from '@jsverse/transloco';
// add TranslocoDirective to the component's `imports`
```

```html
<ng-container *transloco="let t">
  <p-tab>{{ t('Preview') }}</p-tab>
</ng-container>
```

**The key must also be seeded** in the init template's base translation file:
`Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` → the `en` i18n JSON block (search for `"Save": "Save"`). This is required because a consumer app's `transloco-keys-manager` extraction scans only its own `src/`, **not** `node_modules/spiderly` — so a `t('NewKey')` used only inside this library would never appear in a consumer's translation files unless we seed it. New apps from `spiderly init` then resolve the key out of the box.

## Adding a new form control

A control is more than the Angular component — wiring spans the source generator and the init template. When adding one, mirror an existing control end-to-end (the Markdown control mirrored the Editor):

- Component under `controls/<name>/`, extending `BaseControl`; register it in `controls/spiderly-controls.module.ts` (imports **and** exports) and export it from `src/public-api.ts`.
- **Decide the control's commit mode.** `BaseFormService.initFormGroup` assigns `updateOn` from the schema type: array-typed / `Date` / `…Id`-named → `'change'`, everything else → `'blur'`. If the new control's value can mutate through a body-teleported overlay or a non-focusable affordance (chip remove ×, clear ×) — interactions that can end without the host ever holding focus — a `'blur'` control never commits and the save silently ships the stale value (the 2026-08-06 multiselect deselect regression; full story at the `updateOn` assignment in `base-form.service.ts`). Make sure such a control's value shape lands in the `'change'` branch, and pin it with a spec like `spiderly-multiselect.component.spec.ts` (deselect → `getRawValue()` with no blur). Known open siblings on `'blur'`: enum dropdowns (schema type is the bare enum name) and `DateOnly`/`TimeOnly` calendars (schema type `'string'`).
- Add the enum value in **both** `Spiderly.Shared/Enums/UIControlTypeCodes.cs` (public, consumer-facing, with XML docs) **and** `Spiderly.SourceGenerators/Enums/UIControlTypeCodes.cs` (internal — `Enum.TryParse` resolves the attribute value against this one).
- Map it in the generator: `NgDetailsPropertyBlockGenerator` (selector string, control attributes, width default, ordering tier) and any shared image-upload/validator hooks (`Helpers.GetEditorImageProperties`, etc.).
- If the component pulls a new runtime dependency (the Markdown control added `ngx-markdown` + `marked`), declare it as a **peerDependency** in `projects/spiderly/package.json` and seed it (plus any required `provide*()` bootstrap call) in the init template's `package.json` and `app.config.ts`. The source generator cannot edit a consumer's hand-owned `app.config.ts`, so bootstrap providers have to be seeded by `spiderly init`, not injected at generation time.
