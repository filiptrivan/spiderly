# Coding standards

The rules a reviewer judges from a change in Spiderly or in its website, spiderly-website, which
keeps none of its own. A rule a lint or test pins is named by its pin.

## Design (Filip, 2026-10-25)

We work in this codebase for years, so a diff is *strategic* programming, never *tactical*
(Ousterhout, *A Philosophy of Software Design*): working code is the floor, and the code a diff
touches is left simpler than it was. A conforming diff puts new behaviour where the design absorbs
it, a deep module or an existing seam; a special case, flag, copied block or workaround patched
around the design is a finding, even when its tests pass.

## Code and comments are written for agents

Premise: Spiderly is a framework for agents, and its contributors work in it through agents (Filip,
2026-10-25), so documentation is written for its actual reader: a stateless agent with grep and a
token budget. A conforming diff:

- anchors with symbols, never line numbers (`see PermissionHandlerRegistrationGuard`, not
  `see line 82`);
- tells each fact once, with pointers everywhere else; the code is itself a telling, so no prose
  restates what it does;
- dates a claim and names its evidence ("measured 2026-08-07" is judgeable for staleness,
  "currently" is not);
- pins a rule with a test or lint rule rather than prose;
- never narrates syntax or softens for an imagined junior.
