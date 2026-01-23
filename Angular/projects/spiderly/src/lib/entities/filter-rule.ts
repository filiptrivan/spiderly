import { MatchModeCodes } from '../enums/match-mode-enum-codes';

/**
 * Represents a filter rule used for querying or filtering data collections.
 *
 * The `FilterRule` class is a generic structure that defines a single filtering condition,
 * including the match mode (comparison operator), the value to compare, and an optional logical operator.
 *
 * The allowed match modes are determined by the type parameter `T`:
 * - For `string`: supports `Contains`, `StartsWith`, and `Equals`.
 * - For `boolean`: supports `Equals`.
 * - For `Date`: supports `Equals`, `GreaterThan`, and `LessThan`.
 * - For `number`: supports `Equals`, `GreaterThan`, `LessThan`, and `In`.
 * - For other types: allows any value from `MatchModeCodes`.
 *
 * @template T The type of the value to filter by.
 */
export class FilterRule<T = any> {
  matchMode: AllowedMatchModes<T>;
  value: T;
  operator?: string;
}

type AllowedMatchModes<T> = T extends string
  ? MatchModeCodes.Contains | MatchModeCodes.StartsWith | MatchModeCodes.Equals
  : T extends boolean
    ? MatchModeCodes.Equals
    : T extends Date
      ?
          | MatchModeCodes.Equals
          | MatchModeCodes.GreaterThan
          | MatchModeCodes.LessThan
      : T extends number
        ?
            | MatchModeCodes.Equals
            | MatchModeCodes.GreaterThan
            | MatchModeCodes.LessThan
            | MatchModeCodes.In
        : MatchModeCodes;
