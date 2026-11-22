// The rules of stylelint.config.mjs, each on a fixture linted as a file at its library path.
//
//   cd Angular && npm run test:lint-rules

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import stylelint from 'stylelint';

const project = join(dirname(fileURLToPath(import.meta.url)), '..');

async function warnings(path, code) {
  const { results } = await stylelint.lint({
    code,
    codeFilename: join(project, path),
    configFile: join(project, 'stylelint.config.mjs'),
  });
  return results[0].warnings.map((w) => w.text);
}

const component = 'src/lib/components/fixture/fixture.component.scss';

test('scroll-behavior is refused, naming the call-site fix', async () => {
  const found = await warnings('src/lib/styles/layout/_main.scss', `html {\n  scroll-behavior: smooth;\n}\n`);

  assert.equal(found.length, 1, found.join('\n'));
  assert.match(found[0], /scroll-behavior/);
  assert.match(found[0], /behavior: 'smooth'/);
  assert.match(found[0], /prefers-reduced-motion/);
});

test('the viewport inset with its 0px fallback passes', async () => {
  const found = await warnings(component, `.row {\n  scroll-margin-top: calc(var(--spiderly-viewport-top-inset, 0px) + 1rem);\n}\n`);

  assert.deepEqual(found, []);
});

test('the viewport inset without its 0px fallback is refused in a component, naming the fallback', async () => {
  const found = await warnings(
    component,
    `.bar {\n  top: var(--spiderly-viewport-top-inset);\n  margin-top: calc(var(--spiderly-viewport-top-inset, 1rem) * -1);\n}\n`,
  );

  assert.equal(found.length, 2, found.join('\n'));
  for (const text of found) assert.match(text, /var\(--spiderly-viewport-top-inset, 0px\)/);
  assert.match(found[0], /phantom gap/);
});

test('the layout files beside the declaration read the inset without a fallback', async () => {
  for (const file of ['_topbar', '_content', '_menu']) {
    const found = await warnings(`src/lib/styles/layout/${file}.scss`, `.layout-x {\n  top: var(--spiderly-viewport-top-inset);\n}\n`);

    assert.deepEqual(found, [], file);
  }
});
