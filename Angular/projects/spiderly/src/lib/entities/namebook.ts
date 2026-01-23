import { BaseEntity } from '../entities/base-entity';

export class Namebook extends BaseEntity {
  id?: any;
  displayName?: string;

  constructor({
    id,
    displayName,
  }: {
    id?: any;
    displayName?: string;
  } = {}) {
    super();

    this.id = id;
    this.displayName = displayName;
  }

  static schema = {
    id: {
      type: 'any',
    },
    displayName: {
      type: 'string',
    },
  } as const;

  static readonly typeName = 'Namebook' as const;
}
