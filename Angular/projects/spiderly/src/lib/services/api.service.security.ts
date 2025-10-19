import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Namebook } from '../entities/namebook';
import { Filter } from '../entities/filter';
import { Login, RefreshTokenRequest, AuthResult, Role, UserBase, ExternalProvider, VerificationTokenRequest, RoleSaveBody, RoleMainUIForm } from '../entities/security-entities';
import { ConfigBaseService } from './config-base.service';
import { PaginatedResult } from '../entities/paginated-result';

@Injectable({
    providedIn: 'root'
})
export class ApiSecurityService {

    constructor(
        protected http: HttpClient,
        protected config: ConfigBaseService
    ) {
        
    }

    //#region Authentication

    login = (request: VerificationTokenRequest): Observable<AuthResult> => { 
        return this.http.post<AuthResult>(`${this.config.apiUrl}/Security/Login`, request, this.config.httpOptions);
    }

    loginExternal = (externalProviderDTO: ExternalProvider): Observable<AuthResult> => { 
        return this.http.post<AuthResult>(`${this.config.apiUrl}/Security/LoginExternal`, externalProviderDTO, this.config.httpOptions);
    }

    sendLoginVerificationEmail = (loginDTO: Login): Observable<any> => { 
        return this.http.post<any>(`${this.config.apiUrl}/Security/SendLoginVerificationEmail`, loginDTO, this.config.httpOptions);
    }


    logout = (browserId: string): Observable<any> => { 
        return this.http.get<any>(`${this.config.apiUrl}/Security/Logout?browserId=${browserId}`);
    }

    refreshToken = (request: RefreshTokenRequest): Observable<AuthResult> => { 
        return this.http.post<AuthResult>(`${this.config.apiUrl}/Security/RefreshToken`, request, this.config.httpOptions);
    }

    //#endregion

    //#region User

    getCurrentUserBase = (): Observable<UserBase> => { 
        return this.http.get<UserBase>(`${this.config.apiUrl}/Security/GetCurrentUserBase`, this.config.httpSkipSpinnerOptions);
    }

    getCurrentUserPermissionCodes = (): Observable<string[]> => { 
        return this.http.get<string[]>(`${this.config.apiUrl}/Security/GetCurrentUserPermissionCodes`, this.config.httpSkipSpinnerOptions);
    }

    //#endregion

    //#region Role

    getPaginatedRoleList = (dto: Filter): Observable<PaginatedResult> => { 
        return this.http.post<PaginatedResult>(`${this.config.apiUrl}/Security/GetPaginatedRoleList`, dto, this.config.httpSkipSpinnerOptions);
    }

    exportRoleListToExcel = (dto: Filter): Observable<any> => { 
        return this.http.post<any>(`${this.config.apiUrl}/Security/ExportRoleListToExcel`, dto, this.config.httpOptions);
    }

    deleteRole = (id: number): Observable<any> => { 
        return this.http.delete<any>(`${this.config.apiUrl}/Security/DeleteRole?id=${id}`);
    }

    getRoleMainUIFormDTO = (id: number): Observable<RoleMainUIForm> => {
        return this.http.get<RoleMainUIForm>(`${this.config.apiUrl}/Security/GetRoleMainUIFormDTO?id=${id}`);
    }

    getRole = (id: number): Observable<Role> => {
        return this.http.get<Role>(`${this.config.apiUrl}/Security/GetRole?id=${id}`);
    }

    saveRole = (dto: RoleSaveBody): Observable<Role> => { 
        return this.http.put<Role>(`${this.config.apiUrl}/Security/SaveRole`, dto, this.config.httpOptions);
    }

    getUsersNamebookListForRole = (roleId: number): Observable<Namebook[]> => {
        return this.http.get<Namebook[]>(`${this.config.apiUrl}/Security/GetUsersNamebookListForRole?roleId=${roleId}`, this.config.httpSkipSpinnerOptions);
    }

    getPermissionsDropdownListForRole = (): Observable<Namebook[]> => {
        return this.http.get<Namebook[]>(`${this.config.apiUrl}/Security/GetPermissionsDropdownListForRole`, this.config.httpSkipSpinnerOptions);
    }

    getPermissionsNamebookListForRole = (roleId: number): Observable<Namebook[]> => {
        return this.http.get<Namebook[]>(`${this.config.apiUrl}/Security/GetPermissionsNamebookListForRole?roleId=${roleId}`, this.config.httpSkipSpinnerOptions);
    }

    getUsersAutocompleteListForRole = (limit: number, query: string): Observable<Namebook[]> => {
        return this.http.get<Namebook[]>(`${this.config.apiUrl}/Security/GetUsersAutocompleteListForRole?limit=${limit}&query=${query}`, this.config.httpSkipSpinnerOptions);
    }

    //#endregion

    //#region Notification

    getUnreadNotificationsCountForCurrentUser = (): Observable<number> => { 
        return this.http.get<number>(`${this.config.apiUrl}/Notification/GetUnreadNotificationsCountForCurrentUser`, this.config.httpSkipSpinnerOptions);
    }

    //#endregion

}

