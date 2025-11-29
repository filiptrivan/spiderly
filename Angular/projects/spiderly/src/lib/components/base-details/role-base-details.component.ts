import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { combineLatest, forkJoin, map, Observable, of, Subscription } from 'rxjs';
import { SpiderlyMultiAutocompleteComponent } from '../../controls/spiderly-multiautocomplete/spiderly-multiautocomplete.component';
import { SpiderlyMultiSelectComponent } from '../../controls/spiderly-multiselect/spiderly-multiselect.component';
import { SpiderlyTextareaComponent } from '../../controls/spiderly-textarea/spiderly-textarea.component';
import { SpiderlyTextboxComponent } from '../../controls/spiderly-textbox/spiderly-textbox.component';
import { BaseEntity } from '../../entities/base-entity';
import { IsAuthorizedForSaveEvent } from '../../entities/is-authorized-for-save-event';
import { LastMenuIconIndexClicked } from '../../entities/last-menu-icon-index-clicked';
import { Role, RoleMainUIForm, RoleSaveBody } from '../../entities/security-entities';
import { SpiderlyButton } from '../../entities/spiderly-button';
import { ApiSecurityService } from '../../services/api.service.security';
import { AuthBaseService } from '../../services/auth-base.service';
import { BaseFormService } from '../../services/base-form.service';
import { CardSkeletonComponent } from '../card-skeleton/card-skeleton.component';
import { SpiderlyReturnButtonComponent } from '../spiderly-buttons/return-button/return-button.component';
import { SpiderlyButtonComponent } from '../spiderly-buttons/spiderly-button/spiderly-button.component';
import { SpiderlyFormArray, SpiderlyFormGroup } from '../spiderly-form-control/spiderly-form-control';
import { SpiderlyPanelsModule } from '../spiderly-panels/spiderly-panels.module';
import { Namebook } from '../../entities/namebook';

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
    @Output() onAfterFormGroupInit = new EventEmitter<void>();
    @Input() getCrudMenuForOrderedData: (formArray: SpiderlyFormArray, modelConstructor: BaseEntity, lastMenuIconIndexClicked: LastMenuIconIndexClicked, adjustFormArrayManually: boolean) => MenuItem[];
    @Input() parentFormGroup: SpiderlyFormGroup<RoleMainUIForm>;
    @Input() additionalButtons: SpiderlyButton[] = [];
    @Input() panelTitle: string;
    @Input() showBigPanelTitle: boolean = true;
    @Input() panelIcon: string;

    authorizationForSaveSubscription: Subscription;
    @Input() authorizedForSaveObservable: () => Observable<boolean> = () => of(false);
    isAuthorizedForSave: boolean = false;
    @Output() onIsAuthorizedForSaveChange = new EventEmitter<IsAuthorizedForSaveEvent>(); 
    
    modelId: number;
    loading: boolean = true;

    usersForRoleOptions: Namebook[];
    permissionsForRoleOptions: Namebook[];

    constructor(
        private apiService: ApiSecurityService,
        private route: ActivatedRoute,
        private baseFormService: BaseFormService,
        private authService: AuthBaseService
    ) {}

    ngOnInit(){
        this.parentFormGroup.initSaveBody = () => { 
            let saveBody = new RoleSaveBody();
            saveBody = this.baseFormService.mapMainUIFormToSaveBody(RoleMainUIForm, this.parentFormGroup.getRawValue());
            return saveBody;
        }

        this.parentFormGroup.saveObservableMethod = this.apiService.saveRole;

        this.route.params.subscribe(async (params) => {
            this.modelId = params['id'];

            this.apiService.getPermissionsDropdownListForRole().subscribe(po => {
                this.permissionsForRoleOptions = po;
            });

            if(this.modelId > 0){
                forkJoin({
                    mainUIFormDTO: this.apiService.getRoleMainUIFormDTO(this.modelId)
                })
                .subscribe(({ mainUIFormDTO }) => {
                    this.baseFormService.initFormGroup(this.parentFormGroup, RoleMainUIForm, mainUIFormDTO);
                    this.authorizationForSaveSubscription = this.handleAuthorizationForSave().subscribe();
                    this.onAfterFormGroupInit.next();
                    this.loading = false;
                });
            }
            else{
                this.baseFormService.initFormGroup(this.parentFormGroup, RoleMainUIForm, new RoleMainUIForm({roleDTO: new Role({id: 0})}));
                
                this.authorizationForSaveSubscription = this.handleAuthorizationForSave().subscribe();
                this.onAfterFormGroupInit.next();
                this.loading = false;
            }
        });
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
                        this.parentFormGroup.enable();
                    }
                    else{
                        this.parentFormGroup.disable();
                    }

                    this.onIsAuthorizedForSaveChange.next(new IsAuthorizedForSaveEvent({
                        isAuthorizedForSave: this.isAuthorizedForSave, 
                    })); 
                }
            })
        );
    }

    searchUsersForRole(event: AutoCompleteCompleteEvent) {
        this.apiService.getUsersAutocompleteListForRole(50, event?.query ?? '').subscribe(no => {
            this.usersForRoleOptions = no;
        });
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
