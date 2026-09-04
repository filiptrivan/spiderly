import { computed, signal, Signal } from '@angular/core';

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

export function numberFilter(config: {
  label: string;
}): FilterDefinition<'number'> {
  return { kind: 'number', label: config.label };
}

/**
 * The kind is what carries BOTH halves — which operators are legal and what the value is. It has
 * to stay a literal all the way from the factory to here: typing the definition's `kind` as the
 * widened `FilterValueKind` silently allows every operator on every filter, which is the shape
 * this first had (the runtime guard still fired, so only a `@ts-expect-error` pin caught it).
 */
/**
 * `In` is the only multi-valued operator MatchModeCodes has, so it is the only one whose value is
 * a list. Distributing over the operator union yields a discriminated union rather than a loose
 * `T | T[]`, so `{ operator: Equals, value: [2, 3] }` is a compile error rather than a query the
 * paginator answers in some way nobody predicted.
 */
type ConstraintFor<TKind extends FilterValueKind, TOperator> =
  TOperator extends MatchModeCodes.In
    ? { operator: TOperator; value: ValueByKind[TKind][] | null | undefined }
    : { operator: TOperator; value: ValueByKind[TKind] | null | undefined };

export type FilterConstraint<TKind extends FilterValueKind> = ConstraintFor<
  TKind,
  (typeof ALLOWED_OPERATORS)[TKind][number]
>;

/** What the maps hold. The public generic precision is for the CALLER; internally it is noise. */
interface StoredConstraint {
  operator: MatchModeCodes;
  value: unknown;
}

/** One applied constraint, in the shape the chip bar draws. */
export interface AppliedFilter {
  id: string;
  label: string;
  operator: MatchModeCodes;
  value: unknown;
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
  // Two stores, and the split is the whole point: `set` writes a DRAFT, `commit` publishes it.
  // A chip drawn off a draft would repeat the mistake the header's filter icon already shipped —
  // claiming the grid is narrowed on the first keystroke. Controls that apply on change
  // (multiselect, boolean, date) call both at once; a text box commits on Enter or blur.
  const drafts = new Map<keyof TDefs, StoredConstraint>();
  const committed = signal<ReadonlyMap<keyof TDefs, StoredConstraint>>(
    new Map(),
  );

  const applied: Signal<AppliedFilter[]> = computed(() =>
    [...committed()].map(([id, constraint]) => ({
      id: id as string,
      label: definitions[id].label,
      operator: constraint.operator,
      value: constraint.value,
    })),
  );

  return {
    definitions,
    applied,

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

      drafts.set(id, constraint);
    },

    /**
     * Publishes the draft. A blanked draft REMOVES the constraint rather than committing an empty
     * one, so `applied()` and the payload agree: no chip, no key, no `contains ''`.
     */
    commit<K extends keyof TDefs>(id: K): void {
      const draft = drafts.get(id);
      const next = new Map(committed());

      if (draft === undefined || isBlank(draft.value)) next.delete(id);
      else next.set(id, draft);

      committed.set(next);
    },

    /**
     * The chip's `x`. Drops the DRAFT as well as the committed constraint: a surviving draft
     * leaves the control still showing the cleared text, and the next commit would restore a
     * filter nobody re-typed.
     */
    reset<K extends keyof TDefs>(id: K): void {
      drafts.delete(id);

      const next = new Map(committed());
      next.delete(id);
      committed.set(next);
    },

    /** The `filters` half of `Filter` — what the generated paginator reads. */
    toFilterPayload(): Record<string, FilterRule[]> {
      const payload: Record<string, FilterRule[]> = {};

      for (const [id, constraint] of committed()) {
        payload[id as string] = [
          { matchMode: constraint.operator, value: constraint.value } as FilterRule,
        ];
      }

      return payload;
    },
  };
}
