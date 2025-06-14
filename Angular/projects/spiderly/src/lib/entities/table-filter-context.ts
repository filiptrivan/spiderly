import { MatchModeCodes } from "../enums/match-mode-enum-codes";

export class TableFilterContext<T=any>
{
    matchMode: AllowedMatchModes<T>;
    value: T;
    operator?: string;
}

type AllowedMatchModes<T> = 
    T extends string ? MatchModeCodes.Contains | MatchModeCodes.StartsWith | MatchModeCodes.Equals :
    T extends boolean ? MatchModeCodes.Equals :
    T extends Date ? MatchModeCodes.Equals | MatchModeCodes.GreaterThan | MatchModeCodes.LessThan :
    T extends number ? MatchModeCodes.Equals | MatchModeCodes.GreaterThan | MatchModeCodes.LessThan | MatchModeCodes.In :
    MatchModeCodes;