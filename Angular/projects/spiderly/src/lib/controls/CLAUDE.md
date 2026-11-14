# Spiderly form controls

Mirror an existing control end-to-end; its wiring spans the generator and the init template.

- **Decide the control's commit mode.** A value can change through a body-teleported overlay or a non-focusable affordance (chip remove ×, clear ×). A `'blur'` control never commits that change, so the save sends the stale value. Put such a control's value shape in the `'change'` branch of the `updateOn` assignment in `base-form.service.ts`. Pin it with a spec like `spiderly-multiselect.component.spec.ts` (deselect → `getRawValue()` with no blur).
- Seed a dependency's `provide*()` bootstrap call in the init template's app.config.ts. The source generator cannot edit a consumer's hand-owned app.config.ts, so only `spiderly init` can seed it.
