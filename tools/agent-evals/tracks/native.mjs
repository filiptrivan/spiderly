import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { fixturesRoot, bundleRoot } from '../lib/paths.mjs';

// Native (Claude) track — reproduces what `Spiderly.CLI agent-sync` projects into a real consumer
// (Spiderly.CLI/Commands/AgentSyncCommand.cs), but from the repo bundle and via COPIES: a pre-`init`
// showcase workspace has no node_modules to junction against. It:
//   • copies the bundle's docs/ into the workspace (browsable, version-matched)
//   • writes AGENTS.md with agent-sync's exact "training data is stale → browse docs/" pointer block
//   • copies each skill-surface skill into .claude/skills/spiderly-<name> (agent-sync junctions these)
//   • makes CLAUDE.md import AGENTS.md (@AGENTS.md)
// This is the honest "with Spiderly" showcase condition: the agent starts with the same Spiderly
// guidance a real Claude Code user has.
//
// NOTE: native and agnostic.mjs both follow agent-sync's docs-pointer model; native is kept separate
// because it ALSO copies the skill surfaces into .claude/skills and adds the CLAUDE.md import (the
// native delta), and stages docs under a namespaced .spiderly/agent/docs so they can't collide with
// files the freestyle build creates. agent-sync (AgentSyncCommand.cs) is the source of truth.

const DOCS_SUBDIR = '.spiderly/agent/docs'; // where browsable docs are staged in the workspace

// Verbatim from AgentSyncCommand.BuildBlock so the agent sees exactly the real pointer.
function buildAgentsBlock(relDocs) {
  return `<!-- BEGIN:spiderly -->
# Spiderly

Your training data for Spiderly is stale. Before writing any Spiderly code, browse
\`${relDocs}/\` and read the \`index.md\` for the topic you're working on — these docs are
version-matched to the installed Spiderly package.
<!-- END:spiderly -->
`;
}

export function provision(task, workspaceDir, { bundlePath = bundleRoot } = {}) {
  const fixtureDir = join(fixturesRoot, task.fixture);
  if (existsSync(fixtureDir)) copyDir(fixtureDir, workspaceDir);

  const manifestPath = join(bundlePath, 'manifest.json');
  if (!existsSync(manifestPath)) return; // no bundle (e.g. a unit test) → fixture only

  // Docs: stage them in the workspace and point AGENTS.md at them (mirrors agent-sync's relDocs).
  const docsSrc = join(bundlePath, 'docs');
  if (existsSync(docsSrc)) {
    copyDir(docsSrc, join(workspaceDir, DOCS_SUBDIR));
    writeFileSync(join(workspaceDir, 'AGENTS.md'), buildAgentsBlock(DOCS_SUBDIR));
  }

  // CLAUDE.md imports AGENTS.md (agent-sync's @AGENTS.md directive), preserving any existing content.
  const claudePath = join(workspaceDir, 'CLAUDE.md');
  const claudeExisting = existsSync(claudePath) ? readFileSync(claudePath, 'utf8') : '';
  if (!claudeExisting.includes('@AGENTS.md')) {
    writeFileSync(claudePath, '@AGENTS.md\n' + (claudeExisting ? '\n' + claudeExisting : ''));
  }

  // Skill surfaces: copy each into .claude/skills/spiderly-<name> (agent-sync junctions these).
  const skills = JSON.parse(readFileSync(manifestPath, 'utf8')).skills ?? [];
  for (const s of skills) {
    const skillSrc = join(bundlePath, 'skills', s.name);
    if (existsSync(skillSrc)) copyDir(skillSrc, join(workspaceDir, '.claude', 'skills', `spiderly-${s.name}`));
  }
}
