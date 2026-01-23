import { BaseEntity } from './base-entity';

export class LastMenuIconIndexClicked extends BaseEntity {
  index?: number;

  constructor({
    index,
  }: {
    index?: number;
  } = {}) {
    super();

    this.index = index;
  }

  static readonly typeName = 'LastMenuIconIndexClicked' as const;
}
