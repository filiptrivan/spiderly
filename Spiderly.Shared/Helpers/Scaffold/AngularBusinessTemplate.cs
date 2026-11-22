using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>src/app/business/</c> folder: the layout with its menu, and the app's subclasses of Spiderly's
    /// Angular services. The build writes the generated entities, enums and components into its empty folders.
    /// </summary>
    internal static class AngularBusinessTemplate
    {
        internal static SpiderlyFolder Folder() => new()
        {
            Name = "business",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "components",
                },
                new SpiderlyFolder
                {
                    Name = "entities",
                },
                new SpiderlyFolder
                {
                    Name = "enums",
                },
                new SpiderlyFolder
                {
                    Name = "layout",
                    Files =
                    {
                        new SpiderlyFile { Name = "layout.component.html", Data = GetLayoutComponentHtmlCode() },
                        new SpiderlyFile { Name = "layout.component.ts", Data = GetLayoutComponentTsCode() },
                    }
                },
                new SpiderlyFolder
                {
                    Name = "services",
                    ChildFolders =
                    {
                        new SpiderlyFolder
                        {
                            Name = "api",
                            Files =
                            {
                                new SpiderlyFile { Name = "api.service.ts", Data = GetAPIServiceTsCode() },
                            }
                        },
                        new SpiderlyFolder
                        {
                            Name = "auth",
                            Files =
                            {
                                new SpiderlyFile { Name = "auth.service.ts", Data = GetAuthServiceTsCode() },
                            }
                        },
                        new SpiderlyFolder
                        {
                            Name = "layout",
                            Files =
                            {
                                new SpiderlyFile { Name = "layout.service.ts", Data = GetLayoutServiceTsCode() },
                            }
                        },
                        new SpiderlyFolder
                        {
                            Name = "validators",
                            Files =
                            {
                                new SpiderlyFile { Name = "validators.ts", Data = GetValidatorsTsCode() },
                            }
                        },
                    },
                    Files =
                    {
                        new SpiderlyFile { Name = "config.service.ts", Data = GetConfigServiceTsCode() },
                        new SpiderlyFile { Name = "translations.ts", Data = GetTranslationsTsCode() },
                    }
                },
            },
        };

        private static string GetValidatorsTsCode()
        {
            return $$"""
import { Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ValidatorServiceGenerated } from './validators.generated';

@Injectable({
    providedIn: 'root',
})
export class ValidatorService extends ValidatorServiceGenerated {
    constructor(protected override translocoService: TranslocoService) {
        super(translocoService);
    }
}

""";
        }

        private static string GetTranslationsTsCode()
        {
            return $$"""
import { Injectable } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';
import { provideSpiderlyTransloco } from 'spiderly';

// Imported, never fetched: the build emits each language file as a content-hashed chunk shipped
// with the code that reads its keys. A file fetched from an unchanging URL like
// /assets/i18n/en.json can outlive a deploy in the browser cache (a hard reload stops bypassing
// the cache at the load event, before Transloco asks for it), so new keys render raw. angular.json
// leaves assets/i18n/ out of the published assets for the same reason.
@Injectable({ providedIn: 'root' })
export class ImportedTranslocoLoader implements TranslocoLoader {
  getTranslation(lang: string): Promise<Translation> {
    return import(`../../../assets/i18n/${lang}.json`).then((file) => file.default);
  }
}

// Spiderly ships its own strings in English. Another language for them is one more file here,
// assets/i18n/spiderly/<lang>.json, which this loader reads as `spiderly/<lang>`.
export function provideTranslations() {
  return provideSpiderlyTransloco({
    config: {
      availableLangs: ['en'],
      defaultLang: 'en',
      reRenderOnLangChange: true,
    },
    loader: ImportedTranslocoLoader,
  });
}
""";
        }

        private static string GetConfigServiceTsCode()
        {
            return $$"""
import { Injectable } from "@angular/core";
import { environment } from "src/environments/environment";
import { ConfigServiceBase } from 'spiderly';

@Injectable({
  providedIn: 'root',
})
export class ConfigService extends ConfigServiceBase
{
    override production: boolean = environment.production;
    override apiUrl: string = environment.apiUrl;
    override frontendUrl: string = environment.frontendUrl;
    override companyName: string = environment.companyName;

    /* URLs */
    administrationSlug: string = 'administration';

    constructor(
    ) {
        super();
    }
}
""";
        }

        private static string GetAPIServiceTsCode()
        {
            return $$"""
import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ApiGeneratedService } from './api.service.generated';
import { ConfigService } from '../config.service';

@Injectable({
    providedIn: 'root'
})
export class ApiService extends ApiGeneratedService {

    constructor(
        protected override http: HttpClient,
        protected override config: ConfigService,
    ) {
        super(http, config);
    }

}
""";
        }

        private static string GetAuthServiceTsCode()
        {
            return $$"""
import { Inject, Injectable, OnDestroy, PLATFORM_ID } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ApiService } from 'src/app/business/services/api/api.service';
import { ConfigService } from '../config.service';
import { AuthServiceBase } from 'spiderly';

@Injectable({
  providedIn: 'root',
})
export class AuthService extends AuthServiceBase implements OnDestroy {

  constructor(
    protected override router: Router,
    protected override http: HttpClient,
    protected override apiService: ApiService,
    protected override config: ConfigService,
    @Inject(PLATFORM_ID) protected override platformId: Object,
  ) {
    super(router, http, apiService, config, platformId);
  }

}
""";
        }

        private static string GetLayoutServiceTsCode()
        {
            return $$"""
import { Injectable, OnDestroy } from '@angular/core';
import { ApiService } from 'src/app/business/services/api/api.service';
import { ConfigService } from '../config.service';
import { LayoutServiceBase } from 'spiderly';
import { AuthService } from '../auth/auth.service';

@Injectable({
  providedIn: 'root',
})
export class LayoutService extends LayoutServiceBase implements OnDestroy {

    constructor(
        protected override apiService: ApiService,
        protected override config: ConfigService,
        protected override authService: AuthService,
    ) {
        super(apiService, config, authService);
    }

}

""";
        }

        private static string GetLayoutComponentHtmlCode()
        {
            return """
<spiderly-layout [menu]="menu"></spiderly-layout>
""";
        }

        private static string GetLayoutComponentTsCode()
        {
            return $$"""
import { TranslocoService } from '@jsverse/transloco';
import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ConfigService } from 'src/app/business/services/config.service';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { SpiderlyLayoutComponent, SpiderlyMenuItem, SecurityPermissionCodes } from 'spiderly';
import { CommonModule } from '@angular/common';
import { PermissionCodes } from '../enums/enums.generated';

@Component({
    selector: 'layout',
    templateUrl: './layout.component.html',
    imports: [
        CommonModule,
        FormsModule,
        HttpClientModule,
        RouterModule,
        SpiderlyLayoutComponent,
    ]
})
export class LayoutComponent {
    menu: SpiderlyMenuItem[];

    constructor(
        private config: ConfigService,
        private translocoService: TranslocoService
    ) {
    }

    ngOnInit(): void {
        this.menu = [
            {
                items: [
                    { 
                        label: this.translocoService.translate('Home'), 
                        icon: 'pi pi-fw pi-home', 
                        routerLink: [''],
                    },
                    {
                        label: this.translocoService.translate('Administration'),
                        icon: 'pi pi-fw pi-cog',
                        hasPermission: (permissionCodes: string[]): boolean => {
                            return (
                                permissionCodes?.includes(PermissionCodes.ReadUser) ||
                                permissionCodes?.includes(SecurityPermissionCodes.ReadRole)
                            )
                        },
                        items: [
                            {
                                label: this.translocoService.translate('UserList'),
                                icon: 'pi pi-fw pi-user',
                                routerLink: [`/${this.config.administrationSlug}/users`],
                                hasPermission: (permissionCodes: string[]): boolean => {
                                    return (
                                        permissionCodes?.includes(PermissionCodes.ReadUser)
                                    )
                                },
                            },
                            {
                                label: this.translocoService.translate('RoleList'),
                                icon: 'pi pi-fw pi-id-card',
                                routerLink: [`/${this.config.administrationSlug}/roles`],
                                hasPermission: (permissionCodes: string[]): boolean => {
                                    return (
                                        permissionCodes?.includes(SecurityPermissionCodes.ReadRole)
                                    )
                                },
                            },
                        ]
                    },
                ]
            },
        ];
    }

}

""";
        }
    }
}
