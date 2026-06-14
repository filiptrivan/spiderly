import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// A task verifier default-exports async ({ workspaceDir, run }) => Check[]
// where Check = { name, pass, detail }. This one needs no toolchain.
export default async function verify({ workspaceDir }) {
  const p = join(workspaceDir, 'result.txt');
  const ok = existsSync(p) && readFileSync(p, 'utf8').trim() === 'DONE';
  return [{
    name: 'marker-present',
    pass: ok,
    detail: ok ? 'result.txt == DONE' : 'result.txt missing or wrong content',
  }];
}
