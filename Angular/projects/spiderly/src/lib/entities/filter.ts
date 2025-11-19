import { BaseEntity } from "./base-entity";
import { FilterRule } from "./filter-rule";
import { FilterSortMeta } from "./filter-sort-meta";

export class Filter<T extends BaseEntity = any> extends BaseEntity {
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
        }: {
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

    static schema = {
        filters: {
            type: 'Object',
            // Note: 'filters' is a dynamic map (Dictionary), so we usually treat it as a generic Object
            // unless strict typing is required for the inner FilterRule[] values by the serializer.
        },
        first: {
            type: 'number',
        },
        rows: {
            type: 'number',
        },
        sortField: {
            type: 'string',
        },
        sortOrder: {
            type: 'number',
        },
        multiSortMeta: {
            type: 'FilterSortMeta[]',
            get nestedConstructor() { return FilterSortMeta; },
        },
        additionalFilterIdInt: {
            type: 'number',
        },
        additionalFilterIdLong: {
            type: 'number',
        },
    } as const;

    static readonly typeName = 'Filter' as const;
}