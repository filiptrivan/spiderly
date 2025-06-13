import { BaseEntity } from "../entities/base-entity";
import { TableFilterContext } from "../entities/table-filter-context";
import { TableFilterSortMeta } from "../entities/table-filter-sort-meta";
import { MatchModeCodes } from "../enums/match-mode-enum-codes";

export class TableFilter<T extends BaseEntity=any> extends BaseEntity
{
    filters?: { [K in keyof T]?: { value: any; matchMode: MatchModeCodes }[] };
    first?: number;
    rows?: number;
    sortField?: string;
    sortOrder?: number;
    multiSortMeta?: TableFilterSortMeta[];
    additionalFilterIdInt?: number;
    additionalFilterIdLong?: number;
  
    constructor(
    {
        filters,
        first,
        rows,
        sortField,
        sortOrder,
        multiSortMeta,
        additionalFilterIdInt,
        additionalFilterIdLong,
    }:{
        filters?: { [K in keyof T]?: { value: any; matchMode: MatchModeCodes }[] };
        first?: number;
        rows?: number;
        sortField?: string;
        sortOrder?: number;
        multiSortMeta?: TableFilterSortMeta[];
        additionalFilterIdInt?: number;
        additionalFilterIdLong?: number;
    } = {}
    ) {
        super('TableFilter');

        this.filters = filters;
        this.first = first;
        this.rows = rows;
        this.sortField = sortField;
        this.sortOrder = sortOrder;
        this.multiSortMeta = multiSortMeta;
        this.additionalFilterIdInt = additionalFilterIdInt;
        this.additionalFilterIdLong = additionalFilterIdLong;
    }
}