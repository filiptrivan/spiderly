import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// Recursively collect *.cs files under dir, skipping build output and deps.
function csFiles(dir) {
  const out = [];
  let entries = [];
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of entries) {
    if (e.isDirectory()) {
      if (e.name === 'bin' || e.name === 'obj' || e.name === 'node_modules') continue;
      out.push(...csFiles(join(dir, e.name)));
    } else if (e.name.endsWith('.cs')) {
      out.push(join(dir, e.name));
    }
  }
  return out;
}

// `compiles` is the required, deterministic signal — Spiderly's source generators run during
// dotnet build, so a malformed validator fails the compile. `validator-present` is a diagnostic.
export default async function verify({ workspaceDir, run }) {
  const backend = join(workspaceDir, 'Backend');
  const build = run('dotnet', ['build'], { cwd: backend, shell: true, timeoutMs: 10 * 60 * 1000 });
  const compiles = build.code === 0;

  // Diagnostic only (not in `required`): did they add a FluentValidation RuleFor referencing Name?
  // Pure-Node scan — avoids depending on `grep`, which is absent on Windows (the harness's habitat).
  const validatorPresent = csFiles(backend).some((f) => {
    const txt = readFileSync(f, 'utf8');
    return txt.includes('RuleFor') && /name/i.test(txt);
  });

  return [
    { name: 'compiles', pass: compiles, detail: compiles ? 'dotnet build OK' : build.stderr.slice(-500) },
    { name: 'validator-present', pass: validatorPresent, detail: 'found RuleFor referencing Name (diagnostic)' },
  ];
}
