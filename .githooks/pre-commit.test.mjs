// Runs .githooks/pre-commit through a real `git commit` in a throwaway repo per test, holding only the
// files the block under test reads: the agent bundle's builder and two authoring trees, the Angular
// manifest and lock, or the library's lint configs and rules with this repo's Angular packages linked in.
//
//   node --test .githooks/pre-commit.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
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

// The library's lint configs and rules, with this repo's installed Angular packages linked in.
const lib = 'Angular/projects/spiderly/src/lib';
const angularPackages = join(repoRoot, 'Angular/node_modules');

function lintFixture({ packages = true, files = {} } = {}) {
  if (packages) assert.ok(existsSync(angularPackages), "these tests link this repo's Angular packages: cd Angular && npm ci");
  const root = fixture(
    [
      'Angular/.gitignore',
      'Angular/angular.json',
      'Angular/package.json',
      'Angular/eslint.config.js',
      'Angular/projects/spiderly/eslint.config.js',
      'Angular/projects/spiderly/stylelint.config.mjs',
      'Angular/projects/spiderly/lint/no-untranslated-text.js',
      'Angular/projects/spiderly/lint/no-untranslated-text.test.mjs',
      'Angular/projects/spiderly/lint/library-key.js',
      'Angular/projects/spiderly/lint/library-key.test.mjs',
      'Angular/projects/spiderly/src/lib/i18n/en.json',
      'Angular/projects/spiderly/src/lib/components/spiderly-buttons/return-button/return-button.component.html',
      'Angular/projects/spiderly/lint/viewport-inset-fallback.mjs',
      'Angular/projects/spiderly/lint/stylelint.test.mjs',
    ],
    files,
  );
  if (packages) symlinkSync(angularPackages, join(root, 'Angular/node_modules'), 'dir');
  commitAll(root);
  return root;
}

test('a staged library template with static English is refused, naming the text and the fix', () => {
  const root = lintFixture();
  write(root, `${lib}/components/greeting/greeting.component.html`, '<h2>Welcome back</h2>\n');
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'static text');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /greeting\.component\.html/);
  assert.match(r.out, /Static text "Welcome back"/);
  assert.match(r.out, /\*transloco="let t"/);
  assert.equal(commitCount(root), '1');
});

test('a staged library stylesheet setting scroll-behavior is refused, naming the fix', () => {
  const root = lintFixture();
  write(root, `${lib}/styles/layout/_main.scss`, 'html {\n  scroll-behavior: smooth;\n}\n');
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'scroll-behavior');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /_main\.scss/);
  assert.match(r.out, /behavior: 'smooth'/);
  assert.equal(commitCount(root), '1');
});

test('a staged library template reading a key outside the library\'s scope is refused, naming the fix', () => {
  const root = lintFixture();
  write(root, `${lib}/components/greeting/greeting.component.html`, `<ng-container *transloco="let t"><h2>{{ t('WelcomeBack') }}</h2></ng-container>\n`);
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'unscoped key');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /greeting\.component\.html/);
  assert.match(r.out, /t\('spiderly\.WelcomeBack'\)/);
  assert.match(r.out, /i18n\/en\.json/);
  assert.equal(commitCount(root), '1');
});

test('a staged translated template and a stylesheet within the rules pass', () => {
  const root = lintFixture();
  write(root, `${lib}/components/greeting/greeting.component.html`, `<ng-container *transloco="let t"><h2>{{ t('spiderly.Return') }}</h2></ng-container>\n`);
  write(root, `${lib}/components/greeting/greeting.component.scss`, '.greeting {\n  top: var(--spiderly-viewport-top-inset, 0px);\n}\n');
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'translated');

  assert.equal(r.code, 0, r.out);
  assert.match(r.out, /the library's lint passes/);
  assert.equal(commitCount(root), '2');
});

// The shallow stylesheet is what a shell's own `**/*.scss` would stop at: sh's `**` is one directory.
test('a staged lint rule change lints the whole library, down to a file it does not stage', () => {
  const root = lintFixture({
    files: {
      [`${lib}/styles/shared.scss`]: '.shared {\n  margin: 0;\n}\n',
      [`${lib}/components/a/b/deep.component.html`]: '<h2>Welcome back</h2>\n',
      [`${lib}/components/a/b/deep.component.scss`]: 'html {\n  scroll-behavior: smooth;\n}\n',
    },
  });
  const rule = 'Angular/projects/spiderly/lint/no-untranslated-text.js';
  write(root, rule, `${readFileSync(join(root, rule), 'utf8')}// Touched.\n`);
  stage(root, rule);

  const r = git(root, 'commit', '-m', 'rule change');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /deep\.component\.html/);
  assert.match(r.out, /deep\.component\.scss/);
  assert.equal(commitCount(root), '1');
});

test('a staged en.json lints the whole library, down to a template that reads a key it dropped', () => {
  const root = lintFixture();
  const en = `${lib}/i18n/en.json`;
  write(root, en, '{}\n');
  stage(root, en);

  const r = git(root, 'commit', '-m', 'drop a key');

  assert.notEqual(r.code, 0, r.out);
  assert.doesNotMatch(r.out, /a lint rule's own test failed/);
  assert.match(r.out, /return-button\.component\.html/);
  assert.match(r.out, /"spiderly\.Return" has no English/);
  assert.equal(commitCount(root), '1');
});

test('a staged library file with no Angular packages installed is refused, naming the install', () => {
  const root = lintFixture({ packages: false });
  write(root, `${lib}/components/greeting/greeting.component.html`, '<h2>Welcome back</h2>\n');
  stage(root, 'Angular');

  const r = git(root, 'commit', '-m', 'no packages');

  assert.notEqual(r.code, 0, r.out);
  assert.match(r.out, /cd Angular && npm ci/);
  assert.equal(commitCount(root), '1');
});
