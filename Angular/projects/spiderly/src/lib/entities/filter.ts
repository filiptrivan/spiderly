import { BaseEntity } from "./base-entity";
import { FilterSortMeta as FilterSortMeta } from "./filter-sort-meta";
import { FilterRule } from "./filter-rule";

export class Filter<T extends BaseEntity=any> extends BaseEntity
{
    filters?: { [K in keyof T]?: FilterRule[] };
    first?: number;
    rows?: number;
    sortField?: string;
    sortOrder?: number;
    multiSortMeta?: FilterSortMeta[];
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
        filters?: { [K in keyof T]?: FilterRule[] };
        first?: number;
        rows?: number;
        sortField?: string;
        sortOrder?: number;
        multiSortMeta?: FilterSortMeta[];
        additionalFilterIdInt?: number;
        additionalFilterIdLong?: number;
    } = {}
    ) {
        super();

        this.filters = filters;
        this.first = first;
        this.rows = rows;
        this.sortField = sortField;
        this.sortOrder = sortOrder;
        this.multiSortMeta = multiSortMeta;
        this.additionalFilterIdInt = additionalFilterIdInt;
        this.additionalFilterIdLong = additionalFilterIdLong;
    }

    static readonly typeName = 'Filter' as const; 
}