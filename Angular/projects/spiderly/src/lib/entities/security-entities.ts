import { BaseEntity } from "./base-entity";
import { Namebook } from "./namebook";


export class UserBase extends BaseEntity
{
    static readonly typeName = 'UserBase' as const;

    id?: number;
    email?: string;

    constructor(
    {
        id,
        email,
    }:{
        id?: number;
        email?: string;
    } = {}
    ) {
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


export class RolePermission extends BaseEntity
{
    static readonly typeName = 'RolePermission' as const;

    roleDisplayName?: string;
    roleId?: number;
    permissionDisplayName?: string;
    permissionId?: number;

    constructor(
    {
        roleDisplayName,
        roleId,
        permissionDisplayName,
        permissionId
    }:{
        roleDisplayName?: string;
        roleId?: number;
        permissionDisplayName?: string;
        permissionId?: number;    
    } = {}
    ) {
        super(); 

        this.roleDisplayName = roleDisplayName;
        this.roleId = roleId;
        this.permissionDisplayName = permissionDisplayName;
        this.permissionId = permissionId;
    }

    static readonly schema = {
        roleDisplayName: {
            type: 'string',
        },
        roleId: {
            type: 'number',
        },
        permissionDisplayName: {
            type: 'string',
        },
        permissionId: {
            type: 'number',
        },
    } as const;
}


export class RolePermissionSaveBody extends BaseEntity
{
    static readonly typeName = 'RolePermissionSaveBody' as const;

    rolePermissionDTO?: RolePermission;

    constructor(
    {
        rolePermissionDTO
    }:{
        rolePermissionDTO?: RolePermission;    
    } = {}
    ) {
        super(); 

        this.rolePermissionDTO = rolePermissionDTO;
    }

    static readonly schema = {
        rolePermissionDTO: {
            type: 'RolePermission',
            get nestedConstructor() { return RolePermission; },
        },
    } as const;
}


export class AuthResult extends BaseEntity
{
    static readonly typeName = 'AuthResult' as const;

    userId?: number;
    email?: string;
    accessToken?: string;
    refreshToken?: string;

    constructor(
    {
        userId,
        email,
        accessToken,
        refreshToken
    }:{
        userId?: number;
        email?: string;
        accessToken?: string;
        refreshToken?: string;    
    } = {}
    ) {
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


export class VerificationTokenRequest extends BaseEntity
{
    static readonly typeName = 'VerificationTokenRequest' as const;

    verificationCode?: string;
    browserId?: string;
    email?: string;

    constructor(
    {
        verificationCode,
        browserId,
        email
    }:{
        verificationCode?: string;
        browserId?: string;
        email?: string;    
    } = {}
    ) {
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


export class ExternalProvider extends BaseEntity
{
    static readonly typeName = 'ExternalProvider' as const;

    idToken?: string;
    browserId?: string;

    constructor(
    {
        idToken,
        browserId
    }:{
        idToken?: string;
        browserId?: string;    
    } = {}
    ) {
        super(); 

        this.idToken = idToken;
        this.browserId = browserId;
    }

    static readonly schema = {
        idToken: {
            type: 'string',
        },
        browserId: {
            type: 'string',
        },
    } as const;
}


export class UserRole extends BaseEntity
{
    static readonly typeName = 'UserRole' as const;

    roleId?: number;
    userId?: number;

    constructor(
    {
        roleId,
        userId
    }:{
        roleId?: number;
        userId?: number;    
    } = {}
    ) {
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


export class UserRoleSaveBody extends BaseEntity
{
    static readonly typeName = 'UserRoleSaveBody' as const;

    userRoleDTO?: UserRole;

    constructor(
    {
        userRoleDTO
    }:{
        userRoleDTO?: UserRole;    
    } = {}
    ) {
        super(); 

        this.userRoleDTO = userRoleDTO;
    }

    static readonly schema = {
        userRoleDTO: {
            type: 'UserRole',
            get nestedConstructor() { return UserRole; },
        },
    } as const;
}


export class LoginVerificationToken extends BaseEntity
{
    static readonly typeName = 'LoginVerificationToken' as const;

    email?: string;
    userId?: number;
    browserId?: string;
    expireAt?: Date;

    constructor(
    {
        email,
        userId,
        browserId,
        expireAt
    }:{
        email?: string;
        userId?: number;
        browserId?: string;
        expireAt?: Date;    
    } = {}
    ) {
        super(); 

        this.email = email;
        this.userId = userId;
        this.browserId = browserId;
        this.expireAt = expireAt;
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
        expireAt: {
            type: 'Date',
        },
    } as const;
}


export class Login extends BaseEntity
{
    static readonly typeName = 'Login' as const;

    email?: string;
    browserId?: string;

    constructor(
    {
        email,
        browserId
    }:{
        email?: string;
        browserId?: string;    
    } = {}
    ) {
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


export class RefreshTokenRequest extends BaseEntity
{
    static readonly typeName = 'RefreshTokenRequest' as const;

    refreshToken?: string;
    browserId?: string;

    constructor(
    {
        refreshToken,
        browserId
    }:{
        refreshToken?: string;
        browserId?: string;    
    } = {}
    ) {
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


export class Role extends BaseEntity
{
    static readonly typeName = 'Role' as const;

    name?: string;
    description?: string;
    version?: number;
    id?: number;
    createdAt?: Date;
    modifiedAt?: Date;

    constructor(
    {
        name,
        description,
        version,
        id,
        createdAt,
        modifiedAt
    }:{
        name?: string;
        description?: string;
        version?: number;
        id?: number;
        createdAt?: Date;
        modifiedAt?: Date;    
    } = {}
    ) {
        super(); 

        this.name = name;
        this.description = description;
        this.version = version;
        this.id = id;
        this.createdAt = createdAt;
        this.modifiedAt = modifiedAt;
    }

    static readonly schema = {
        name: {
            type: 'string',
        },
        description: {
            type: 'string',
        },
        version: {
            type: 'number',
        },
        id: {
            type: 'number',
        },
        createdAt: {
            type: 'Date',
        },
        modifiedAt: {
            type: 'Date',
        },
    } as const;
}


export class RoleMainUIForm extends BaseEntity
{
    static readonly typeName = 'RoleMainUIForm' as const;

    roleDTO?: Role;
    usersNamebookDTOList?: Namebook[];
    permissionsIds?: number[];

    constructor(
    {
        roleDTO,
        usersNamebookDTOList = [],
        permissionsIds = []
    }:{
        roleDTO?: Role;
        usersNamebookDTOList?: Namebook[];
        permissionsIds?: number[];    
    } = {}
    ) {
        super(); 

        this.roleDTO = roleDTO;
        this.usersNamebookDTOList = usersNamebookDTOList;
        this.permissionsIds = permissionsIds;
    }

    static readonly schema = {
        roleDTO: {
            type: 'Role',
            get nestedConstructor() { return Role; },
            isMainDTOForMainUIFormDTO: true,
        },
        usersNamebookDTOList: {
            type: 'Namebook[]',
            get nestedConstructor() { return Namebook; },
        },
        permissionsIds: {
            type: 'number[]',
        },
    } as const;
}

export class RoleSaveBody extends BaseEntity
{
    static readonly typeName = 'RoleSaveBody' as const;

    roleDTO?: Role;
    selectedPermissionsIds?: number[];
    selectedUsersIds?: number[];

    constructor(
    {
        roleDTO,
        selectedPermissionsIds = [],
        selectedUsersIds = []
    }:{
        roleDTO?: Role;
        selectedPermissionsIds?: number[];
        selectedUsersIds?: number[];    
    } = {}
    ) {
        super(); 

        this.roleDTO = roleDTO;
        this.selectedPermissionsIds = selectedPermissionsIds;
        this.selectedUsersIds = selectedUsersIds;
    }

    static readonly schema = {
        roleDTO: {
            type: 'Role',
            get nestedConstructor() { return Role; },
            isSaveBodyMainDTO: true,
        },
        selectedPermissionsIds: {
            type: 'number[]',
        },
        selectedUsersIds: {
            type: 'number[]',
        },
    } as const;
}


export class Permission extends BaseEntity
{
    static readonly typeName = 'Permission' as const;

    name?: string;
    nameLatin?: string;
    description?: string;
    descriptionLatin?: string;
    code?: string;
    id?: number;

    constructor(
    {
        name,
        nameLatin,
        description,
        descriptionLatin,
        code,
        id
    }:{
        name?: string;
        nameLatin?: string;
        description?: string;
        descriptionLatin?: string;
        code?: string;
        id?: number;    
    } = {}
    ) {
        super(); 

        this.name = name;
        this.nameLatin = nameLatin;
        this.description = description;
        this.descriptionLatin = descriptionLatin;
        this.code = code;
        this.id = id;
    }

    static readonly schema = {
        name: {
            type: 'string',
        },
        nameLatin: {
            type: 'string',
        },
        description: {
            type: 'string',
        },
        descriptionLatin: {
            type: 'string',
        },
        code: {
            type: 'string',
        },
        id: {
            type: 'number',
        },
    } as const;
}


export class PermissionSaveBody extends BaseEntity
{
    static readonly typeName = 'PermissionSaveBody' as const;

    permissionDTO?: Permission;

    constructor(
    {
        permissionDTO
    }:{
        permissionDTO?: Permission;    
    } = {}
    ) {
        super(); 

        this.permissionDTO = permissionDTO;
    }

    static readonly schema = {
        permissionDTO: {
            type: 'Permission',
            get nestedConstructor() { return Permission; },
        },
    } as const;
}