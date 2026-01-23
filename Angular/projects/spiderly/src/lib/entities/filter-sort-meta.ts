export class FilterSortMeta {
  field: string;
  order: number;

  constructor({
    field,
    order,
  }: {
    field?: string;
    order?: number;
  } = {}) {
    this.field = field as string;
    this.order = order as number;
  }

  static schema = {
    field: {
      type: 'string',
    },
    order: {
      type: 'number',
    },
  } as const;

  static readonly typeName = 'FilterSortMeta' as const;
}
