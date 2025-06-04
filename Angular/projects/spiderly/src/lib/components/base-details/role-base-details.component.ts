import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { ActivatedRoute } from '@angular/router';
import { combineLatest, forkJoin, map, Observable, of, Subscription } from 'rxjs';
import { MenuItem } from 'primeng/api';
import { CardSkeletonComponent } from '../card-skeleton/card-skeleton.component';
import { SpiderlyFormArray, SpiderlyFormControl, SpiderlyFormGroup } from '../spiderly-form-control/spiderly-form-control';
import { BaseEntity } from '../../entities/base-entity';
import { LastMenuIconIndexClicked } from '../../entities/last-menu-icon-index-clicked';
import { Role, RoleSaveBody } from '../../entities/security-entities';
import { SpiderlyButton } from '../../entities/spiderly-button';
import { getControl, getPrimengAutocompleteNamebookOptions, getPrimengDropdownNamebookOptions, nameof } from '../../services/helper-functions';
import { BaseFormService } from '../../services/base-form.service';
import { ApiSecurityService } from '../../services/api.service.security';
import { PrimengOption } from '../../entities/primeng-option';
import { AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { IsAuthorizedForSaveEvent } from '../../entities/is-authorized-for-save-event';
import { AuthBaseService } from '../../services/auth-base.service';
import { SpiderlyTextboxComponent } from '../../controls/spiderly-textbox/spiderly-textbox.component';
import { SpiderlyTextareaComponent } from '../../controls/spiderly-textarea/spiderly-textarea.component';
import { SpiderlyMultiAutocompleteComponent } from '../../controls/spiderly-multiautocomplete/spiderly-multiautocomplete.component';
import { SpiderlyMultiSelectComponent } from '../../controls/spiderly-multiselect/spiderly-multiselect.component';
import { SpiderlyPanelsModule } from '../spiderly-panels/spiderly-panels.module';
import { SpiderlyButtonComponent } from '../spiderly-buttons/spiderly-button/spiderly-button.component';
import { SpiderlyReturnButtonComponent } from '../spiderly-buttons/return-button/return-button.component';

@Component({
    selector: 'role-base-details',
    templateUrl: 'role-base-details.component.html',
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        TranslocoDirective,
        CardSkeletonComponent,
        SpiderlyTextboxComponent,
        SpiderlyTextareaComponent,
        SpiderlyMultiAutocompleteComponent,
        SpiderlyMultiSelectComponent,
        SpiderlyPanelsModule,
        SpiderlyButtonComponent,
        SpiderlyReturnButtonComponent
    ]
})
export class RoleBaseDetailsComponent {
    @Output() onSave = new EventEmitter<void>();
    @Output() onRoleFormGroupInitFinish = new EventEmitter<void>();
    @Input() getCrudMenuForOrderedData: (formArray: SpiderlyFormArray, modelConstructor: BaseEntity, lastMenuIconIndexClicked: LastMenuIconIndexClicked, adjustFormArrayManually: boolean) => MenuItem[];
    @Input() formGroup: SpiderlyFormGroup;
    @Input() roleFormGroup: SpiderlyFormGroup<Role>;
    @Input() additionalButtons: SpiderlyButton[] = [];

    authorizationForSaveSubscription: Subscription;
    @Input() authorizedForSaveObservable: () => Observable<boolean> = () => of(false);
    isAuthorizedForSave: boolean = false;
    @Output() onIsAuthorizedForSaveChange = new EventEmitter<IsAuthorizedForSaveEvent>(); 
    
    modelId: number;
    loading: boolean = true;
    roleSaveBodyName: string = nameof<RoleSaveBody>('roleDTO');

    usersForRoleOptions: PrimengOption[];
    permissionsForRoleOptions: PrimengOption[];

    selectedUsersForRole = new SpiderlyFormControl<PrimengOption[]>(null, {updateOn: 'change'});
    selectedPermissionsForRole = new SpiderlyFormControl<number[]>(null, {updateOn: 'change'});

    constructor(
        private apiService: ApiSecurityService,
        private route: ActivatedRoute,
        private baseFormService: BaseFormService,
        private authService: AuthBaseService
    ) {}

