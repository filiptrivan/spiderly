import { join } from 'node:path';

// `compiles` is the required, deterministic signal — Spiderly's source generators run during
// dotnet build, so a malformed validator fails the compile. `validator-present` is a diagnostic.
export default async function verify({ workspaceDir, run }) {
  const backend = join(workspaceDir, 'Backend');
  const build = run('dotnet', ['build'], { cwd: backend, shell: true, timeoutMs: 10 * 60 * 1000 });
  const compiles = build.code === 0;

  // Heuristic diagnostic: did they touch a validation rule for Product.Name at all?
  const grep = run('grep', ['-rIl', '--include=*.cs', 'RuleFor', backend], { shell: true });
  const validatorPresent = grep.code === 0 && /name/i.test(grep.stdout + build.stdout);

  return [
    { name: 'compiles', pass: compiles, detail: compiles ? 'dotnet build OK' : build.stderr.slice(-500) },
    { name: 'validator-present', pass: validatorPresent, detail: 'found RuleFor reference (heuristic)' },
  ];
}
