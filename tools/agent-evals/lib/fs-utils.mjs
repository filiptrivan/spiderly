import { cpSync, mkdirSync, existsSync } from 'node:fs';

// Recursively copy src into dst, merging onto any existing dst contents (used both to
// seed a fixture and to overlay an oracle patch).
export function copyDir(src, dst) {
  if (!existsSync(src)) throw new Error(`copyDir: source missing: ${src}`);
  mkdirSync(dst, { recursive: true });
  cpSync(src, dst, { recursive: true });
}
