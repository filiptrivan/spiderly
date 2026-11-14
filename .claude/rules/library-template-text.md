---
paths:
  - "Angular/projects/spiderly/src/lib/**/*.html"
  - "Angular/projects/spiderly/src/lib/**/*.component.ts"
---

# Static text in a library template

- **Each new Transloco key is seeded** in the init template's base translation file, the `en` i18n JSON
  block in `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` (search for `"Save": "Save"`).
  The miss: a consumer's `transloco-keys-manager` extraction scans only its own `src/`, **not**
  `node_modules/spiderly`, so an unseeded key used only inside this library never reaches a
  consumer's translation files; seeded, new apps from `spiderly init` resolve it out of the box.
