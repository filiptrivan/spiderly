# Spiderly Angular library (`spiderly` npm package)

This workspace builds the published `spiderly` Angular library (`projects/spiderly`). Build it with `npx ng build spiderly`.

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
- Add the enum value in **both** `Spiderly.Shared/Enums/UIControlTypeCodes.cs` (public, consumer-facing, with XML docs) **and** `Spiderly.SourceGenerators/Enums/UIControlTypeCodes.cs` (internal — `Enum.TryParse` resolves the attribute value against this one).
- Map it in the generator: `NgDetailsPropertyBlockGenerator` (selector string, control attributes, width default, ordering tier) and any shared image-upload/validator hooks (`Helpers.GetEditorImageProperties`, etc.).
- If the component pulls a new runtime dependency (the Markdown control added `ngx-markdown` + `marked`), declare it as a **peerDependency** in `projects/spiderly/package.json` and seed it (plus any required `provide*()` bootstrap call) in the init template's `package.json` and `app.config.ts`. The source generator cannot edit a consumer's hand-owned `app.config.ts`, so bootstrap providers have to be seeded by `spiderly init`, not injected at generation time.
