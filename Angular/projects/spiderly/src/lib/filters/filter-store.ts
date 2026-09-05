import { computed, signal, Signal } from '@angular/core';

import { FilterRule } from '../entities/filter-rule';
import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import {
  ALLOWED_OPERATORS,
  AllowedOperatorFor,
  FilterValueKind,
} from './allowed-operators';

/** One tickable choice. `value` is the filter's own value type — an id, not a display string. */
export interface FilterOption {
  value: unknown;
  label: string;
}

/**
 * A filter declaration. It carries its VALUE type, never a UI control name — that separation is
 * the whole point: `multiselect` is a control, and it emits `In`, which `AllowedMatchModes` allows
 * on a number and forbids on a string. Conflating the two is what shipped `paymentGatewayCode` as
 * a text filter with a hand-written comment instead of a compile error.
 */
export interface FilterDefinition<TKind extends FilterValueKind = FilterValueKind> {
  kind: TKind;
  label: string;
  /**
   * Declaring options turns this filter into a pick-list, which means `In` — the same rule as the
   * table's `[filters]`, where the SHAPE of what is supplied decides, not a mode flag. It is also
   * the only way "Processing OR PreparingForShipping" can be expressed at all.
   */
  options?: FilterOption[];
  /** Offered-operator narrowing; semantics on `FilterConfig.operators`, the factories' door. */
  operators?: MatchModeCodes[];
  /** Whether "+ Filter" offers it; semantics on `FilterConfig.offered`, the factories' door. */
  offered?: boolean;
}

/**
 * Transloco keys for the operator a control offers. The keys are seeded in the init template's
 * en block (`NetAndAngularFilesGenerator.cs`), so one operator cannot be worded two ways in one
 * admin. The wording is per KIND on purpose: `LessThan` reads "Less than" for a number and
 * "Dates before" for a date.
 *
 * The operator TABLE itself lives in `allowed-operators.ts`, the one telling both consumers
 * derive from (this store's runtime lists, `FilterRule`'s compile-time union).
 */
interface OperatorWords {
  /** The picker's option label. */
  pickerKey: string;
  /**
   * The chip's inline phrase, which cannot reuse the picker key: those are capitalised option
   * labels and one of them is a plural noun, so a chip would read "Datum Datumi pre 1.9." A chip
   * is a sentence — "Firma sadrži Elektromont" — and needs its own words.
   */
  phraseKey: string;
}

const OPERATOR_WORDS: Record<
  FilterValueKind,
  Partial<Record<MatchModeCodes, OperatorWords>>
> = {
  text: {
    [MatchModeCodes.StartsWith]: {
      pickerKey: 'StartsWith',
      phraseKey: 'FilterChipStartsWith',
    },
    [MatchModeCodes.Contains]: {
      pickerKey: 'Contains',
      phraseKey: 'FilterChipContains',
    },
    [MatchModeCodes.Equals]: {
      pickerKey: 'Equals',
      phraseKey: 'FilterChipEquals',
    },
    // No `In` for text: ALLOWED_OPERATORS.text does not carry it, so the entry was unreachable —
    // and it made `textFilter({ options })` look supported when the store throws on it.
  },
  number: {
    [MatchModeCodes.Equals]: {
      pickerKey: 'Equals',
      phraseKey: 'FilterChipEquals',
    },
    [MatchModeCodes.GreaterThan]: {
      pickerKey: 'MoreThan',
      phraseKey: 'FilterChipGreaterThan',
    },
    [MatchModeCodes.LessThan]: {
      pickerKey: 'LessThan',
      phraseKey: 'FilterChipLessThan',
    },
    // The picker never renders for a pick-list (one operator), but the CHIP always does.
    [MatchModeCodes.In]: { pickerKey: 'In', phraseKey: 'FilterChipIn' },
  },
  boolean: {
    [MatchModeCodes.Equals]: {
      pickerKey: 'Equals',
      phraseKey: 'FilterChipEquals',
    },
  },
  date: {
    [MatchModeCodes.Equals]: {
      pickerKey: 'OnDate',
      phraseKey: 'FilterChipEquals',
    },
    [MatchModeCodes.LessThan]: {
      pickerKey: 'DatesBefore',
      phraseKey: 'FilterChipBefore',
    },
    [MatchModeCodes.GreaterThan]: {
      pickerKey: 'DatesAfter',
      phraseKey: 'FilterChipAfter',
    },
  },
};

export interface OperatorOption {
  value: MatchModeCodes;
  /** Transloco key, resolved by the control — the store holds no translations. */
  labelKey: string;
}

