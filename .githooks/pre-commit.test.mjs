// Runs .githooks/pre-commit through a real `git commit` in a throwaway repo per test, holding only the
// files the block under test reads: the agent bundle's builder and two authoring trees, or the Angular
// manifest and lock.
//
//   node --test .githooks/pre-commit.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const agent = 'Angular/projects/spiderly/agent';

// Git's own variables are dropped, so a run from inside another hook never reaches the outer repo.
const env = {
  ...Object.fromEntries(Object.entries(process.env).filter(([k]) => !k.startsWith('GIT_'))),
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.com',
  GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.com',
};

function run(cwd, cmd, args) {
  const r = spawnSync(cmd, args, { cwd, env, encoding: 'utf8' });
  return { code: r.status, out: r.stdout + r.stderr };
}

function git(cwd, ...args) {
  return run(cwd, 'git', ['-c', 'core.hooksPath=.githooks', '-c', 'commit.gpgsign=false', ...args]);
}

function write(root, path, text) {
  mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, path), text);
}

function copy(root, path) {
  mkdirSync(dirname(join(root, path)), { recursive: true });
  copyFileSync(join(repoRoot, path), join(root, path));
}

function stage(root, ...paths) {
  const r = git(root, 'add', ...paths);
  assert.equal(r.code, 0, r.out);
}

// A repo whose first commit, made past the hook, holds `paths` copied from this repo and `files`. It sits
// one level down in its temp directory, so the Size cap block finds no sibling ci-workflows and skips.
function fixture(paths, files = {}) {
  const root = join(mkdtempSync(join(tmpdir(), 'pre-commit-')), 'repo');
  mkdirSync(root);
  assert.equal(git(root, 'init', '-q').code, 0);
  for (const p of ['.githooks/pre-commit', ...paths]) copy(root, p);
  for (const [p, text] of Object.entries(files)) write(root, p, text);
  return root;
}

function commitAll(root) {
  stage(root, '-A');
  const r = git(root, 'commit', '-q', '--no-verify', '-m', 'fixture');
  assert.equal(r.code, 0, r.out);
}

const entry = (name, body) => `---\nname: ${name}\ndescription: The ${name} fixture.\n---\n\n${body}\n`;

function bundleFixture() {
  const root = fixture(['tools/build-agent-bundle.mjs'], {
    'claude-plugins/docs/alpha/index.md': entry('alpha', 'Alpha doc.'),
    'claude-plugins/skills/beta/SKILL.md': entry('beta', 'Beta skill.'),
  });
  assert.equal(run(root, process.execPath, ['tools/build-agent-bundle.mjs']).code, 0);
  commitAll(root);
  return root;
}

const commitCount = (root) => git(root, 'rev-list', '--count', 'HEAD').out.trim();

test('a staged hand edit under agent/ is refused, naming the authoring file it comes from', () => {
  const root = bundleFixture();
  write(root, `${agent}/docs/alpha/index.md`, entry('alpha', 'Edited by hand.'));
  write(root, `${agent}/skills/beta/SKILL.md`, entry('beta', 'Edited by hand.'));
  stage(root, agent);

  const r = git(root, 'commit', '-m', 'hand edit');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /claude-plugins\/docs\/alpha\/index\.md/);
  assert.match(r.out, /claude-plugins\/skills\/beta\/SKILL\.md/);
  assert.equal(commitCount(root), '1');
  assert.match(git(root, 'diff', '--cached', '--', agent).out, /Edited by hand\./, 'the hand edit stays staged');
});

test('an edit to an authoring tree regenerates the bundle and stages it into the commit', () => {
  const root = bundleFixture();
  write(root, 'claude-plugins/docs/alpha/index.md', entry('alpha', 'Edited at the source.'));
  stage(root, 'claude-plugins');

  const r = git(root, 'commit', '-m', 'source edit');

  assert.equal(r.code, 0, r.out);
  assert.match(git(root, 'show', `HEAD:${agent}/docs/alpha/index.md`).out, /Edited at the source\./);
});

test('a staged bundle built from its staged source passes', () => {
  const root = bundleFixture();
  write(root, 'claude-plugins/skills/beta/SKILL.md', entry('beta', 'Rebuilt by hand.'));
  assert.equal(run(root, process.execPath, ['tools/build-agent-bundle.mjs']).code, 0);
  stage(root, 'claude-plugins', agent);

  const r = git(root, 'commit', '-m', 'source edit and its build');

  assert.equal(r.code, 0, r.out);
  assert.match(r.out, /regenerating to verify sync/);
  assert.match(git(root, 'show', `HEAD:${agent}/skills/beta/SKILL.md`).out, /Rebuilt by hand\./);
});

test('a staged build whose source changed again unstaged names that source, not a hand edit', () => {
  const root = bundleFixture();
  write(root, 'claude-plugins/skills/beta/SKILL.md', entry('beta', 'Built and staged.'));
  assert.equal(run(root, process.execPath, ['tools/build-agent-bundle.mjs']).code, 0);
  stage(root, 'claude-plugins', agent);
  write(root, 'claude-plugins/skills/beta/SKILL.md', entry('beta', 'Edited again, unstaged.'));

  const r = git(root, 'commit', '-m', 'unstaged source');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /bundle sources have unstaged\/untracked changes/);
  assert.doesNotMatch(r.out, /replaces these staged files/);
});

// This repo's own manifest and lock, staged on top of a first commit that lacks them.
function angularFixture() {
  const root = fixture([]);
  commitAll(root);
  copy(root, 'Angular/package.json');
  copy(root, 'Angular/package-lock.json');
  return root;
}

// The npm the hook runs: `packageManager` without a `+sha512…` hash.
const pinnedNpm = JSON.parse(readFileSync(join(repoRoot, 'Angular/package.json'), 'utf8')).packageManager.split('+')[0];

test('a staged Angular lock that drifts from its package.json is refused under the pinned npm', () => {
  const root = angularFixture();
  const lock = JSON.parse(readFileSync(join(root, 'Angular/package-lock.json'), 'utf8'));
  assert.equal(lock.packages['node_modules/left-pad'], undefined, 'the drift adds a package the lock lacks');
  const manifest = JSON.parse(readFileSync(join(root, 'Angular/package.json'), 'utf8'));
  manifest.dependencies['left-pad'] = '1.3.0';
  write(root, 'Angular/package.json', JSON.stringify(manifest, null, 2) + '\n');
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'drifted lock');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /Missing: left-pad@1\.3\.0 from lock file/);
  assert.ok(r.out.includes(pinnedNpm), `the refusal names ${pinnedNpm}:\n${r.out}`);
  assert.equal(commitCount(root), '1');
});

test("a staged Angular lock in sync with its package.json passes, as this repo's own does", () => {
  const root = angularFixture();
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'lock in sync');

  assert.equal(r.code, 0, r.out);
  assert.ok(r.out.includes(`the Angular lock is in sync under ${pinnedNpm}`), r.out);
  assert.equal(commitCount(root), '2');
});
