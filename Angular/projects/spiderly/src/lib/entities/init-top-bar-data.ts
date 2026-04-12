import { BaseEntity } from './base-entity';
import { UserBase } from './security-entities';

export class InitTopBarData extends BaseEntity {
  companyName?: string;
  userProfilePath?: string;
  showProfileIcon?: boolean;
  currentUser?: UserBase;

  constructor({
    companyName,
    userProfilePath,
    showProfileIcon,
    currentUser,
  }: {
    companyName?: string;
    userProfilePath?: string;
    showProfileIcon?: boolean;
    currentUser?: UserBase;
  } = {}) {
    super();

    this.companyName = companyName;
    this.userProfilePath = userProfilePath;
    this.showProfileIcon = showProfileIcon;
    this.currentUser = currentUser;
  }

  static readonly typeName = 'InitTopBarData' as const;
}
