import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import { AllowedOperatorFor } from '../filters/allowed-operators';

/**
 * Represents a filter rule used for querying or filtering data collections.
 *
 * The `FilterRule` class is a generic structure that defines a single filtering condition,
 * including the match mode (comparison operator), the value to compare, and an optional logical operator.
 *
 * The allowed match modes are determined by the type parameter `T`, each union derived from the
 * one operator table in `filters/allowed-operators.ts` (for other types, any `MatchModeCodes`
 * value is allowed).
 *
 * @template T The type of the value to filter by.
 */
export class FilterRule<T = any> {
  matchMode: AllowedMatchModes<T>;
  value: T;
  operator?: string;
}

type AllowedMatchModes<T> = T extends string
  ? AllowedOperatorFor<'text'>
  : T extends boolean
    ? AllowedOperatorFor<'boolean'>
    : T extends Date
      ? AllowedOperatorFor<'date'>
      : T extends number
        ? AllowedOperatorFor<'number'>
        : MatchModeCodes;
