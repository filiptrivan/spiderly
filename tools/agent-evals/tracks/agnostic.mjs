import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { fixturesRoot, bundleRoot } from '../lib/paths.mjs';

// Agnostic track: every agent gets ONLY the AGENTS.md index built from the shipped
// bundle's `doc`-surface entries — no agent-specific layer (no skill junctions, no
// .cursor/rules). bundlePath is injectable for testing.
export function provision(task, workspaceDir, { bundlePath = bundleRoot } = {}) {
  copyDir(join(fixturesRoot, task.fixture), workspaceDir);

  const manifestPath = join(bundlePath, 'manifest.json');
  if (!existsSync(manifestPath)) return; // no bundle (e.g. trivial self-test) → fixture only

  const skills = JSON.parse(readFileSync(manifestPath, 'utf8')).skills ?? [];
  const docs = skills.filter((s) => s.surface === 'doc');
  const index = [
    '# Spiderly agent guidance (agnostic track)',
    '',
    ...docs.map((d) => `- **${d.name}** — ${d.description}`),
  ].join('\n');
  writeFileSync(join(workspaceDir, 'AGENTS.md'), index + '\n');
}
