---
paths:
  - "Angular/projects/spiderly/src/lib/**/*.scss"
---

# Library SCSS

- **Content PrimeNG teleports to `document.body` (an open overlay: `p-popover`, dropdown, calendar
  and multiselect panels, `p-menu popup`) is styled at the SCSS top level, never under `:host`**:
  under emulated encapsulation `:host .foo` becomes a descendant selector that stops matching once
  the content leaves the host, while a top-level rule keeps only the `[_ngcontent]` attribute, which
  travels with the nodes. Such a rule gets a computed-style assert in the component's spec, as the
  data table's 'chooser styles survive the body teleport' describe does.
