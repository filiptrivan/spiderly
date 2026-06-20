import { cpSync, mkdirSync, existsSync, readdirSync } from 'node:fs';
import { join, relative } from 'node:path';

// Recursively copy src into dst, merging onto any existing dst contents (used both to
// seed a fixture and to overlay an oracle patch).
export function copyDir(src, dst, { filter } = {}) {
  if (!existsSync(src)) throw new Error(`copyDir: source missing: ${src}`);
  mkdirSync(dst, { recursive: true });
  cpSync(src, dst, { recursive: true, filter });
}

// Build output / dependency dirs — never "source the build produced"; skipped by walkFiles.
export const BUILD_ARTIFACT_DIRS = new Set(['bin', 'obj', 'node_modules', '.git', '.angular', 'dist', '.vs']);

// A cpSync `filter` that drops any path lying under one of `dirNames` (matched as a path segment), so
// a whole build-artifact subtree is excluded from a copyDir. Pass a subset of BUILD_ARTIFACT_DIRS —
// the full set, or the set minus 'node_modules' to keep a shipped bundle. Single source of truth for
// "what's a build dir", shared with walkFiles instead of per-track regexes.
export function skipDirsFilter(dirNames) {
  const set = dirNames instanceof Set ? dirNames : new Set(dirNames);
  return (src) => !src.split(/[\\/]/).some((seg) => set.has(seg));
}

// Recursively list source files under `root` as sorted, POSIX-relative paths, skipping
// BUILD_ARTIFACT_DIRS. Unreadable dirs are skipped (not thrown). Used to capture what a build
// produced — e.g. the showcase file-tree + count.
export function walkFiles(root, dir = root, out = []) {
  let entries = [];
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of entries) {
    const full = join(dir, e.name);
    if (e.isDirectory()) {
      if (BUILD_ARTIFACT_DIRS.has(e.name)) continue;
      walkFiles(root, full, out);
    } else {
      out.push(relative(root, full).split('\\').join('/'));
    }
  }
  return out.sort();
}
