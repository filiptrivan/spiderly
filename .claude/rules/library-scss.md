---
paths:
  - "Angular/projects/spiderly/src/lib/**/*.scss"
---

# Library SCSS

- **Content PrimeNG teleports to `document.body` is styled at the SCSS top level, never under
  `:host`.** PrimeNG appends open overlays to the body (`appendTo` default: `p-popover`,
  dropdown/calendar/multiselect panels, `p-menu popup`, …). A `:host .foo` rule compiles under
  emulated encapsulation to a `[_nghost] … [_ngcontent]` descendant selector that stops matching the
  moment the content leaves the host subtree. The miss: the rules die silently, and builds and
  behavior specs stay green (the data-table column chooser shipped exactly this way). A top-level
  rule keeps only the `[_ngcontent]` attribute selector, which travels with the teleported nodes, so
  it stays encapsulated without `::ng-deep`. Any rule targeting teleported content also gets a
  computed-style assert in the component's spec; the data-table spec's "chooser styles survive the
  body teleport" describe is the table-driven pattern.
- **A component-side rule reading the viewport inset writes `var(--spiderly-viewport-top-inset,
  0px)`.** Library components are usable without Spiderly's layout. The miss without the fallback:
  a consumer running its own shell gets a phantom gap. The layout's own files (`_topbar.scss`,
  `_content.scss`, `_menu.scss`) deliberately use no fallback: `layout.scss` `@use`s `_main.scss`,
  which declares the vars, alongside them, so the declaration can never be missing there.
- **A call site that wants an animated scroll passes `behavior: 'smooth'` itself; the library sets
  no global `scroll-behavior`** (`html { scroll-behavior: smooth }` was removed 2026-08-29). The
  miss of a global one: it silently applied to every programmatic scroll in every consuming app.
  With the router's `scrollPositionRestoration: 'top'` (PACMS's admin sets it) each
  route change animated the whole way up through the new page's content, it did the same to
  `anchorScrolling` and native `#fragment` jumps, and it overrode `scrollIntoView({ behavior: 'auto' })`
  at every call site. It also ignored `prefers-reduced-motion`, which nothing in the library guards.
