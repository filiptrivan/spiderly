// Render a Matrix into a markdown summary table + a failure digest grouped by task.
// Pass rate counts only true/false rows; pass:null (infra) is tallied separately.
export function renderReport(matrix) {
  const groups = new Map();
  for (const r of matrix.rows) {
    const k = `${r.agent}__${r.taskId}`;
    if (!groups.has(k)) groups.set(k, { agent: r.agent, taskId: r.taskId, pass: 0, total: 0, infra: 0 });
    const g = groups.get(k);
    if (r.pass === null) g.infra++;
    else { g.total++; if (r.pass) g.pass++; }
  }

  const lines = [
    `# Eval report — ${matrix.meta.runId}`,
    '',
    `Track: ${matrix.meta.track} · reps: ${matrix.meta.reps}`,
    '',
    '| Agent | Task | Pass rate | Infra errors |',
    '|---|---|---|---|',
  ];
  for (const g of [...groups.values()].sort((a, b) => a.agent.localeCompare(b.agent) || a.taskId.localeCompare(b.taskId))) {
    // total===0 means no scored reps (all infra errors) — render '—' so it reads distinctly from a real 0/N.
    const rate = g.total === 0 ? '—' : `${g.pass}/${g.total}`;
    lines.push(`| ${g.agent} | ${g.taskId} | ${rate} | ${g.infra} |`);
  }

  const fails = matrix.rows.filter((r) => r.pass === false);
  if (fails.length) {
    lines.push('', '## Failure digest', '');
    for (const r of fails) {
      const failed = (r.checks ?? []).filter((c) => !c.pass).map((c) => c.name).join(', ') || '(no checks)';
      lines.push(`- **${r.agent}/${r.taskId}** rep ${r.rep} — failed: ${failed}`);
    }
  }
  return lines.join('\n');
}
