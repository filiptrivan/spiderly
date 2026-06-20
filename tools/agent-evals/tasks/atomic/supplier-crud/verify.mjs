import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { walkFiles } from '../../../lib/fs-utils.mjs';

// INTERIM verifier (walking skeleton). The REAL check is OUTSIDE-IN — boot the app + Postgres, log
// in, POST/GET /api/suppliers, assert 201/200 and a 400 on an invalid Name/Email — which is the
// next infra step (it needs the app to run). Until then this discriminates oracle (feature present,
// builds) from no-op (nothing) WITHOUT a database: `dotnet build` plus the presence of a Supplier
// entity and a SuppliersController. Replace with the boot-based contract check once Postgres is
// wired. Per the runVerify INVARIANT, never throw — return failing checks instead.

export default async function verify({ workspaceDir, run }) {
  const backend = join(workspaceDir, 'Backend');
  const build = run('dotnet', ['build'], { cwd: backend, shell: true, timeoutMs: 10 * 60 * 1000 });
  const compiles = build.code === 0;

  let entity = false;
  let endpoint = false;
  // Reuse the shared tree-walk (skips bin/obj/node_modules/etc. via BUILD_ARTIFACT_DIRS); grep .cs only.
  for (const rel of walkFiles(backend)) {
    if (!rel.endsWith('.cs')) continue;
    const txt = readFileSync(join(backend, rel), 'utf8');
    if (/class\s+Supplier\b/.test(txt) && /\bstring\s+Name\b/.test(txt)) entity = true;
    if (/class\s+SuppliersController\b/.test(txt)) endpoint = true;
    if (entity && endpoint) break;
  }

  return [
    { name: 'compiles', pass: compiles, detail: compiles ? 'dotnet build OK' : build.stderr.slice(-800) },
    { name: 'supplier-entity', pass: entity, detail: entity ? 'found a Supplier entity with a string Name' : 'no Supplier entity with a string Name property' },
    { name: 'suppliers-endpoint', pass: endpoint, detail: endpoint ? 'found a SuppliersController' : 'no SuppliersController' },
  ];
}