    ngOnInit(){
        this.formGroup.initSaveBody = () => { 
            let saveBody = new RoleSaveBody();
            saveBody.roleDTO = this.roleFormGroup.getRawValue();

            saveBody.selectedUsersIds = this.selectedUsersForRole.getRawValue()?.map(n => n.code);
            saveBody.selectedPermissionsIds = this.selectedPermissionsForRole.getRawValue();

            return saveBody;
        }

        this.formGroup.saveObservableMethod = this.apiService.saveRole;
        this.formGroup.mainDTOName = this.roleSaveBodyName;

        this.route.params.subscribe(async (params) => {
            this.modelId = params['id'];

            getPrimengDropdownNamebookOptions(this.apiService.getPermissionsDropdownListForRole).subscribe(po => {
                this.permissionsForRoleOptions = po;
            });

            if(this.modelId > 0){
                forkJoin({
                    mainUIFormDTO: this.apiService.getRoleMainUIFormDTO(this.modelId)
                })
                .subscribe(({ mainUIFormDTO }) => {
                    this.initRoleFormGroup(new Role(mainUIFormDTO.roleDTO));

                    this.selectedPermissionsForRole.setValue(
                        mainUIFormDTO.permissionsNamebookDTOList.map(n => { return n.id })
                    );
                    this.selectedUsersForRole.setValue(
                        mainUIFormDTO.usersNamebookDTOList.map(n => ({label: n.displayName, code: n.id }))
                    );
                    
                    this.authorizationForSaveSubscription = this.handleAuthorizationForSave().subscribe();
                    this.loading = false;
                });
            }
            else{
                this.initRoleFormGroup(new Role({id: 0}));

                this.authorizationForSaveSubscription = this.handleAuthorizationForSave().subscribe();
                this.loading = false;
            }
        });
    }

    initRoleFormGroup(role: Role) {
        this.baseFormService.addFormGroup<Role>(
            this.roleFormGroup, 
            this.formGroup, 
            role, 
            this.roleSaveBodyName,
            []
        );
        this.roleFormGroup.mainDTOName = this.roleSaveBodyName;
        this.onRoleFormGroupInitFinish.next();
    }
    
    handleAuthorizationForSave = () => {
        return combineLatest([this.authService.currentUserPermissionCodes$, this.authorizedForSaveObservable()]).pipe(
            map(([currentUserPermissionCodes, isAuthorizedForSave]) => {
                if (currentUserPermissionCodes != null && isAuthorizedForSave != null) {
                    this.isAuthorizedForSave =
                        (currentUserPermissionCodes.includes('InsertRole') && this.modelId <= 0) || 
                        (currentUserPermissionCodes.includes('UpdateRole') && this.modelId > 0) ||
                        isAuthorizedForSave;

                    if (this.isAuthorizedForSave) { 
                        this.roleFormGroup.controls.name.enable();
                        this.roleFormGroup.controls.description.enable();
                        this.selectedUsersForRole.enable();
                        this.selectedPermissionsForRole.enable();
                    }
                    else{
                        this.roleFormGroup.controls.name.disable();
                        this.roleFormGroup.controls.description.disable();
                        this.selectedUsersForRole.disable();
                        this.selectedPermissionsForRole.disable();
                    }

                    this.onIsAuthorizedForSaveChange.next(new IsAuthorizedForSaveEvent({
                        isAuthorizedForSave: this.isAuthorizedForSave, 
                        currentUserPermissionCodes: currentUserPermissionCodes
                    })); 
                }
            })
        );
    }

    searchUsersForRole(event: AutoCompleteCompleteEvent) {
        getPrimengAutocompleteNamebookOptions(this.apiService.getUsersAutocompleteListForRole, 50, event?.query ?? '').subscribe(po => {
            this.usersForRoleOptions = po;
        });
    }

    control(formControlName: string, formGroup: SpiderlyFormGroup){
        return getControl(formControlName, formGroup);
    }

    getFormArrayGroups<T>(formArray: SpiderlyFormArray): SpiderlyFormGroup<T>[]{
        return this.baseFormService.getFormArrayGroups<T>(formArray);
    }

    save(){
        this.onSave.next();
    }

    ngOnDestroy(){
        if (this.authorizationForSaveSubscription) {
            this.authorizationForSaveSubscription.unsubscribe();
        }
    }

}
