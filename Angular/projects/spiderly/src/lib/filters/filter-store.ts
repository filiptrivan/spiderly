import { FilterRule } from '../entities/filter-rule';
import { MatchModeCodes } from '../enums/match-mode-enum-codes';

/**
 * A filter declaration. It carries its VALUE type, never a UI control name — that separation is
 * the whole point: `multiselect` is a control, and it emits `In`, which `AllowedMatchModes` allows
 * on a number and forbids on a string. Conflating the two is what shipped `paymentGatewayCode` as
 * a text filter with a hand-written comment instead of a compile error.
 */
export interface FilterDefinition<TKind extends FilterValueKind = FilterValueKind> {
  kind: TKind;
  label: string;
}

/**
 * What each value type may be asked. ONE telling, and the compile-time half is derived from it
 * below rather than written twice — a second hand-kept copy is how the offered list and the
 * accepted list drift apart.
 *
 * Its older twin is `AllowedMatchModes<T>` in `entities/filter-rule.ts`, keyed by TS type instead
 * of by kind. Collapse the two when `FilterRule` next moves; they must not be edited apart.
 */
const ALLOWED_OPERATORS = {
  text: [
    MatchModeCodes.Contains,
    MatchModeCodes.StartsWith,
    MatchModeCodes.Equals,
  ],
  number: [
    MatchModeCodes.Equals,
    MatchModeCodes.GreaterThan,
    MatchModeCodes.LessThan,
    MatchModeCodes.In,
  ],
  boolean: [MatchModeCodes.Equals],
  date: [
    MatchModeCodes.Equals,
    MatchModeCodes.GreaterThan,
    MatchModeCodes.LessThan,
  ],
} as const satisfies Record<string, readonly MatchModeCodes[]>;

export type FilterValueKind = keyof typeof ALLOWED_OPERATORS;

/** The value each kind carries. Exhaustive over the kinds by construction. */
interface ValueByKind {
  text: string;
  number: number;
  boolean: boolean;
  date: Date;
}

export function textFilter(config: { label: string }): FilterDefinition<'text'> {
  return { kind: 'text', label: config.label };
}

/**
 * The kind is what carries BOTH halves — which operators are legal and what the value is. It has
 * to stay a literal all the way from the factory to here: typing the definition's `kind` as the
 * widened `FilterValueKind` silently allows every operator on every filter, which is the shape
 * this first had (the runtime guard still fired, so only a `@ts-expect-error` pin caught it).
 */
export interface FilterConstraint<TKind extends FilterValueKind> {
  operator: (typeof ALLOWED_OPERATORS)[TKind][number];
  value: ValueByKind[TKind] | null | undefined;
}

/**
 * The filter engine. It knows nothing about columns: a filter has an id, a value type and a
 * constraint, and that is the entire vocabulary.
 */
/**
 * A blanked value is NOT a constraint. `contains ''` matches every row, so emitting one would
 * narrow nothing while the bar claims the grid is filtered.
 */
function isBlank(value: unknown): boolean {
  if (value === null || value === undefined) return true;
  if (typeof value === 'string') return value.trim() === '';
  if (Array.isArray(value)) return value.length === 0;

  return false;
}

export function createFilterStore<
  TDefs extends Record<string, FilterDefinition<FilterValueKind>>,
>(definitions: TDefs) {
  const constraints = new Map<keyof TDefs, FilterConstraint<FilterValueKind>>();

  return {
    definitions,

    set<K extends keyof TDefs>(
      id: K,
      constraint: FilterConstraint<TDefs[K]['kind']>,
    ): void {
      const kind = definitions[id].kind;

      // Throws rather than logging and ignoring, which is what `computeMatchModes` does for a
      // column's OFFERED match modes. Different stakes: that one narrows a dropdown, so a bad
      // entry merely fails to show, while this one assembles a QUERY — dropping it silently would
      // leave the operator looking at unfiltered rows believing they are filtered.
      if (!ALLOWED_OPERATORS[kind].includes(constraint.operator as never)) {
        throw new Error(
          `Filter '${String(id)}' is ${kind}, which does not accept the '${constraint.operator}' ` +
            `operator (allowed: ${ALLOWED_OPERATORS[kind].join(', ')}). The generated paginator ` +
            `answers an unsupported match mode with InvalidMatchMode.`,
        );
      }

      constraints.set(id, constraint);
    },

    /** The `filters` half of `Filter` — what the generated paginator reads. */
    toFilterPayload(): Record<string, FilterRule[]> {
      const payload: Record<string, FilterRule[]> = {};

      for (const [id, constraint] of constraints) {
        if (isBlank(constraint.value)) continue;

        payload[id as string] = [
          { matchMode: constraint.operator, value: constraint.value } as FilterRule,
        ];
      }

      return payload;
    },
  };
}
