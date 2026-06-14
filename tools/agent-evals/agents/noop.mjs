// Fake agent that does nothing. Must score ~0% — proves verifiers are not too lax.
export default {
  name: 'noop',
  async run() {
    return { transcript: '', tokens: 0, costUsd: 0, wallMs: 0, turns: 0, cleanExit: true };
  },
};
