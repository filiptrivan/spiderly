import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './spiderly.mjs';

test('spiderly provision copies the real init output incl. its shipped guidance, skips only build output', () => {
  // Fake `spiderly init` app: app source + the guidance that init/agent-sync set up (root AGENTS.md
  // and the docs shipped inside node_modules/spiderly/agent) + a .NET build artifact.
  const root = mkdtempSync(join(tmpdir(), 'fix-'));
  const app = join(root, 'spiderly-app');
  mkdirSync(join(app, 'Backend', 'bin'), { recursive: true });
  mkdirSync(join(app, 'node_modules', 'spiderly', 'agent', 'docs', 'entity-design'), { recursive: true });
  writeFileSync(join(app, 'Backend', 'Program.cs'), '// spiderly app');
  writeFileSync(join(app, 'AGENTS.md'), '# Spiderly\n');
  writeFileSync(join(app, 'node_modules', 'spiderly', 'agent', 'docs', 'entity-design', 'index.md'), '# doc');
  writeFileSync(join(app, 'Backend', 'bin', 'x.dll'), 'BIN');

  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  provision({}, ws, { root });

  assert.ok(existsSync(join(ws, 'Backend', 'Program.cs')), 'app source copied');
  assert.ok(existsSync(join(ws, 'AGENTS.md')), 'init-written AGENTS.md kept');
  // The whole point: the guidance shipped in node_modules is KEPT (plain-net strips it; this must not).
  assert.ok(
    existsSync(join(ws, 'node_modules', 'spiderly', 'agent', 'docs', 'entity-design', 'index.md')),
    'shipped Spiderly guidance kept',
  );
  assert.equal(existsSync(join(ws, 'Backend', 'bin')), false, '.NET build output skipped');
});
