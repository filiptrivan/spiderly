import { test } from 'node:test';
import assert from 'node:assert/strict';
import { run } from './exec.mjs';

test('run captures stdout and a zero exit code', () => {
  const r = run(process.execPath, ['-e', "process.stdout.write('hi')"]);
  assert.equal(r.code, 0);
  assert.equal(r.stdout, 'hi');
});

test('run reports a non-zero exit code without throwing', () => {
  const r = run(process.execPath, ['-e', 'process.exit(3)']);
  assert.equal(r.code, 3);
});
