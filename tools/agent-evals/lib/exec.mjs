import { spawnSync } from 'node:child_process';

// Run a command, capturing output. shell:true is needed on Windows for .cmd shims
// (npm, ng, claude). Returns a normalized {code, stdout, stderr}; never throws on
// non-zero exit (callers decide what a non-zero code means).
export function run(cmd, args = [], opts = {}) {
  const res = spawnSync(cmd, args, {
    cwd: opts.cwd,
    env: { ...process.env, ...opts.env },
    encoding: 'utf8',
    timeout: opts.timeoutMs ?? 0,
    shell: opts.shell ?? false,
    maxBuffer: 64 * 1024 * 1024,
  });
  return {
    code: res.status ?? (res.error ? -1 : 0),
    stdout: res.stdout ?? '',
    stderr: res.stderr ?? (res.error ? String(res.error) : ''),
  };
}
