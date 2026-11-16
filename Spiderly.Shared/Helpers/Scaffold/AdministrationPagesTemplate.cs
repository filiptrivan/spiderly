using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>pages/administration/</c> folder: the details and list pages of the seeded User and Role entities.
    /// </summary>
    internal static class AdministrationPagesTemplate
    {
        internal static SpiderlyFolder Folder() => new()
        {
            Name = "administration",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "user",
                    Files =
                    {
                        new SpiderlyFile { Name = "user-details.component.html", Data = GetUserDetailsComponentHtmlData() },
                        new SpiderlyFile { Name = "user-details.component.ts", Data = GetUserDetailsComponentTsData() },
                        new SpiderlyFile { Name = "user-list.component.html", Data = GetUserTableComponentHtmlData() },
                        new SpiderlyFile { Name = "user-list.component.ts", Data = GetUserTableComponentTsData() },
                    }
                },
                new SpiderlyFolder
                {
                    Name = "role",
                    Files =
                    {
                        new SpiderlyFile { Name = "role-details.component.html", Data = GetRoleDetailsComponentHtmlData() },
                        new SpiderlyFile { Name = "role-details.component.ts", Data = GetRoleDetailsComponentTsData() },
                        new SpiderlyFile { Name = "role-list.component.html", Data = GetRoleTableComponentHtmlData() },
                        new SpiderlyFile { Name = "role-list.component.ts", Data = GetRoleTableComponentTsData() },
                    }
                },
            },
        };

        private static string GetRoleDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">

    <role-base-details 
    [panelTitle]="t('Role')"
    panelIcon="pi pi-id-card"
    [parentFormGroup]="parentFormGroup" 
    (onSave)="onSave()" 
    ></role-base-details>

</ng-container>
""";
        }

        private static string GetRoleDetailsComponentTsData()
        {
            return $$$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import {
    BaseFormComponent,
    BaseFormService,
    SpiderlyControlsModule,
    SpiderlyMessageService,
    SpiderlyPanelsModule,
} from 'spiderly';
import { RoleBaseDetailsComponent } from 'src/app/business/components/base-details.generated';
import { RoleMainUIForm, RoleSaveBody } from 'src/app/business/entities/entities.generated';

@Component({
    selector: 'role-details',
    templateUrl: './role-details.component.html',
    imports: [TranslocoDirective, SpiderlyPanelsModule, SpiderlyControlsModule, RoleBaseDetailsComponent],
})
export class RoleDetailsComponent extends BaseFormComponent<RoleMainUIForm, RoleSaveBody> implements OnInit {
    override saveBodyClass = RoleSaveBody;
    override mainUIFormClass = RoleMainUIForm;

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService,
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router,
        protected override route: ActivatedRoute,
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }
}

""";
        }

        private static string GetRoleTableComponentHtmlData() =>
            EntityPagesTemplate.TableHtml("Role");

        // The seeded Role/User pages share one datetime filter declaration — hoisted so the
        // narrowing (equals on a timestamp matches one second and answers with an empty grid)
        // cannot drift between the two templates.
        private const string SeededListSpiderlyImports =
            "Column, createFilterStore, dateFilter, MatchModeCodes, SpiderlyDataTableComponent, textFilter";

        private const string SeededCreatedAtFilterEntry = """
        // Equals on a timestamp matches only the row written in that exact second and answers
        // with an empty grid, so a datetime filter offers only after/before.
        createdAt: dateFilter({
            label: t('CreatedAt'),
            operators: [MatchModeCodes.GreaterThan, MatchModeCodes.LessThan],
        }),
""";

        private static string GetRoleTableComponentTsData()
        {
            return EntityPagesTemplate.ListComponentTs(
                "Role",
                spiderlyImports: SeededListSpiderlyImports,
                filterEntries: """
        name: textFilter({ label: t('Name') }),
""" + "\n" + SeededCreatedAtFilterEntry,
                colEntries: """
            {name: this.translocoService.translate('Name'), filterType: 'text', field: 'name'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt'},
""");
        }

        private static string GetUserDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <user-base-details
        [panelTitle]="parentFormGroup.controls.userDTO?.controls?.email?.getRawValue()"
        panelIcon="pi pi-user"
        [parentFormGroup]="parentFormGroup"
        (onSave)="onSave()"
        [showIsDisabledForUser]="showIsDisabledControl"
        [showReturnButton]="false"
        [handleAdditionalSaveAuthorization]="handleAdditionalSaveAuthorization"
        (onAfterFormGroupInit)="handleAfterFormGroupInit()"
    ></user-base-details>
</ng-container>

""";
        }

        private static string GetUserDetailsComponentTsData()
        {
            return $$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import {
    BaseFormComponent,
    BaseFormService,
    SpiderlyControlsModule,
    SpiderlyMessageService,
    SpiderlyPanelsModule,
} from 'spiderly';
import { UserBaseDetailsComponent } from 'src/app/business/components/base-details.generated';
import { UserMainUIForm, UserSaveBody } from 'src/app/business/entities/entities.generated';
import { PermissionCodes } from 'src/app/business/enums/enums.generated';
import { AuthService } from 'src/app/business/services/auth/auth.service';

@Component({
    selector: 'user-details',
    templateUrl: './user-details.component.html',
    imports: [TranslocoDirective, SpiderlyPanelsModule, SpiderlyControlsModule, UserBaseDetailsComponent],
})
export class UserDetailsComponent extends BaseFormComponent<UserMainUIForm, UserSaveBody> implements OnInit {
    override saveBodyClass = UserSaveBody;
    override mainUIFormClass = UserMainUIForm;

    showIsDisabledControl: boolean = false;

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService,
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router,
        protected override route: ActivatedRoute,
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService,
        private authService: AuthService
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }

    handleAdditionalSaveAuthorization = async (): Promise<boolean> => {
        const currentUser = await firstValueFrom(this.authService.user$);
        return this.isCurrentUserPage(currentUser.id);
    };

    isCurrentUserPage = (currentUserId: number) => {
        return currentUserId === this.parentFormGroup.controls.userDTO.getRawValue().id;
    };

    async handleAfterFormGroupInit() {
        const currentUserPermissionCodes = await firstValueFrom(this.authService.currentUserPermissionCodes$);

        const shouldShowIsDisabledAndExternalLoggedIn =
            this.showIsDisabledAndExternalLoggedIn(currentUserPermissionCodes);

        this.showIsDisabledControl = shouldShowIsDisabledAndExternalLoggedIn;
    }

    showIsDisabledAndExternalLoggedIn = (currentUserPermissionCodes: string[]) => {
        return (
            currentUserPermissionCodes.includes(PermissionCodes.ReadUser) ||
            currentUserPermissionCodes.includes(PermissionCodes.UpdateUser) ||
            currentUserPermissionCodes.includes(PermissionCodes.InsertUser)
        );
    };
}

""";
        }

        private static string GetUserTableComponentHtmlData() =>
            EntityPagesTemplate.TableHtml("User");

        private static string GetUserTableComponentTsData()
        {
            return EntityPagesTemplate.ListComponentTs(
                "User",
                spiderlyImports: SeededListSpiderlyImports,
                filterEntries: """
        email: textFilter({ label: t('Email') }),
""" + "\n" + SeededCreatedAtFilterEntry,
                colEntries: """
            {name: this.translocoService.translate('Email'), filterType: 'text', field: 'email'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt'},
""");
        }
    }
}
