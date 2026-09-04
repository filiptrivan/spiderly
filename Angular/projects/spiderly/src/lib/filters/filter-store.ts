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

/**
 * Transloco keys for the operator a control offers. Reuses the keys the data table's
 * `matchModeDateOptions` / `matchModeTextOptions` / `matchModeNumberOptions` already populate, so
 * no new seed is needed and one operator cannot be worded two ways in one admin. The wording is
 * per KIND on purpose: `LessThan` reads "Less than" for a number and "Dates before" for a date.
 *
 * Note for whoever touches this next: "which operators exist per kind" is now recorded in THREE
 * hand-kept places — this file's ALLOWED_OPERATORS, `AllowedMatchModes<T>` in
 * entities/filter-rule.ts, and those three option lists in the data table. The option lists are
 * derived from ALLOWED_OPERATORS here so this file adds no fourth; collapse the other two when
 * either is next edited.
 */
const OPERATOR_LABEL_KEY: Record<
  FilterValueKind,
  Partial<Record<MatchModeCodes, string>>
> = {
  text: {
    [MatchModeCodes.StartsWith]: 'StartsWith',
    [MatchModeCodes.Contains]: 'Contains',
    [MatchModeCodes.Equals]: 'Equals',
  },
  number: {
    [MatchModeCodes.Equals]: 'Equals',
    [MatchModeCodes.GreaterThan]: 'MoreThan',
    [MatchModeCodes.LessThan]: 'LessThan',
    [MatchModeCodes.In]: 'In',
  },
  boolean: { [MatchModeCodes.Equals]: 'Equals' },
  date: {
    [MatchModeCodes.Equals]: 'OnDate',
    [MatchModeCodes.LessThan]: 'DatesBefore',
    [MatchModeCodes.GreaterThan]: 'DatesAfter',
  },
};

export interface OperatorOption {
  value: MatchModeCodes;
  /** Transloco key, resolved by the control — the store holds no translations. */
  labelKey: string;
}

/** Derived from ALLOWED_OPERATORS, so the offered list and the accepted list cannot disagree. */
export function operatorOptions(kind: FilterValueKind): OperatorOption[] {
  return ALLOWED_OPERATORS[kind].map((value) => ({
    value,
    labelKey: OPERATOR_LABEL_KEY[kind][value] ?? value,
  }));
}

/**
 * The operator applied when nobody picked one, which has to be what a person MEANS by filling in
 * the control. `Contains` for a fragment typed into a box; `GreaterThan` for a date, never
 * `Equals` — a date equality against a TIMESTAMP matches only the row written in that exact
 * second, and answers with an empty grid rather than an error (PACMS ran into this on all three
 * of its date columns, Filip 2026-08-28).
 */
export const DEFAULT_OPERATOR = {
  text: MatchModeCodes.Contains,
  number: MatchModeCodes.Equals,
  boolean: MatchModeCodes.Equals,
  date: MatchModeCodes.GreaterThan,
} as const satisfies Record<FilterValueKind, MatchModeCodes>;

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

export function booleanFilter(config: {
  label: string;
}): FilterDefinition<'boolean'> {
  return { kind: 'boolean', label: config.label };
}

