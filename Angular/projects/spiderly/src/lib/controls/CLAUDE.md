# Spiderly form controls

The library's form controls (`spiderly-*`), one directory each, which the generated details pages render. A control is more than the Angular component — wiring spans the source generator and the init template.

## Adding a new form control

When adding one, mirror an existing control end-to-end (the Markdown control mirrored the Editor):

- Component under `controls/<name>/`, extending `BaseControl`; register it in `controls/spiderly-controls.module.ts` (imports **and** exports) and export it from `src/public-api.ts`.
- **Decide the control's commit mode.** If the new control's value can mutate through a body-teleported overlay or a non-focusable affordance (chip remove ×, clear ×) — interactions that can end without the host ever holding focus — a `'blur'` control never commits and the save silently ships the stale value (the 2026-08-06 multiselect deselect regression; full story at the `updateOn` assignment in `base-form.service.ts`). Make sure such a control's value shape lands in the `'change'` branch, and pin it with a spec like `spiderly-multiselect.component.spec.ts` (deselect → `getRawValue()` with no blur). Known open siblings on `'blur'`: enum dropdowns (schema type is the bare enum name) and `DateOnly`/`TimeOnly` calendars (schema type `'string'`).
- Add the enum value in **both** `Spiderly.Shared/Enums/UIControlTypeCodes.cs` (public, consumer-facing, with XML docs) **and** `Spiderly.SourceGenerators/Enums/UIControlTypeCodes.cs` (internal — `Enum.TryParse` resolves the attribute value against this one).
- Map it in the generator: `NgDetailsPropertyBlockGenerator` (selector string, control attributes, width default, ordering tier) and any shared image-upload/validator hooks (`Helpers.GetEditorImageProperties`, etc.).
- If the component pulls a new runtime dependency (the Markdown control added `ngx-markdown` + `marked`), declare it as a **peerDependency** in `projects/spiderly/package.json` and seed it (plus any required `provide*()` bootstrap call) in the init template's `package.json` and app.config.ts. The source generator cannot edit a consumer's hand-owned app.config.ts, so bootstrap providers have to be seeded by `spiderly init`, not injected at generation time.
