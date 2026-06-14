// Headline score: a task is green iff ALL its `required` checks passed.
// Sub-checks are retained on the row for diagnosis but never averaged into the headline.
export function scoreRow(task, verifyResult) {
  const byName = new Map(verifyResult.checks.map((c) => [c.name, c.pass]));
  const pass = task.required.every((name) => byName.get(name) === true);
  return { pass };
}
