---
paths:
  - "Angular/projects/spiderly/src/lib/**/*.html"
  - "Angular/projects/spiderly/src/lib/**/*.component.ts"
---

# Static text in a library template

- **Static UI text in a library template goes through Transloco**, never hardcoded English (button
  and tab labels, placeholders, headings). The miss: the admin a consumer builds on this library is
  fully localized, so a hardcoded English word is the one untranslated thing on its screen. The
  pattern: `TranslocoDirective` in the component's `imports`, the template inside
  `<ng-container *transloco="let t">`, as `Angular/projects/spiderly/src/lib/controls/spiderly-markdown/`
  and `Angular/projects/spiderly/src/lib/components/spiderly-buttons/return-button/` do.
- **Each new key is also seeded** in the init template's base translation file, the `en` i18n JSON
  block in `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` (search for `"Save": "Save"`).
  The miss: a consumer's `transloco-keys-manager` extraction scans only its own `src/`, **not**
  `node_modules/spiderly`, so an unseeded key used only inside this library never reaches a
  consumer's translation files; seeded, new apps from `spiderly init` resolve it out of the box.
