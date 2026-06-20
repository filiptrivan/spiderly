import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { fixturesRoot } from '../lib/paths.mjs';

// Plain track: the no-framework control. The agent gets ONLY the task's starting fixture —
// for a freestyle showcase build that's an empty workspace — and NOTHING from Spiderly: no
// init'd app, no AGENTS.md bundle, no junctioned skills. "Make the app" from a bare directory;
// the agent runs its own `dotnet new` / `ng new`. The spiderly↔plain gap is the whole point of
// the showcase, so this track must add nothing a Spiderly-aware track would.
//
// The fixture copy is guarded so a task can declare an absent/empty fixture and still get a bare
// workspace (freestyle from nothing) rather than throwing.
export function provision(task, workspaceDir) {
  const fixtureDir = join(fixturesRoot, task.fixture);
  if (existsSync(fixtureDir)) copyDir(fixtureDir, workspaceDir);
  // Deliberately nothing else.
}
