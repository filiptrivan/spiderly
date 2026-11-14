# Spiderly form controls

Mirror an existing control end-to-end; its wiring spans the generator and the init template.

- Component under `controls/<name>/`, extending `BaseControl`; register it in `controls/spiderly-controls.module.ts` (imports **and** exports) and export it from `src/public-api.ts`.
- **Decide the control's commit mode.** A `'blur'` control never commits a value that changes through a body-teleported overlay or a non-focusable affordance (chip remove ×, clear ×), so the save sends the stale value. Put such a control's value shape in the `'change'` branch of the `updateOn` assignment in `base-form.service.ts`, and pin it with a spec like `spiderly-multiselect.component.spec.ts` (deselect → `getRawValue()` with no blur).
- Map it in the generator: `NgDetailsPropertyBlockGenerator` (selector string, control attributes, width default, ordering tier) and any shared image-upload/validator hooks (`Helpers.GetEditorImageProperties`, etc.).
- If the component pulls a new runtime dependency, declare it as a **peerDependency** in `projects/spiderly/package.json` and seed it in the init template's `package.json`.
- Seed a dependency's `provide*()` bootstrap call in the init template's app.config.ts: the source generator cannot edit a consumer's hand-owned app.config.ts, so only `spiderly init` can seed it.
