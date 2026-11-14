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
- **A component-side rule reading the viewport inset writes `var(--spiderly-viewport-top-inset,
  0px)`.** Library components are usable without Spiderly's layout. The miss without the fallback:
  a consumer running its own shell gets a phantom gap. The layout's own files (`_topbar.scss`,
  `_content.scss`, `_menu.scss`) deliberately use no fallback: `layout.scss` `@use`s `_main.scss`,
  which declares the vars, alongside them, so the declaration can never be missing there.
- **A call site that wants an animated scroll passes `behavior: 'smooth'` itself; the library sets
  no global `scroll-behavior`.** The miss of a global one: it silently applies to every programmatic
  scroll in every consuming app, the router's scroll to the top on a route change included. It also
  ignores `prefers-reduced-motion`, which nothing in the library guards.
