import { MatchModeCodes } from '../enums/match-mode-enum-codes';

/**
 * What each value type may be asked — the ONE operator table, mirroring what the generated
 * paginator implements per type (`PaginatedResultGenerator`): `In` on a string column answers
 * with InvalidMatchMode, PrimeNG's own text list adds three modes the backend 400s, and so on.
 *
 * Deliberately a LEAF module (imports nothing but the codes) so both consumers can derive from
 * it without a cycle: the store's runtime validation and offered lists (`filter-store.ts`), and
 * the compile-time `FilterRule.matchMode` union (`entities/filter-rule.ts`). These used to be
 * hand-kept copies; edit the table here and every surface follows.
 *
 * Tuple order is DISPLAY order for every operator picker (Equals, then "less/before", then
 * "more/after" — the order the header dropdowns always shipped).
 */
export const ALLOWED_OPERATORS = {
  text: [
    MatchModeCodes.StartsWith,
    MatchModeCodes.Contains,
    MatchModeCodes.Equals,
  ],
  number: [
    MatchModeCodes.Equals,
    MatchModeCodes.LessThan,
    MatchModeCodes.GreaterThan,
    MatchModeCodes.In,
  ],
  boolean: [MatchModeCodes.Equals],
  date: [
    MatchModeCodes.Equals,
    MatchModeCodes.LessThan,
    MatchModeCodes.GreaterThan,
  ],
} as const satisfies Record<string, readonly MatchModeCodes[]>;

export type FilterValueKind = keyof typeof ALLOWED_OPERATORS;

/** The compile-time half of one kind's row, derived so it can never drift from the runtime half. */
export type AllowedOperatorFor<TKind extends FilterValueKind> =
  (typeof ALLOWED_OPERATORS)[TKind][number];
