import { BaseEntity } from "./base-entity";
import { Filter } from "./filter";
import { FilterRule } from "./filter-rule";
import { FilterSortMeta } from "./filter-sort-meta";
import { MimeTypes } from "./mime-type";
import { Namebook } from "./namebook";


export class UserBase extends BaseEntity
{
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
        super('UserBase'); 

        this.id = id;
		this.email = email;
    }
}


export class RolePermission extends BaseEntity
{
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
        super('RolePermission'); 

        this.roleDisplayName = roleDisplayName;
		this.roleId = roleId;
		this.permissionDisplayName = permissionDisplayName;
		this.permissionId = permissionId;
    }
}


export class RolePermissionSaveBody extends BaseEntity
{
    rolePermissionDTO?: RolePermission;

    constructor(
    {
        rolePermissionDTO
    }:{
        rolePermissionDTO?: RolePermission;     
    } = {}
    ) {
        super('RolePermissionSaveBody'); 

        this.rolePermissionDTO = rolePermissionDTO;
    }
}


export class AuthResult extends BaseEntity
{
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
        super('AuthResult'); 

        this.userId = userId;
		this.email = email;
		this.accessToken = accessToken;
		this.refreshToken = refreshToken;
    }
}


export class VerificationTokenRequest extends BaseEntity
{
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
        super('VerificationTokenRequest'); 

        this.verificationCode = verificationCode;
		this.browserId = browserId;
		this.email = email;
    }
}


export class ExternalProvider extends BaseEntity
{
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
        super('ExternalProvider'); 

        this.idToken = idToken;
		this.browserId = browserId;
    }
}


export class UserRole extends BaseEntity
{
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
        super('UserRole'); 

        this.roleId = roleId;
		this.userId = userId;
    }
}


export class UserRoleSaveBody extends BaseEntity
{
    userRoleDTO?: UserRole;

    constructor(
    {
        userRoleDTO
    }:{
        userRoleDTO?: UserRole;     
    } = {}
    ) {
        super('UserRoleSaveBody'); 

        this.userRoleDTO = userRoleDTO;
    }
}


export class LoginVerificationToken extends BaseEntity
{
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
        super('LoginVerificationToken'); 

        this.email = email;
		this.userId = userId;
		this.browserId = browserId;
		this.expireAt = expireAt;
    }
}


export class Login extends BaseEntity
{
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
        super('Login'); 

        this.email = email;
		this.browserId = browserId;
    }
}


export class RefreshTokenRequest extends BaseEntity
{
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
        super('RefreshTokenRequest'); 

        this.refreshToken = refreshToken;
		this.browserId = browserId;
    }
}


export class Role extends BaseEntity
{
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
        super('Role'); 

        this.name = name;
		this.description = description;
		this.version = version;
		this.id = id;
		this.createdAt = createdAt;
		this.modifiedAt = modifiedAt;
    }
}


export class RoleMainUIForm extends BaseEntity
{
    roleDTO?: Role;
	usersNamebookDTOList?: Namebook[];
	permissionsNamebookDTOList?: Namebook[];

    constructor(
    {
        roleDTO,
        usersNamebookDTOList,
        permissionsNamebookDTOList
    }:{
        roleDTO?: Role;
        usersNamebookDTOList?: Namebook[];
        permissionsNamebookDTOList?: Namebook[];     
    } = {}
    ) {
        super('RoleMainUIForm'); 

        this.roleDTO = roleDTO;
        this.usersNamebookDTOList = usersNamebookDTOList;
        this.permissionsNamebookDTOList = permissionsNamebookDTOList;
    }
}

export class RoleSaveBody extends BaseEntity
{
    roleDTO?: Role;
	selectedPermissionsIds?: number[];
	selectedUsersIds?: number[];

    constructor(
    {
        roleDTO,
		selectedPermissionsIds,
		selectedUsersIds
    }:{
        roleDTO?: Role;
		selectedPermissionsIds?: number[];
		selectedUsersIds?: number[];     
    } = {}
    ) {
        super('RoleSaveBody'); 

        this.roleDTO = roleDTO;
		this.selectedPermissionsIds = selectedPermissionsIds;
		this.selectedUsersIds = selectedUsersIds;
    }
}


export class Permission extends BaseEntity
{
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
        super('Permission'); 

        this.name = name;
		this.nameLatin = nameLatin;
		this.description = description;
		this.descriptionLatin = descriptionLatin;
		this.code = code;
		this.id = id;
    }
}


export class PermissionSaveBody extends BaseEntity
{
    permissionDTO?: Permission;

    constructor(
    {
        permissionDTO
    }:{
        permissionDTO?: Permission;     
    } = {}
    ) {
        super('PermissionSaveBody'); 

        this.permissionDTO = permissionDTO;
    }
}

