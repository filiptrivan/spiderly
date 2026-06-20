import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { walkFiles } from './fs-utils.mjs';

test('walkFiles lists source files as sorted POSIX paths and skips build output / deps', () => {
  const root = mkdtempSync(join(tmpdir(), 'fsu-'));
  mkdirSync(join(root, 'Backend', 'Entities'), { recursive: true });
  mkdirSync(join(root, 'node_modules', 'left-pad'), { recursive: true });
  mkdirSync(join(root, 'Backend', 'bin'), { recursive: true });
  writeFileSync(join(root, 'Backend', 'Entities', 'Product.cs'), 'class Product {}');
  writeFileSync(join(root, 'README.md'), '# app');
  writeFileSync(join(root, 'node_modules', 'left-pad', 'index.js'), 'module.exports={}');
  writeFileSync(join(root, 'Backend', 'bin', 'app.dll'), 'binary');

  assert.deepEqual(walkFiles(root), ['Backend/Entities/Product.cs', 'README.md']);
});

test('walkFiles returns [] for an empty directory', () => {
  const root = mkdtempSync(join(tmpdir(), 'fsu-'));
  assert.deepEqual(walkFiles(root), []);
});
