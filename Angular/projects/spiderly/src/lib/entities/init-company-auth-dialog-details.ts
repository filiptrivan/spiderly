import { BaseEntity } from './base-entity';

export class InitCompanyAuthDialogDetails extends BaseEntity {
  image?: string;
  companyName?: string;

  constructor({
    image,
    companyName,
  }: {
    image?: string;
    companyName?: string;
  } = {}) {
    super();

    this.image = image;
    this.companyName = companyName;
  }

  static readonly typeName = 'InitCompanyAuthDialogDetails' as const;
}
