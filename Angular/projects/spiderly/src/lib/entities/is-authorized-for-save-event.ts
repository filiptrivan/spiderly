import { BaseEntity } from './base-entity';

export class IsAuthorizedForSaveEvent extends BaseEntity {
  isAuthorizedForSave?: boolean;

  constructor({
    isAuthorizedForSave,
  }: {
    isAuthorizedForSave?: boolean;
  } = {}) {
    super();

    this.isAuthorizedForSave = isAuthorizedForSave;
  }

  static readonly typeName = 'IsAuthorizedForSaveEvent' as const;
}