/**
 * `In` and options are the same fact seen from two sides: `In` needs a list of values, and the
 * editor only draws one when the filter declares options. So options mean `In` and nothing else,
 * and their absence rules `In` out — offering it on a plain number filter would hand the
 * operator a mode with no control behind it.
 *
 * Widened first: the per-kind tuples are literal, so `boolean`'s `[Equals]` makes the `In`
 * comparison a "no overlap" error rather than an empty result.
 */
function baseOperators(
  kind: FilterValueKind,
  hasOptions: boolean,
): readonly MatchModeCodes[] {
  const accepted: readonly MatchModeCodes[] = ALLOWED_OPERATORS[kind];

  return accepted.filter((value) =>
    hasOptions ? value === MatchModeCodes.In : value !== MatchModeCodes.In,
  );
}

function toOperatorOptions(
  kind: FilterValueKind,
  operators: readonly MatchModeCodes[],
): OperatorOption[] {
  return operators.map((value) => ({
    value,
    labelKey: OPERATOR_WORDS[kind][value]?.pickerKey ?? value,
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

/**
 * What every factory takes, so narrowing does not have to be re-plumbed per factory (its adjacent
 * asymmetry — `options` on `numberFilter` only — is FORCED and documented there; this one is not).
 *
 * `operators` narrows what the editor OFFERS, in display order; the first entry becomes the
 * default. Typed per kind, so declaring an operator the kind cannot answer is a build error at
 * the declaration — the store's runtime drop-and-report covers only what dodges the type system
 * (a dynamically built array). The store still accepts every wire-legal operator on restore, so
 * an old persisted constraint keeps filtering: the narrowing shapes the controls, not the
 * contract. The forcing case: date-equality against a timestamp matches one second and answers
 * with an empty grid, so PACMS's order dates offer only "posle"/"pre".
 */
interface FilterConfig<TKind extends FilterValueKind> {
  label: string;
  operators?: AllowedOperatorFor<TKind>[];
  /**
   * `false` keeps the filter out of the bar's "+ Filter" menu — for a filter with a DEDICATED
   * control somewhere on the page (PACMS's order search box is a placement of the store's
   * `mixedSearch`), where the generic entry point would give one question two homes. The chip
   * still renders when the filter is applied, so the bar's claim to list every constraint holds.
   */
  offered?: boolean;
}

export function textFilter(config: FilterConfig<'text'>): FilterDefinition<'text'> {
  return {
    kind: 'text',
    label: config.label,
    operators: config.operators,
    offered: config.offered,
  };
}

/**
 * Only a NUMBER filter accepts options, and the asymmetry is forced rather than chosen: options
 * mean `In`, and `In` is the one operator ALLOWED_OPERATORS grants to numbers alone — because the
 * generated paginator answers it with InvalidMatchMode on a string column. A text pick-list used
 * to type-check and then throw at the first `set`.
 */
export function numberFilter(
  config: FilterConfig<'number'> & { options?: FilterOption[] },
): FilterDefinition<'number'> {
  return {
    kind: 'number',
    label: config.label,
    options: config.options,
    operators: config.operators,
    offered: config.offered,
  };
}

export function booleanFilter(
  config: FilterConfig<'boolean'>,
): FilterDefinition<'boolean'> {
  return {
    kind: 'boolean',
    label: config.label,
    operators: config.operators,
    offered: config.offered,
  };
}

export function dateFilter(config: FilterConfig<'date'>): FilterDefinition<'date'> {
  return {
    kind: 'date',
    label: config.label,
    operators: config.operators,
    offered: config.offered,
  };
}

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

/**
 * The kind is what carries BOTH halves — which operators are legal and what the value is. It has
 * to stay a literal all the way from the factory to here: typing the definition's `kind` as the
 * widened `FilterValueKind` silently allows every operator on every filter, which is the shape
 * this first had (the runtime guard still fired, so only a `@ts-expect-error` pin caught it).
 */
export type FilterConstraint<TKind extends FilterValueKind> = ConstraintFor<
  TKind,
  (typeof ALLOWED_OPERATORS)[TKind][number]
>;

/** What the maps hold. The public generic precision is for the CALLER; internally it is noise. */
interface StoredConstraint {
  operator: MatchModeCodes;
  value: unknown;
}

/** One filter, addressed on its own. The placement API, and what the bar's editor drives. */
export interface FilterHandle {
  id: string;
  label: string;
  kind: FilterValueKind;
  /** The DRAFT — what a control shows. `applied()` answers the other question. */
  value: Signal<unknown>;
  operator: Signal<MatchModeCodes | undefined>;
  /** Every operator this filter accepts, in display order. */
  operators: OperatorOption[];
  /** Applied when nobody picked an operator. */
  defaultOperator: MatchModeCodes;
  /** The tickable choices, when this filter declares any. */
  options?: FilterOption[];
  set(constraint: { operator: MatchModeCodes; value: unknown }): void;
  commit(): void;
  reset(): void;
}

/**
 * What the CHIP BAR needs from a store, and no more: it reads the applied set and removes from it.
 * Narrow on purpose — it keeps the bar free of the store's generics, so the bar renders any store
 * without the two types having to agree on filter ids.
 */
export interface FilterBarSource {
  /** Every DECLARED filter, which is what "+ Filter" offers — not every visible column. */
  definitions: Record<string, FilterDefinition<FilterValueKind>>;
  applied: Signal<AppliedFilter[]>;
  get(id: string): FilterHandle;
  reset(id: string): void;
  clear(): void;
}

/** What the TABLE needs: the bar's half, plus the payload it sends to the paginator. */
export interface FilterSource extends FilterBarSource {
  toFilterPayload(): Record<string, FilterRule[]>;
  /** The applied set as plain JSON, for whoever owns this table's persisted state. */
  snapshot(): FilterSnapshot;
  /** Replaces the applied set, and the drafts with it so every control agrees with its chip. */
  restore(snapshot: FilterSnapshot): void;
}

/** The applied constraints in a form that survives `JSON.stringify`. */
export type FilterSnapshot = Record<
  string,
  { operator: MatchModeCodes; value: unknown }
>;

/** The shape `Date.toJSON` writes, which is the only value here JSON cannot round-trip itself. */
const ISO_DATE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/;

function reviveValue(value: unknown): unknown {
  if (typeof value === 'string' && ISO_DATE.test(value)) return new Date(value);
  if (Array.isArray(value)) return value.map(reviveValue);

  return value;
}

/**
 * One sort key as the bar draws it. Sort deliberately does NOT live in the filter store:
 * `multiSortMeta` already holds it, keyed by field, and a copy here would be a second source of
 * truth for one fact. The bar is one surface reading two owners.
 */
export interface SortKeyLabel {
  label: string;
  descending: boolean;
}

/** One applied constraint, in the shape the chip bar draws. */
export interface AppliedFilter {
  id: string;
  label: string;
  /** The bar reads this to word the chip — `String(false)` would print "false" at an operator. */
  kind: FilterValueKind;
  operator: MatchModeCodes;
  /**
   * Transloco key for the chip's middle word. Without it a chip reads "Firma Elektromont", which
   * leaves the reader to guess whether the grid is narrowed by `contains` or by `equals` — very
   * different sets for one typed value, on a surface whose only claim is that it cannot lie.
   */
  operatorPhraseKey: string;
  value: unknown;
}

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

/**
 * Whether two constraint values are the same QUESTION. Identity is not it: every `set` builds a
 * fresh draft, a multiselect hands back a fresh array per change, and two Dates at one instant
 * are one constraint. This is what `commit` bails on, so a paste-over-itself or a double Apply
 * cannot spend a request — the rule PACMS's order list used to hand-roll as `lastCommittedTerm`.
 */
function valueEquals(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (a instanceof Date && b instanceof Date) return a.getTime() === b.getTime();
  if (Array.isArray(a) && Array.isArray(b)) {
    return a.length === b.length && a.every((item, i) => valueEquals(item, b[i]));
  }

  return false;
}

function constraintEquals(
  a: StoredConstraint | undefined,
  b: StoredConstraint,
): boolean {
  return a != null && a.operator === b.operator && valueEquals(a.value, b.value);
}

/**
 * The filter engine. It knows nothing about columns: a filter has an id, a value type and a
 * constraint, and that is the entire vocabulary.
 */
export function createFilterStore<
  TDefs extends Record<string, FilterDefinition<FilterValueKind>>,
>(definitions: TDefs) {
  // Per-filter offered operators, resolved ONCE per declaration — so an invalid narrowing is
  // said once, not on every editor open — and re-resolved only where the inputs can actually
  // change: `setOptions`, because options' PRESENCE flips a filter between pick-list (`In`) and
  // plain operators. A per-store Map rather than the module-level WeakMap this first shipped
  // as: that memo was keyed on a definition object consumers mutate, so late-filled options
  // could freeze a filter's operators at the wrong answer.
  const resolveOffered = (id: keyof TDefs) => {
    const definition = definitions[id];
    const base = baseOperators(definition.kind, definition.options != null);

    let codes: readonly MatchModeCodes[] = base;
    if (definition.operators?.length) {
      const survivors = definition.operators.filter((operator) =>
        base.includes(operator),
      );

      // Same polarity as the column path's computeMatchModes: a bad entry is dropped and said
      // out loud, and a narrowing that leaves nothing falls back to the full list — an editor
      // with no operators would be unusable, which is worse than an unwanted one. Mostly
      // unreachable now that FilterConfig types `operators` per kind; kept for dynamically
      // built arrays.
      if (survivors.length < definition.operators.length) {
        console.error(
          `Filter '${definition.label}' narrows to operators its ${definition.kind} kind does not ` +
            `offer (${definition.operators.join(', ')}; offered: ${base.join(', ')}). ` +
            `Unsupported entries are ignored${survivors.length ? '' : '; falling back to the full list'}.`,
        );
      }

      codes = survivors.length ? survivors : base;
    }

    return { codes, options: toOperatorOptions(definition.kind, codes) };
  };

  const offered = new Map<
    keyof TDefs,
    { codes: readonly MatchModeCodes[]; options: OperatorOption[] }
  >();
  for (const id of Object.keys(definitions) as (keyof TDefs)[]) {
    offered.set(id, resolveOffered(id));
  }

  /**
   * The operator applied when nobody picked one. Options mean `In`, whatever the kind's default;
   * a narrowed filter defaults to its FIRST declared operator.
   */
  const defaultOperatorFor = (id: keyof TDefs): MatchModeCodes => {
    const definition = definitions[id];
    if (definition.options != null) return MatchModeCodes.In;

    return definition.operators?.length
      ? offered.get(id)!.codes[0]
      : DEFAULT_OPERATOR[definition.kind];
  };

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
      operatorPhraseKey:
        OPERATOR_WORDS[definitions[id].kind][constraint.operator]?.phraseKey ??
        constraint.operator,
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
        // Getters, not snapshots: options can land AFTER a handle is taken — the bar's editor
        // holds one for the life of the popover, and PACMS fills its lookups asynchronously —
        // and a snapshot left an already-open pick-list empty until it was reopened. The
        // operator list rides along, since setOptions can flip it to `In`.
        get operators() {
          return offered.get(id)!.options;
        },
        get defaultOperator() {
          return defaultOperatorFor(id);
        },
        get options() {
          return definitions[id].options;
        },
        set: (constraint: FilterConstraint<TDefs[K]['kind']>) =>
          store.set(id, constraint),
        commit: () => store.commit(id),
        reset: () => store.reset(id),
      };
    },

    /**
     * The ONE seam for choices that arrive after the store is built (PACMS fills its pick-lists
     * from lookups that race a deploy). Never write `definitions[id].options` by hand: options'
     * PRESENCE is what makes a filter a pick-list, so the setter also re-resolves the offered
     * operators — a hand write would leave a late-filled filter offering `Equals` on a control
     * that emits a list.
     */
    setOptions<K extends keyof TDefs>(id: K, options: FilterOption[]): void {
      definitions[id].options = options;
      offered.set(id, resolveOffered(id));
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
     * `set` + `commit` in one breath — the shape every PROGRAMMATIC write takes (a view's
     * `apply`, the sheet flow). The split API stays for controls, where draft-on-type /
     * publish-on-Apply is the point; but a bare `set` whose `commit` is forgotten (or written
     * with the wrong id — the pair repeats the string twice) fails silently: the draft is
     * written, nothing is published, and a view narrows by nothing under a labelled tab.
     */
    setAndCommit<K extends keyof TDefs>(
      id: K,
      constraint: FilterConstraint<TDefs[K]['kind']>,
    ): void {
      store.set(id, constraint);
      store.commit(id);
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
      // reacts to identity — so a no-op commit would spend a request. The bail compares the
      // CONSTRAINT, not the draft object: every set() builds a fresh draft, and the case that
      // matters is an identical value re-committed (see valueEquals).
      if (shouldRemove ? !current.has(id) : constraintEquals(current.get(id), draft)) return;

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

    /**
     * The applied set, plain enough to store. Drafts are deliberately NOT included: a half-typed
     * value is not a state anyone should come back to, and the bar's whole claim is that what it
     * shows is what narrows the grid.
     */
    snapshot(): FilterSnapshot {
      const snapshot: FilterSnapshot = {};

      for (const [id, constraint] of committed()) {
        snapshot[id as string] = {
          operator: constraint.operator,
          value: constraint.value,
        };
      }

      return snapshot;
    },

    /**
     * Replaces the applied set. The drafts are written too, so reopening a restored chip shows
     * the value it is filtering by rather than an empty control.
     *
     * Ids the store no longer declares are dropped rather than carried: a filter removed from the
     * declaration in a later release would otherwise ride every request from anyone whose storage
     * still held it, with no chip able to name it and no way to clear it from the UI.
     */
    restore(snapshot: FilterSnapshot): void {
      const next = new Map<keyof TDefs, StoredConstraint>();

      for (const [id, constraint] of Object.entries(snapshot ?? {})) {
        if (!(id in definitions)) continue;

        next.set(id as keyof TDefs, {
          operator: constraint.operator,
          value: reviveValue(constraint.value),
        });
      }

      drafts.set(new Map(next));
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
