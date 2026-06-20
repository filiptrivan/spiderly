import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseCodexUsage } from './codex.mjs';

// Codex `exec --json` emits a JSONL event stream (thread.started, turn.started,
// turn.completed, item.*, error); token usage lives on `turn.completed` events.
test('parseCodexUsage reads output tokens and turn count from one completed turn', () => {
  const stdout = [
    '{"type":"thread.started","thread_id":"t1"}',
    '{"type":"turn.started"}',
    '{"type":"turn.completed","usage":{"input_tokens":1200,"cached_input_tokens":1000,"output_tokens":345,"reasoning_output_tokens":20}}',
  ].join('\n');
  const { tokens, turns, usage } = parseCodexUsage(stdout);
  assert.equal(tokens, 345);
  assert.equal(turns, 1);
  assert.equal(usage.input_tokens, 1200);
});

test('parseCodexUsage uses the last (cumulative) turn for totals and counts every turn', () => {
  const stdout = [
    '{"type":"turn.started"}',
    '{"type":"turn.completed","usage":{"input_tokens":500,"output_tokens":100}}',
    '{"type":"turn.started"}',
    '{"type":"turn.completed","usage":{"input_tokens":1500,"output_tokens":380}}',
  ].join('\n');
  const { tokens, turns } = parseCodexUsage(stdout);
  assert.equal(turns, 2);
  assert.equal(tokens, 380); // cumulative totals → the last turn carries the run total
});

test('parseCodexUsage ignores non-turn.completed events and malformed/truncated lines', () => {
  const stdout = [
    '{"type":"thread.started"}',
    '{"type":"item.completed","item":{"type":"reasoning"}}',
    'not json at all',
    '{"type":"turn.completed","usage":{"input_tokens":900,"output_tokens":210}}',
    '{"type":"item.completed","item":{"type":"command_exec', // truncated trailing line
  ].join('\n');
  const { tokens, turns } = parseCodexUsage(stdout);
  assert.equal(turns, 1);
  assert.equal(tokens, 210);
});

test('parseCodexUsage returns zeroes when no turn completed (crash / turn.failed)', () => {
  const stdout = [
    '{"type":"thread.started"}',
    '{"type":"turn.started"}',
    '{"type":"turn.failed","error":{"message":"boom"}}',
  ].join('\n');
  const { tokens, turns, usage } = parseCodexUsage(stdout);
  assert.equal(tokens, 0);
  assert.equal(turns, 0);
  assert.deepEqual(usage, {});
});
