import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PROMPTS } from './showcase.mjs';

test('PROMPTS name the domain and only the spiderly side mentions Spiderly', () => {
  assert.match(PROMPTS.spiderly, /product-catalog admin panel/);
  assert.match(PROMPTS.plain, /product-catalog admin panel/);
  assert.match(PROMPTS.spiderly, /with Spiderly/i);
  assert.doesNotMatch(PROMPTS.plain, /spiderly/i);
});
