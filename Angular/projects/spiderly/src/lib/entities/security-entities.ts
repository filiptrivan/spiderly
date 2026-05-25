import { BaseEntity } from './base-entity';

export class UserBase extends BaseEntity {
  static readonly typeName = 'UserBase' as const;

  id?: number;
  email?: string;

  constructor({
    id,
    email,
  }: {
    id?: number;
    email?: string;
  } = {}) {
    super();

    this.id = id;
    this.email = email;
  }

  static readonly schema = {
    id: {
      type: 'number',
    },
    email: {
      type: 'string',
    },
  } as const;
}

export class AuthResult extends BaseEntity {
  static readonly typeName = 'AuthResult' as const;

  userId?: number;
  email?: string;
  accessToken?: string;
  refreshToken?: string;

  constructor({
    userId,
    email,
    accessToken,
    refreshToken,
  }: {
    userId?: number;
    email?: string;
    accessToken?: string;
    refreshToken?: string;
  } = {}) {
    super();

    this.userId = userId;
    this.email = email;
    this.accessToken = accessToken;
    this.refreshToken = refreshToken;
  }

  static readonly schema = {
    userId: {
      type: 'number',
    },
    email: {
      type: 'string',
    },
    accessToken: {
      type: 'string',
    },
    refreshToken: {
      type: 'string',
    },
  } as const;
}

export class VerificationTokenRequest extends BaseEntity {
  static readonly typeName = 'VerificationTokenRequest' as const;

  verificationCode?: string;
  browserId?: string;
  email?: string;

  constructor({
    verificationCode,
    browserId,
    email,
  }: {
    verificationCode?: string;
    browserId?: string;
    email?: string;
  } = {}) {
    super();

    this.verificationCode = verificationCode;
    this.browserId = browserId;
    this.email = email;
  }

  static readonly schema = {
    verificationCode: {
      type: 'string',
    },
    browserId: {
      type: 'string',
    },
    email: {
      type: 'string',
    },
  } as const;
}

export class ExternalProvider extends BaseEntity {
  static readonly typeName = 'ExternalProvider' as const;

  provider?: string;
  idToken?: string;
  browserId?: string;

  constructor({
    provider,
    idToken,
    browserId,
  }: {
    provider?: string;
    idToken?: string;
    browserId?: string;
  } = {}) {
    super();

    this.provider = provider;
    this.idToken = idToken;
    this.browserId = browserId;
  }

  static readonly schema = {
    provider: {
      type: 'string',
    },
    idToken: {
      type: 'string',
    },
    browserId: {
      type: 'string',
    },
  } as const;
}

export class ExternalProviderPublic extends BaseEntity {
  static readonly typeName = 'ExternalProviderPublic' as const;

  code?: string;
  authority?: string;
  clientId?: string;
  label?: string;

  constructor({
    code,
    authority,
    clientId,
    label,
  }: {
    code?: string;
    authority?: string;
    clientId?: string;
    label?: string;
  } = {}) {
    super();

    this.code = code;
    this.authority = authority;
    this.clientId = clientId;
    this.label = label;
  }

  static readonly schema = {
    code: {
      type: 'string',
    },
    authority: {
      type: 'string',
    },
    clientId: {
      type: 'string',
    },
    label: {
      type: 'string',
    },
  } as const;
}

export class UserRole extends BaseEntity {
  static readonly typeName = 'UserRole' as const;

  roleId?: number;
  userId?: number;

  constructor({
    roleId,
    userId,
  }: {
    roleId?: number;
    userId?: number;
  } = {}) {
    super();

    this.roleId = roleId;
    this.userId = userId;
  }

  static readonly schema = {
    roleId: {
      type: 'number',
    },
    userId: {
      type: 'number',
    },
  } as const;
}

export class LoginVerificationToken extends BaseEntity {
  static readonly typeName = 'LoginVerificationToken' as const;

  email?: string;
  userId?: number;
  browserId?: string;
  expiresAt?: Date;

  constructor({
    email,
    userId,
    browserId,
    expiresAt,
  }: {
    email?: string;
    userId?: number;
    browserId?: string;
    expiresAt?: Date;
  } = {}) {
    super();

    this.email = email;
    this.userId = userId;
    this.browserId = browserId;
    this.expiresAt = expiresAt;
  }

  static readonly schema = {
    email: {
      type: 'string',
    },
    userId: {
      type: 'number',
    },
    browserId: {
      type: 'string',
    },
    expiresAt: {
      type: 'Date',
    },
  } as const;
}

export class Login extends BaseEntity {
  static readonly typeName = 'Login' as const;

  email?: string;
  browserId?: string;

  constructor({
    email,
    browserId,
  }: {
    email?: string;
    browserId?: string;
  } = {}) {
    super();

    this.email = email;
    this.browserId = browserId;
  }

  static readonly schema = {
    email: {
      type: 'string',
    },
    browserId: {
      type: 'string',
    },
  } as const;
}

export class RefreshTokenRequest extends BaseEntity {
  static readonly typeName = 'RefreshTokenRequest' as const;

  refreshToken?: string;
  browserId?: string;

  constructor({
    refreshToken,
    browserId,
  }: {
    refreshToken?: string;
    browserId?: string;
  } = {}) {
    super();

    this.refreshToken = refreshToken;
    this.browserId = browserId;
  }

  static readonly schema = {
    refreshToken: {
      type: 'string',
    },
    browserId: {
      type: 'string',
    },
  } as const;
}

export class AuthResultWithCookies extends BaseEntity {
  static readonly typeName = 'AuthResultWithCookies' as const;

  userId?: number;
  email?: string;
  accessTokenExpiresAt?: Date;

  constructor({
    userId,
    email,
    accessTokenExpiresAt,
  }: {
    userId?: number;
    email?: string;
    accessTokenExpiresAt?: Date;
  } = {}) {
    super();

    this.userId = userId;
    this.email = email;
    this.accessTokenExpiresAt = accessTokenExpiresAt;
  }

  static readonly schema = {
    userId: {
      type: 'number',
    },
    email: {
      type: 'string',
    },
    accessTokenExpiresAt: {
      type: 'Date',
    },
  } as const;
}

export class SendLoginVerificationEmailResult extends BaseEntity {
  static readonly typeName = 'SendLoginVerificationEmailResult' as const;

  message?: string;

  constructor({
    message,
  }: {
    message?: string;
  } = {}) {
    super();

    this.message = message;
  }

  static readonly schema = {
    message: {
      type: 'string',
    },
  } as const;
}
