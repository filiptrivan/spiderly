import { BaseEntity } from "./base-entity";

export class IsAuthorizedForSaveEvent extends BaseEntity
{
    isAuthorizedForSave?: boolean;
    currentUserPermissionCodes?: string[];

    constructor(
    {
        isAuthorizedForSave,
        currentUserPermissionCodes,
    }:{
        isAuthorizedForSave?: boolean;
        currentUserPermissionCodes?: string[];
    } = {}
    ) {
        super(); 

        this.isAuthorizedForSave = isAuthorizedForSave;
        this.currentUserPermissionCodes = currentUserPermissionCodes;
    }

    static readonly typeName = 'IsAuthorizedForSaveEvent' as const;
}