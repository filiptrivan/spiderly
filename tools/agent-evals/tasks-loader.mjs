import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tasksRoot } from './lib/paths.mjs';

const TIERS = ['atomic', 'feature', 'full-app'];
const REQUIRED_FIELDS = ['id', 'tier', 'targets', 'fixture', 'maxTurns', 'required'];

// Discover tasks. A task is a folder under tasks/<tier>/ containing task.json + prompt.md.
export function loadTasks({ tier, taskId } = {}) {
  const tasks = [];
  for (const t of TIERS) {
    if (tier && tier !== t) continue;
    const tierDir = join(tasksRoot, t);
    if (!existsSync(tierDir)) continue;
    for (const d of readdirSync(tierDir, { withFileTypes: true })) {
      if (!d.isDirectory()) continue;
      if (taskId && d.name !== taskId) continue;
      const dir = join(tierDir, d.name);
      if (!existsSync(join(dir, 'task.json'))) continue;
      const meta = JSON.parse(readFileSync(join(dir, 'task.json'), 'utf8'));
      for (const f of REQUIRED_FIELDS) {
        if (meta[f] === undefined) throw new Error(`task ${d.name}: missing field "${f}"`);
      }
      if (meta.id !== d.name) {
        throw new Error(`task ${d.name}: id "${meta.id}" must equal folder name`);
      }
      const promptText = readFileSync(join(dir, 'prompt.md'), 'utf8');
      tasks.push({ ...meta, dir, promptText });
    }
  }
  return tasks;
}
