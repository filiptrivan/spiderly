---
paths:
  - "claude-plugins/docs/**"
  - "claude-plugins/skills/**"
  - "Angular/projects/spiderly/agent/**"
---

# The agent guidance bundle

`Angular/projects/spiderly/agent/` is a **committed build artifact** that ships *inside* the
`spiderly` npm package, built from two authoring trees, `claude-plugins/docs/**` (reference docs,
each `index.md`) and `claude-plugins/skills/**` (workflow skills, each `SKILL.md`). How it is laid
out and reaches a consumer: the header of `tools/build-agent-bundle.mjs`; the design:
`docs/agent-guidance-distribution.md`.

- After changing any doc or skill, or its `*.generated.md` references, regenerate and commit the
  bundle in the same commit: `node tools/build-agent-bundle.mjs`, run **after**
  `tools/regen-metadata.sh` when both apply (it copies the freshly-generated reference tables). The
  miss: CI regenerates the bundle and fails on any diff.
- Never hand-edit anything under `agent/`. The miss: the next regeneration (the gated
  `.githooks/pre-commit`'s included, which auto-stages it) silently replaces the edit with what the
  authoring trees say.