export function dateFilter(config: {
  label: string;
}): FilterDefinition<'date'> {
  return { kind: 'date', label: config.label };
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

/**
 * What the CHIP BAR needs from a store, and no more: it reads the applied set and removes from it.
 * Narrow on purpose — it keeps the bar free of the store's generics, so the bar renders any store
 * without the two types having to agree on filter ids.
 */
/** One filter, addressed on its own. The placement API, and what the bar's editor drives. */
export interface FilterHandle {
  id: string;
  label: string;
  kind: FilterValueKind;
  /** The DRAFT — what a control shows. `applied()` answers the other question. */
  value: Signal<unknown>;
  operator: Signal<MatchModeCodes | undefined>;
  /** Every operator this filter's kind accepts, in display order. */
  operators: OperatorOption[];
  set(constraint: { operator: MatchModeCodes; value: unknown }): void;
  commit(): void;
  reset(): void;
}

export interface FilterBarSource {
  /** Every DECLARED filter, which is what "+ Filter" offers — not every visible column. */
  definitions: Record<string, FilterDefinition<FilterValueKind>>;
  applied: Signal<AppliedFilter[]>;
  get(id: string): FilterHandle;
  reset(id: string): void;
}

/** What the TABLE needs: the bar's half, plus the payload it sends to the paginator. */
export interface FilterSource extends FilterBarSource {
  toFilterPayload(): Record<string, FilterRule[]>;
}

/** One applied constraint, in the shape the chip bar draws. */
export interface AppliedFilter {
  id: string;
  label: string;
  /** The bar reads this to word the chip — `String(false)` would print "false" at an operator. */
  kind: FilterValueKind;
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
  // A half-typed datepicker yields an Invalid Date, which JSON.stringify writes as `null`.
  if (value instanceof Date) return Number.isNaN(value.getTime());

  return false;
}

export function createFilterStore<
  TDefs extends Record<string, FilterDefinition<FilterValueKind>>,
>(definitions: TDefs) {
  // Two stores, and the split is the whole point: `set` writes a DRAFT, `commit` publishes it.
  // A chip drawn off a draft would repeat the mistake the header's filter icon already shipped —
  // claiming the grid is narrowed on the first keystroke. Controls that apply on change
  // (multiselect, boolean, date) call both at once; a text box commits on Enter or blur.
  const drafts = signal<ReadonlyMap<keyof TDefs, StoredConstraint>>(new Map());

  const writeDraft = (id: keyof TDefs, constraint: StoredConstraint | null) => {
    const next = new Map(drafts());
    if (constraint === null) next.delete(id);
    else next.set(id, constraint);
    drafts.set(next);
  };
  const committed = signal<ReadonlyMap<keyof TDefs, StoredConstraint>>(
    new Map(),
  );

  const applied: Signal<AppliedFilter[]> = computed(() =>
    [...committed()].map(([id, constraint]) => ({
      id: id as string,
      label: definitions[id].label,
      kind: definitions[id].kind,
      operator: constraint.operator,
      value: constraint.value,
    })),
  );

  const store = {
    definitions,
    applied,

    /**
     * A handle bound to one filter. This is the PLACEMENT api: it needs nothing from the component
     * tree, so the same filter can be driven from the bar, from a header cell, or from a drawer
     * rendered at app root. `value` is the DRAFT — a control shows what was typed, not what was
     * applied; `applied()` is the other question and the bar asks that one.
     */
    get<K extends keyof TDefs>(id: K) {
      return {
        id: id as string,
        label: definitions[id].label,
        kind: definitions[id].kind,
        value: computed(() => drafts().get(id)?.value),
        operator: computed(() => drafts().get(id)?.operator),
        operators: operatorOptions(definitions[id].kind),
        set: (constraint: FilterConstraint<TDefs[K]['kind']>) =>
          store.set(id, constraint),
        commit: () => store.commit(id),
        reset: () => store.reset(id),
      };
    },

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

      writeDraft(id, constraint);
    },

    /**
     * Publishes the draft. A blanked draft REMOVES the constraint rather than committing an empty
     * one, so `applied()` and the payload agree: no chip, no key, no `contains ''`.
     */
    commit<K extends keyof TDefs>(id: K): void {
      const draft = drafts().get(id);
      const current = committed();
      const shouldRemove = draft === undefined || isBlank(draft.value);

      // A new Map is a new identity even with identical contents, and everything downstream
      // reacts to identity — so a no-op commit would spend a request. Bail before building one.
      if (shouldRemove ? !current.has(id) : current.get(id) === draft) return;

      const next = new Map(current);
      if (shouldRemove) next.delete(id);
      else next.set(id, draft);

      committed.set(next);
    },

    /**
     * The chip's `x`. Drops the DRAFT as well as the committed constraint: a surviving draft
     * leaves the control still showing the cleared text, and the next commit would restore a
     * filter nobody re-typed.
     */
    reset<K extends keyof TDefs>(id: K): void {
      if (drafts().has(id)) writeDraft(id, null);
      if (!committed().has(id)) return;

      const next = new Map(committed());
      next.delete(id);
      committed.set(next);
    },

    /** The bar's "Clear all". `reset` for every filter, drafts included. */
    clear(): void {
      if (drafts().size > 0) drafts.set(new Map());
      if (committed().size > 0) committed.set(new Map());
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

  return store;
}
