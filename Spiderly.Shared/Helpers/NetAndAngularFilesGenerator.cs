using Spiderly.Shared.Classes;
using Spiderly.Shared.Extensions;
using CaseConverter;
using System.Security.Cryptography;
using Spiderly.Shared.Exceptions;
using Microsoft.AspNetCore.Routing;
using System;

namespace Spiderly.Shared.Helpers
{
    public static class NetAndAngularFilesGenerator
    {
        /// <summary>
        /// Generates the starter project template for a Spiderly application, including both backend and frontend components.
        /// </summary>
        public static void Generate(string outputPath, string appName, string spiderlyVersion, bool isRunningFromNuget, string primaryColor, bool hasTopMenu)
        {
            string jwtKey = Helper.GenerateJwtSecretKey();

            string sqlServerConnectionString = Helper.GetAvailableSqlServerConnectionString(appName);

            SpiderlyFolder appStructure = new SpiderlyFolder
            {
                Name = appName.ToKebabCase(),
                ChildFolders =
                {
                    new SpiderlyFolder
                    {
                        Name = "Frontend",
                        ChildFolders =
                        {
                            new SpiderlyFolder
                            {
                                Name = "src",
                                ChildFolders =
                                {
                                    new SpiderlyFolder
                                    {
                                        Name = "app",
                                        ChildFolders =
                                        {
                                            new SpiderlyFolder
                                            {
                                                Name = "business",
                                                ChildFolders =
                                                {
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "components",
                                                        ChildFolders =
                                                        {
                                                            new SpiderlyFolder
                                                            {
                                                                Name = "base-details",
                                                            },
                                                        }
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
                                                            new SpiderlyFile { Name = "layout.component.html", Data = GetLayoutComponentHtmlCode(hasTopMenu) },
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
                                                                Name = "translates",
                                                                Files =
                                                                {
                                                                    new SpiderlyFile { Name = "merge-class-names.ts", Data = GetMergeClassNamesTsCode() },
                                                                    new SpiderlyFile { Name = "merge-labels.ts", Data = GetMergeLabelsCode() },
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
                                                        }
                                                    },
                                                },
                                            },
                                            new SpiderlyFolder
                                            {
                                                Name = "pages",
                                                ChildFolders =
                                                {
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "administration",
                                                        ChildFolders =
                                                        {
                                                            new SpiderlyFolder
                                                            {
                                                                Name = "notification",
                                                                Files =
                                                                {
                                                                    new SpiderlyFile { Name = "notification-details.component.html", Data = GetNotificationDetailsComponentHtmlData() },
                                                                    new SpiderlyFile { Name = "notification-details.component.ts", Data = GetNotificationDetailsComponentTsData() },
                                                                    new SpiderlyFile { Name = "notification-list.component.html", Data = GetNotificationTableComponentHtmlData() },
                                                                    new SpiderlyFile { Name = "notification-list.component.ts", Data = GetNotificationTableComponentTsData() },
                                                                }
                                                            },
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
                                                    },
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "homepage",
                                                        Files =
                                                        {
                                                            new SpiderlyFile { Name = "homepage.component.html", Data = GetHomepageComponentHtmlData(appName) },
                                                            new SpiderlyFile { Name = "homepage.component.ts", Data = GetHomepageComponentTsData() },
                                                        }
                                                    },
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "privacy-policy",
                                                        Files =
                                                        {
                                                            new SpiderlyFile { Name = "privacy-policy.component.html", Data = GetPrivacyPolicyComponentHtmlData() },
                                                            new SpiderlyFile { Name = "privacy-policy.component.ts", Data = GetPrivacyPolicyComponentTsData() },
                                                        },
                                                    },
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "user-agreement",
                                                        Files =
                                                        {
                                                            new SpiderlyFile { Name = "user-agreement.component.html", Data = GetUserAgreementComponentHtmlData() },
                                                            new SpiderlyFile { Name = "user-agreement.component.ts", Data = GetUserAgreementComponentTsData() },
                                                        },
                                                    },
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "notifications-view",
                                                        Files =
                                                        {
                                                            new SpiderlyFile { Name = "notifications-view.component.html", Data = GetNotificationsViewComponentHtmlData() },
                                                            new SpiderlyFile { Name = "notifications-view.component.ts", Data = GetNotificationsViewComponentTsData() },
                                                        }
                                                    },
                                                }
                                            },
                                        },
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "app.routes.ts", Data = GetAppRoutesTsData() },
                                            new SpiderlyFile { Name = "app.component.html", Data = GetAppComponentHtmlData() },
                                            new SpiderlyFile { Name = "app.component.ts", Data = GetAppComponentTsData() },
                                            new SpiderlyFile { Name = "app.config.ts", Data = GetAppConfigTsData() },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "assets",
                                        ChildFolders =
                                        {
                                            new SpiderlyFolder
                                            {
                                                Name = "i18n",
                                                Files =
                                                {
                                                    new SpiderlyFile { Name = "en.generated.json", Data = "" },
                                                    new SpiderlyFile { Name = "en.json", Data = GetTranslocoEnJsonCode() },
                                                    new SpiderlyFile { Name = "sr-Latn-RS.generated.json", Data = "" },
                                                    new SpiderlyFile { Name = "sr-Latn-RS.json", Data = GetTranslocoSrLatnRSJsonCode() },
                                                }
                                            },
                                            new SpiderlyFolder
                                            {
                                                Name = "images",
                                                ChildFolders =
                                                {
                                                    new SpiderlyFolder
                                                    {
                                                        Name = "logo",
                                                        Files =
                                                        {
                                                            new SpiderlyFile { Name = "favicon.ico", Data = GetFaviconIcoData() },
                                                            new SpiderlyFile { Name = "logo.svg", Data = GetLogoSvgData() },
                                                        }
                                                    }
                                                }
                                            },
                                        },
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "primeng-theme.ts", Data = GetPrimeNGThemeTsData() },
                                            new SpiderlyFile { Name = "styles.scss", Data = GetStylesScssData(isRunningFromNuget) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "environments",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "environment.prod.ts", Data = GetEnvironmentProdTsData(appName) },
                                            new SpiderlyFile { Name = "environment.ts", Data = GetEnvironmentTsData(appName) },
                                        }
                                    }
                                },
                                Files =
                                {
                                    new SpiderlyFile { Name = "index.html", Data = GetIndexHtmlData(appName) },
                                    new SpiderlyFile { Name = "main.ts", Data = GetMainTsData() },
                                }
                            }
                        },
                        Files =
                        {
                            new SpiderlyFile { Name = ".editorconfig", Data = GetEditOrConfigData() },
                            new SpiderlyFile { Name = "angular.json", Data = GetAngularJsonData(appName) },
                            new SpiderlyFile { Name = "package.json", Data = GetPackageJsonData(appName, spiderlyVersion, isRunningFromNuget) },
                            new SpiderlyFile { Name = "README.md", Data = GetFrontendREADMEData(appName, spiderlyVersion) },
                            new SpiderlyFile { Name = "tsconfig.app.json", Data = GetTsConfigAppJsonData() },
                            new SpiderlyFile { Name = "tsconfig.json", Data = GetTsConfigJsonData(isRunningFromNuget) },
                            new SpiderlyFile { Name = "tsconfig.spec.json", Data = GetTsConfigSpecJsonData() },
                            new SpiderlyFile { Name = "vercel.json", Data = GetVercelJsonData() },
                        }
                    },
                    new SpiderlyFolder
                    {
                        Name = "Backend",
                        ChildFolders =
                        {
                            new SpiderlyFolder
                            {
                                Name = $"{appName}.Business",
                                ChildFolders =
                                {
                                    new SpiderlyFolder
                                    {
                                        Name = "DataMappers",
                                        Files = new List<SpiderlyFile>
                                        {
                                            new SpiderlyFile { Name = "MapsterMapper.cs", Data = GetMapsterMapperCsData(appName) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "DTO",
                                        Files = new List<SpiderlyFile>
                                        {
                                            new SpiderlyFile { Name = "NotificationDTO.cs", Data = GetNotificationDTOCsData(appName) },
                                            new SpiderlyFile { Name = "NotificationSaveBodyDTO.cs", Data = GetNotificationSaveBodyDTOCsData(appName) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "Entities",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "Notification.cs", Data = GetNotificationCsData(appName) },
                                            new SpiderlyFile { Name = "User.cs", Data = GetUserCsData(appName) },
                                            new SpiderlyFile { Name = "UserNotification.cs", Data = GetUserNotificationCsData(appName) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "Enums",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "BusinessPermissionCodes.cs", Data = GetBusinessPermissionCodesCsData(appName) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "Services",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = $"AuthorizationBusinessService.cs", Data = GetAuthorizationServiceCsData(appName) },
                                            new SpiderlyFile { Name = $"{appName}BusinessService.cs", Data = GetBusinessServiceCsData(appName) },
                                        }
                                    },
                                },
                                Files =
                                {
                                    new SpiderlyFile { Name = $"{appName}.Business.csproj", Data = GetBusinessCsProjData(appName, spiderlyVersion, isRunningFromNuget) },
                                    new SpiderlyFile { Name = $"Settings.cs", Data = GetBusinessSettingsCsData(appName) },
                                }
                            },
                            new SpiderlyFolder
                            {
                                Name = $"{appName}.Infrastructure",
                                Files =
                                {
                                    new SpiderlyFile { Name = $"{appName}ApplicationDbContext.cs", Data = GetInfrastructureApplicationDbContextData(appName) },
                                    new SpiderlyFile { Name = $"{appName}.Infrastructure.csproj", Data = GetInfrastructureCsProjData(appName, spiderlyVersion, isRunningFromNuget) },
                                }
                            },
                            new SpiderlyFolder
                            {
                                Name = $"{appName}.Shared",
                                ChildFolders =
                                {
                                    new SpiderlyFolder
                                    {
                                        Name = "FluentValidation",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "TranslatePropertiesConfiguration.cs", Data = GetTranslatePropertiesConfigurationCsData(appName) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "Resources",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "Terms.Designer.cs", Data = GetTermsDesignerCsData(appName) },
                                            new SpiderlyFile { Name = "Terms.resx", Data = GetTermsResxData() },
                                            new SpiderlyFile { Name = "Terms.sr-Latn-RS.resx", Data = GetTermsSrLatnRSResxData() },
                                            new SpiderlyFile { Name = "TermsGenerated.Designer.cs", Data = GetTermsGeneratedDesignerCsData(appName) },
                                            new SpiderlyFile { Name = "TermsGenerated.resx", Data = GetTermsGeneratedResxData() },
                                            new SpiderlyFile { Name = "TermsGenerated.sr-Latn-RS.resx", Data = GetTermsGeneratedSrLatnRSResxData() },
                                        }
                                    }
                                },
                                Files =
                                {
                                    new SpiderlyFile { Name = $"{appName}.Shared.csproj", Data = GetSharedCsProjData(spiderlyVersion, isRunningFromNuget) },
                                }
                            },
                            new SpiderlyFolder
                            {
                                Name = $"{appName}.WebAPI",
                                ChildFolders =
                                {
                                    new SpiderlyFolder
                                    {
                                        Name = "Properties",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "launchSettings.json", Data = GetLaunchSettingsJsonData() },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "Controllers",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "NotificationController.cs", Data = GetNotificationControllerCsData(appName) },
                                            new SpiderlyFile { Name = "SecurityController.cs", Data = GetSecurityControllerCsData(appName) },
                                            new SpiderlyFile { Name = "UserController.cs", Data = GetUserControllerCsData(appName) },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "DI",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "CompositionRoot.cs", Data = GetCompositionRootCsData(appName) },
                                        }
                                    },
                                },
                                Files =
                                {
                                    new SpiderlyFile { Name = "appsettings.json", Data = GetAppSettingsJsonData(
                                        appName, 
                                        emailSender: null, 
                                        emailSenderPassword: null, 
                                        jwtKey: jwtKey, 
                                        blobStorageConnectionString: null, 
                                        blobStorageUrl: null,
                                        sqlServerConnectionString: sqlServerConnectionString
                                    )},
                                    new SpiderlyFile { Name = $"{appName}.WebAPI.csproj", Data = GetWebAPICsProjData(appName, spiderlyVersion, isRunningFromNuget) },
                                    new SpiderlyFile { Name = $"{appName}.WebAPI.csproj.user", Data = GetWebAPICsProjUserData() },
                                    new SpiderlyFile { Name = "Program.cs", Data = GetProgramCsData(appName) },
                                    new SpiderlyFile { Name = "Settings.cs", Data = GetWebAPISettingsCsData(appName) },
                                    new SpiderlyFile { Name = "Startup.cs", Data = GetStartupCsData(appName) },
                                }
                            },
                        },
                        Files =
                        {
                            new SpiderlyFile { Name = $"{appName}.sln", Data = GetNetSolutionData(appName) }
                        }
                    },
                    new SpiderlyFolder
                    {
                        Name = "Database",
                        Files =
                        {
                            new SpiderlyFile { Name = "initialize-script.sql", Data = GetInitializeScriptSqlData(appName) }
                        }
                    }
                },
                Files =
                {
                    new SpiderlyFile { Name = ".gitignore", Data = GetGitIgnoreData() },
                    new SpiderlyFile { Name = "README.md", Data = GetREADMEData(appName, spiderlyVersion) },
                }
            };

            GenerateProjectStructure(appStructure, outputPath);
        }

        private static void GenerateProjectStructure(SpiderlyFolder appStructure, string path)
        {
            string newPath = GenerateFolder(appStructure, path);

            foreach (SpiderlyFile file in appStructure.Files)
                GenerateFile(appStructure, file, newPath);

            foreach (SpiderlyFolder folder in appStructure.ChildFolders)
                GenerateProjectStructure(folder, newPath);
        }

        private static string GenerateFolder(SpiderlyFolder appStructure, string path)
        {
            Helper.MakeFolder(path, appStructure.Name);

            return Path.Combine(path, appStructure.Name);
        }

        private static void GenerateFile(SpiderlyFolder parentFolder, SpiderlyFile file, string path)
        {
            string filePath = Path.Combine(path, file.Name);

            Helper.FileOverrideCheck(filePath);

            Helper.WriteToFile(file.Data, filePath);
        }

        public static string GetSpiderlyControllerTemplate(string entityName, string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Interfaces;
using Azure.Storage.Blobs;
using Spiderly.Security.Services;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;

namespace {{appName}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class {{entityName}}Controller : {{entityName}}BaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}BusinessService _{{appName.FirstCharToLower()}}BusinessService;
        private readonly AuthenticationService _authenticationService;

        public {{entityName}}Controller(
            IApplicationDbContext context, 
            {{appName}}BusinessService {{appName.FirstCharToLower()}}BusinessService, 
            AuthenticationService authenticationService
        )
            : base(context, {{appName.FirstCharToLower()}}BusinessService)
        {
            _context = context;
            _{{appName.FirstCharToLower()}}BusinessService = {{appName.FirstCharToLower()}}BusinessService;
            _authenticationService = authenticationService;
        }

    }
}
""";
        }

        public static string GetSpiderlyAngularDetailsTsTemplate(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ApiService } from 'src/app/business/services/api/api.service';
import { {{entityName}} } from 'src/app/business/entities/business-entities.generated';
import { {{entityName}}BaseDetailsComponent } from 'src/app/business/components/base-details/business-base-details.generated';
import { BaseFormCopy, SpiderlyFormGroup, SpiderlyMessageService, BaseFormService, SpiderlyPanelsModule, SpiderlyControlsModule } from 'spiderly';

@Component({
    selector: '{{kebabEntityName}}-details',
    templateUrl: './{{kebabEntityName}}-details.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyPanelsModule,
        SpiderlyControlsModule,
        {{entityName}}BaseDetailsComponent
    ]
})
export class {{entityName}}DetailsComponent extends BaseFormCopy implements OnInit {
    {{entityName.FirstCharToLower()}}FormGroup = new SpiderlyFormGroup<{{entityName}}>({});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService, 
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router, 
        protected override route: ActivatedRoute,
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService,
        private apiService: ApiService,
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }

    override ngOnInit() {

    }

    override onBeforeSave = (): void => {

    }
}
""";
        }

        public static string GetSpiderlyAngularDetailsHtmlTemplate(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
<ng-container *transloco="let t">

    <{{kebabEntityName}}-base-details
    [panelTitle]="t('{{entityName}}')"
    [formGroup]="formGroup" 
    [{{entityName.FirstCharToLower()}}FormGroup]="{{entityName.FirstCharToLower()}}FormGroup" 
    (onSave)="onSave()"
    [getCrudMenuForOrderedData]="getCrudMenuForOrderedData"
    />

</ng-container>
""";
        }

        public static string GetSpiderlyAngularTableTsTemplate(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
import { ApiService } from 'src/app/business/services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { {{entityName}} } from 'src/app/business/entities/business-entities.generated';
import { Column, SpiderlyDataTableComponent } from 'spiderly';

@Component({
    selector: '{{kebabEntityName}}-list',
    templateUrl: './{{kebabEntityName}}-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent,
    ]
})
export class {{entityName}}ListComponent implements OnInit {
    cols: Column<{{entityName}}>[];

    getPaginated{{entityName}}ListObservableMethod = this.apiService.getPaginated{{entityName}}List;
    export{{entityName}}ListToExcelObservableMethod = this.apiService.export{{entityName}}ListToExcel;
    delete{{entityName}}ObservableMethod = this.apiService.delete{{entityName}};

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Id'), filterType: 'numeric', field: 'id'},
            {actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name:  this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
        ]
    }
}
""";
        }

        public static string GetSpiderlyAngularTableHtmlTemplate(string entityName)
        {
            return $$"""
<ng-container *transloco="let t">

    <spiderly-data-table [tableTitle]="t('{{entityName}}List')" 
    [cols]="cols" 
    [getPaginatedListObservableMethod]="getPaginated{{entityName}}ListObservableMethod" 
    [exportListToExcelObservableMethod]="export{{entityName}}ListToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="delete{{entityName}}ObservableMethod"
    [showAddButton]="true"
    ></spiderly-data-table>

</ng-container>
""";
        }

        public static string GetSpiderlyAngularDataViewTsTemplate(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
import { ApiService } from 'src/app/business/services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { {{entityName}} } from 'src/app/business/entities/business-entities.generated';
import { DataViewCardBody, SpiderlyControlsModule, SpiderlyDataViewComponent, SpiderlyTemplateTypeDirective, DataViewFilter } from 'spiderly';

@Component({
    selector: '{{kebabEntityName}}-list',
    templateUrl: './{{kebabEntityName}}-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyTemplateTypeDirective,
        SpiderlyDataViewComponent,
        SpiderlyControlsModule,
    ]
})
export class {{entityName}}ListComponent implements OnInit {
    templateType: DataViewCardBody<{{entityName}}>;
    filters: DataViewFilter<{{entityName}}>[];

    getPaginated{{entityName}}ListObservableMethod = this.apiService.getPaginated{{entityName}}List;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.filters = [
            {label: this.translocoService.translate('Id'), type: 'numeric', field: 'id', showMatchModes: true},
        ]
    }
}
""";
        }

        public static string GetSpiderlyAngularDataViewHtmlTemplate(string entityName)
        {
            return $$$"""
<ng-container *transloco="let t">

    <spiderly-data-view 
    [getPaginatedListObservableMethod]="getPaginated{{{entityName}}}ListObservableMethod" 
    [filters]="filters"
    >
        <ng-template #cardBody [templateType]="templateType" let-item let-index="index">
            <div class="card">
                {{item.id}}
            </div>
        </ng-template>
    </spiderly-data-view>

</ng-container>
""";
        }

        private static string GetNotificationDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <spiderly-card [title]="t('Notification')" icon="pi pi-bell">
        <spiderly-panel [isFirstMultiplePanel]="true" [showPanelHeader]="false">
            <panel-body>
                <div class="grid">
                    <div class="col-12">
                        <spiderly-checkbox [control]="isMarkedAsRead" [label]="t('NotifyUsers')"/>
                    </div>
                </div>
            </panel-body>
        </spiderly-panel>

        <notification-base-details
        [showBigPanelTitle]="false"
        [formGroup]="formGroup" 
        [notificationFormGroup]="notificationFormGroup" 
        (onSave)="onSave()"
        [isLastMultiplePanel]="true"
        [additionalButtons]="additionalButtons"
        (onIsAuthorizedForSaveChange)="isAuthorizedForSaveChange($event)"
        (onAfterFormGroupInit)="onAfterFormGroupInit()" 
        />

    </spiderly-card>
</ng-container>
""";
        }

        private static string GetNotificationDetailsComponentTsData()
        {
            return $$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Notification } from 'src/app/business/entities/business-entities.generated';
import { ApiService } from 'src/app/business/services/api/api.service';
import { NotificationBaseDetailsComponent } from 'src/app/business/components/base-details/business-base-details.generated';
import { BaseFormCopy, SpiderlyFormGroup, SpiderlyFormControl, SpiderlyButton, SpiderlyMessageService, BaseFormService, IsAuthorizedForSaveEvent, SpiderlyPanelsModule, SpiderlyControlsModule } from 'spiderly';

@Component({
    selector: 'notification-details',
    templateUrl: './notification-details.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyPanelsModule,
        SpiderlyControlsModule,
        NotificationBaseDetailsComponent
    ]
})
export class NotificationDetailsComponent extends BaseFormCopy implements OnInit {
    notificationFormGroup = new SpiderlyFormGroup<Notification>({});

    isMarkedAsRead = new SpiderlyFormControl<boolean>(true, {updateOn: 'change'});

    additionalButtons: SpiderlyButton[] = [];
    sendEmailNotificationButton = new SpiderlyButton({label: this.translocoService.translate('SendEmailNotification'), icon: 'pi pi-send', disabled: true});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService, 
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router, 
        protected override route: ActivatedRoute,
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService,
        private apiService: ApiService,
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }

    override ngOnInit() {
        this.sendEmailNotificationButton.onClick = this.sendEmailNotification;
    }

    isAuthorizedForSaveChange = (event: IsAuthorizedForSaveEvent) => {
        this.sendEmailNotificationButton.disabled = !event.isAuthorizedForSave;

        if (event.isAuthorizedForSave) {
            this.isMarkedAsRead.enable();
        }
        else{
            this.isMarkedAsRead.disable();
        }
    }

    onAfterFormGroupInit() {
        if (this.notificationFormGroup.controls.id.value > 0) {
            this.additionalButtons.push(this.sendEmailNotificationButton);
        }
    }

    // We must to do it like arrow function
    sendEmailNotification = () => {
        this.apiService.sendNotificationEmail(this.notificationFormGroup.controls.id.value, this.notificationFormGroup.controls.version.value).subscribe(() => {
            this.messageService.successMessage(this.translocoService.translate('SuccessfulAttempt'));
        });
    }

    override onBeforeSave = (): void => {
        this.saveBody.isMarkedAsRead = this.isMarkedAsRead.value;
    }

}

""";
        }

        private static string GetNotificationTableComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <spiderly-data-table 
    [tableTitle]="t('NotificationList')" 
    [cols]="cols" 
    [getPaginatedListObservableMethod]="getPaginatedNotificationListObservableMethod" 
    [exportListToExcelObservableMethod]="exportNotificationListToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="deleteNotificationObservableMethod"
    >
    </spiderly-data-table>
</ng-container>
""";
        }

        private static string GetNotificationTableComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Column, SpiderlyDataTableComponent } from 'spiderly';
import { ApiService } from 'src/app/business/services/api/api.service';
import { Notification } from 'src/app/business/entities/business-entities.generated';

@Component({
    selector: 'notification-list',
    templateUrl: './notification-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent
    ]
})
export class NotificationListComponent implements OnInit {
    cols: Column<Notification>[];

    getPaginatedNotificationListObservableMethod = this.apiService.getPaginatedNotificationList;
    exportNotificationListToExcelObservableMethod = this.apiService.exportNotificationListToExcel;
    deleteNotificationObservableMethod = this.apiService.deleteNotification;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Title'), filterType: 'text', field: 'title'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
            {actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name: this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
        ]
    }

}

""";
        }

        private static string GetRoleDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">

    <role-base-details 
    [panelTitle]="t('Role')"
    panelIcon="pi pi-id-card"
    [formGroup]="formGroup" 
    [roleFormGroup]="roleFormGroup" 
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
import { Role, SpiderlyMessageService, BaseFormCopy, BaseFormService, SpiderlyFormGroup, SpiderlyControlsModule, SpiderlyPanelsModule, RoleBaseDetailsComponent } from 'spiderly';

@Component({
    selector: 'role-details',
    templateUrl: './role-details.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyPanelsModule,
        SpiderlyControlsModule,
        RoleBaseDetailsComponent
    ]
})
export class RoleDetailsComponent extends BaseFormCopy implements OnInit {
    roleFormGroup = new SpiderlyFormGroup<Role>({});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService, 
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router, 
        protected override route: ActivatedRoute, 
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService,
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }
}

""";
        }

        private static string GetRoleTableComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <spiderly-data-table 
    [tableTitle]="t('RoleList')" 
    [cols]="cols" 
    [getPaginatedListObservableMethod]="getPaginatedRoleListObservableMethod" 
    [exportListToExcelObservableMethod]="exportRoleListToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="deleteRoleObservableMethod"
    ></spiderly-data-table>
</ng-container>
""";
        }

        private static string GetRoleTableComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ApiService } from 'src/app/business/services/api/api.service';
import { Column, Role, SpiderlyDataTableComponent } from 'spiderly';

@Component({
    selector: 'role-list',
    templateUrl: './role-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent
    ]
})
export class RoleListComponent implements OnInit {
    cols: Column<Role>[];

    getPaginatedRoleListObservableMethod = this.apiService.getPaginatedRoleList;
    exportRoleListToExcelObservableMethod = this.apiService.exportRoleListToExcel;
    deleteRoleObservableMethod = this.apiService.deleteRole;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Name'), filterType: 'text', field: 'name'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
            {actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name: this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
        ]
    }
}

""";
        }

        private static string GetUserDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <user-base-details
    [panelTitle]="userFormGroup.getRawValue().email ?? null"
    [showBigPanelTitle]="false"
    panelIcon="pi pi-user"
    [formGroup]="formGroup" 
    [userFormGroup]="userFormGroup" 
    (onSave)="onSave()" 
    [showIsDisabledForUser]="showIsDisabledControl"
    [showHasLoggedInWithExternalProviderForUser]="showHasLoggedInWithExternalProvider"
    [showReturnButton]="false"
    [authorizedForSaveObservable]="authorizedForSaveObservable"
    (onIsAuthorizedForSaveChange)="isAuthorizedForSaveChange($event)"
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
import { User } from 'src/app/business/entities/business-entities.generated';
import { BaseFormCopy, SpiderlyFormGroup, SpiderlyMessageService, BaseFormService, IsAuthorizedForSaveEvent, SpiderlyControlsModule, SpiderlyPanelsModule } from 'spiderly';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { combineLatest, delay, map, Observable } from 'rxjs';
import { BusinessPermissionCodes } from 'src/app/business/enums/business-enums.generated';
import { UserBaseDetailsComponent } from 'src/app/business/components/base-details/business-base-details.generated';

@Component({
    selector: 'user-details',
    templateUrl: './user-details.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyPanelsModule,
        SpiderlyControlsModule,
        UserBaseDetailsComponent,
    ]
})
export class UserDetailsComponent extends BaseFormCopy implements OnInit {
    userFormGroup = new SpiderlyFormGroup<User>({});

    showIsDisabledControl: boolean = false;
    showHasLoggedInWithExternalProvider: boolean = false;

    isAuthorizedForSave: boolean = false;

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

    override ngOnInit() {

    }

    authorizedForSaveObservable = (): Observable<boolean> => {
        return combineLatest([this.authService.currentUserPermissionCodes$, this.authService.user$]).pipe(
            delay(0),
            map(([currentUserPermissionCodes, currentUser]) => {
                if (currentUserPermissionCodes != null && currentUser != null) {
                    const IsDisabledAndExternalLoggedInControls = this.showIsDisabledAndExternalLoggedInControlsForPermissions(currentUserPermissionCodes);
                    this.showIsDisabledControl = IsDisabledAndExternalLoggedInControls;
                    this.showHasLoggedInWithExternalProvider = IsDisabledAndExternalLoggedInControls;
                    return this.isCurrentUserPage(currentUser.id);
                }

                return false;
            })
        );
    }

    showIsDisabledAndExternalLoggedInControlsForPermissions = (currentUserPermissionCodes: string[]) => {
        return currentUserPermissionCodes.includes(BusinessPermissionCodes.ReadUser) ||
               currentUserPermissionCodes.includes(BusinessPermissionCodes.UpdateUser);
    }

    isCurrentUserPage = (currentUserId: number) => {
        return currentUserId === this.userFormGroup.getRawValue().id;
    }

    isAuthorizedForSaveChange = (event: IsAuthorizedForSaveEvent) => {
        this.isAuthorizedForSave = event.isAuthorizedForSave;

        this.userFormGroup.controls.hasLoggedInWithExternalProvider.disable();
    }

    override onBeforeSave = (): void => {

    }
}

""";
        }

        private static string GetUserTableComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <spiderly-data-table 
    [tableTitle]="t('UserList')" 
    [cols]="cols" 
    [getPaginatedListObservableMethod]="getPaginatedUserListObservableMethod" 
    [exportListToExcelObservableMethod]="exportUserListToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="deleteUserObservableMethod"
    [showAddButton]="false"
    ></spiderly-data-table>
</ng-container>
""";
        }

        private static string GetUserTableComponentTsData()
        {
            return $$"""
import { ApiService } from '../../../business/services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { User } from 'src/app/business/entities/business-entities.generated';
import { Column, SpiderlyDataTableComponent } from 'spiderly';

@Component({
    selector: 'user-list',
    templateUrl: './user-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent,
    ]
})
export class UserListComponent implements OnInit {
    cols: Column<User>[];

    getPaginatedUserListObservableMethod = this.apiService.getPaginatedUserList;
    exportUserListToExcelObservableMethod = this.apiService.exportUserListToExcel;
    deleteUserObservableMethod = this.apiService.deleteUser;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Email'), filterType: 'text', field: 'email'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
            {actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name:  this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
        ]
    }
}

""";
        }

        private static string GetHomepageComponentHtmlData(string appName)
        {
            return $$$"""
<ng-container *transloco="let t">
    <info-card header="Hello, {{companyName}}">
        🎉 Congratulations! Your app is running. To complete the setup, please follow <a href="https://www.spiderly.dev/docs/getting-started/#connect-to-sql-server" target="_blank" rel="noopener noreferrer">Step 9</a> in the Getting Started guide.
    </info-card>
</ng-container>
""";
        }

        private static string GetHomepageComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { InfoCardComponent } from 'spiderly';
import { ConfigService } from 'src/app/business/services/config.service';

@Component({
    templateUrl: './homepage.component.html',
    imports: [
      InfoCardComponent,
      TranslocoDirective,
    ],
})
export class HomepageComponent implements OnInit {
  companyName = this.config.companyName;

  constructor(
    private config: ConfigService
  ) {}

  ngOnInit() {

  }

  ngOnDestroy(): void {

  }

}
""";
        }

        private static string GetPrivacyPolicyComponentHtmlData()
        {
            return $$$"""
<div style="padding: 30px;">
  <div class="card dashboard-card-wrappe">

    <div class="big-header" style="margin-bottom: 20px;">
      <h1 class="remove-h-css">POLITIKA PRIVATNOSTI</h1>
      <div class="bold-header-separator"></div>
    </div>

    <p style="margin-bottom: 20px;">Vaša privatnost nam je važna. Ova Politika privatnosti objašnjava kako <strong>{{companyName}}</strong> prikuplja, koristi i štiti vaše lične podatke. Korišćenjem naše Platforme, saglasni ste sa obradom podataka u skladu sa ovom politikom.</p>

    <spiderly-panel [isFirstMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="1. PRIKUPLJANJE PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>1.1.</strong> Prikupljamo podatke koje nam dobrovoljno dostavite prilikom registracije i korišćenja Platforme.</p>
        <p><strong>1.2.</strong> Automatski prikupljamo tehničke podatke o vašem uređaju, IP adresi i obrascima korišćenja Platforme.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="2. KORIŠĆENJE PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>2.1.</strong> Vaše podatke koristimo za obezbeđivanje, unapređenje i personalizaciju usluga na Platformi.</p>
        <p><strong>2.2.</strong> Podaci se mogu koristiti u analitičke svrhe kako bismo poboljšali korisničko iskustvo.</p>
        <p><strong>2.3.</strong> Nećemo koristiti vaše podatke u svrhe koje nisu navedene u ovoj Politici bez vaše saglasnosti.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="3. DELJENJE PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>3.1.</strong> Vaši podaci neće biti prodati niti iznajmljeni trećim licima.</p>
        <p><strong>3.2.</strong> Možemo podeliti podatke sa trećim licima isključivo u sledećim slučajevima:</p>
        <ul>
          <li>kada imamo vašu izričitu saglasnost,</li>
          <li>kada je to neophodno za pružanje usluga (npr. hosting partneri),</li>
          <li>kada to zahteva zakon ili nadležni organi.</li>
        </ul>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="4. ZAŠTITA PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>4.1.</strong> Preduzimamo odgovarajuće mere zaštite kako bismo osigurali sigurnost vaših podataka.</p>
        <p><strong>4.2.</strong> Iako činimo sve da zaštitimo vaše podatke, ne možemo garantovati apsolutnu bezbednost.</p>
        <p><strong>4.3.</strong> Vi ste odgovorni za čuvanje svojih pristupnih podataka.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="5. PRAVA KORISNIKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>5.1.</strong> Imate pravo da pristupite, izmenite ili izbrišete svoje podatke.</p>
        <p><strong>5.2.</strong> Možete podneti zahtev za prekid obrade vaših podataka kontaktiranjem naše podrške.</p>
        <p><strong>5.3.</strong> Imate pravo da uložite prigovor na obradu podataka u skladu sa važećim zakonima.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="6. KOLAČIĆI I TEHNOLOGIJE PRAĆENJA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>6.1.</strong> Platforma koristi kolačiće za poboljšanje funkcionalnosti i personalizaciju iskustva.</p>
        <p><strong>6.2.</strong> Možete onemogućiti kolačiće u podešavanjima svog pretraživača.</p>
        <p><strong>6.3.</strong> Korišćenjem Platforme pristajete na upotrebu kolačića.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="7. IZMENI I AŽURIRANJE POLITIKE"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>7.1.</strong> Zadržavamo pravo da izmenimo ovu Politiku privatnosti u bilo kom trenutku.</p>
        <p><strong>7.2.</strong> O svim izmenama korisnici će biti blagovremeno obavešteni putem e-maila ili notifikacija na Platformi.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isLastMultiplePanel]="true" [showPanelHeader]="false">
        <panel-body [normalBottomPadding]="true">
          Za sva pitanja ili zahteve vezane za vašu privatnost, kontaktirajte nas na <strong>filiptrivan5&commat;gmail.com</strong>.
          <p>Hvala što koristite <strong>{{companyName}}</strong>!</p>
        </panel-body>
    </spiderly-panel>

  </div>
</div>


""";
        }

        private static string GetPrivacyPolicyComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { SpiderlyPanelsModule } from 'spiderly';
import { ConfigService } from 'src/app/business/services/config.service';

@Component({
    templateUrl: './privacy-policy.component.html',
    imports: [
        SpiderlyPanelsModule
    ]
})
export class PrivacyPolicyComponent implements OnInit {
  companyName = this.config.companyName;

  constructor(
    private config: ConfigService
  ) {}

  ngOnInit() {

  }


}

""";
        }

        private static string GetUserAgreementComponentHtmlData()
        {
            return $$$"""
<div style="padding: 30px;">
  <div class="card dashboard-card-wrappe">

    <div class="big-header" style="margin-bottom: 20px;">
      <h1 class="remove-h-css">USLOVI KORIŠĆENJA</h1>
      <div class="bold-header-separator"></div>
    </div>

    <p style="margin-bottom: 20px;">Dobrodošli na <strong>{{companyName}}</strong>, platformu za kreiranje i upravljanje lojalti programima. Korišćenjem naše platforme, saglasni ste sa sledećim uslovima korišćenja. Molimo vas da ih pažljivo pročitate.</p>

    <spiderly-panel [isFirstMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="1. OPŠTE ODREDBE"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>1.1.</strong> Ovi uslovi korišćenja regulišu pravila i obaveze korisnika prilikom korišćenja <strong>{{companyName}}</strong> (dalje u tekstu: "Platforma").</p>
        <p><strong>1.2.</strong> Pristupom ili korišćenjem Platforme prihvatate ove uslove u celosti.</p>
        <p><strong>1.3.</strong> Ukoliko se ne slažete sa bilo kojim delom ovih uslova, molimo vas da ne koristite Platformu.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="2. REGISTRACIJA I KORIŠĆENJE PLATFORME"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>2.1.</strong> Da biste koristili Platformu, morate se registrovati i kreirati korisnički nalog.</p>
        <p><strong>2.2.</strong> Odgovorni ste za tačnost podataka koje unosite prilikom registracije.</p>
        <p><strong>2.3.</strong> Vaš nalog je ličan i nije dozvoljeno deljenje pristupnih podataka sa trećim licima.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="3. PRAVA I OBAVEZE KORISNIKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>3.1.</strong> Korisnik se obavezuje da će Platformu koristiti u skladu sa važećim zakonima i propisima.</p>
        <p><strong>3.2.</strong> Zabranjeno je zloupotrebljavati Platformu, uključujući, ali ne ograničavajući se na pokušaje neovlašćenog pristupa, manipulaciju podacima ili korišćenje Platforme u nezakonite svrhe.</p>
      </panel-body>
    </spiderly-panel>


    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="4. OGRANIČENJE ODGOVORNOSTI"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>4.1.</strong> Platforma se pruža "kao takva", bez ikakvih garancija.</p>
        <p><strong>4.2.</strong> Ne garantujemo neprekidan rad ili potpunu bezbednost podataka, ali preduzimamo sve razumne mere za njihovu zaštitu.</p>
        <p><strong>4.3.</strong> Ne snosimo odgovornost za bilo kakve gubitke ili štetu nastalu usled korišćenja Platforme.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="5. ZAŠTITA PRIVATNOSTI I PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>5.1.</strong> Svi podaci korisnika biće obrađeni u skladu sa našom Politikom privatnosti.</p>
        <p><strong>5.2.</strong> Nećemo deliti vaše podatke sa trećim licima bez vašeg pristanka, osim ako je to zakonski obavezno.</p>
      </panel-body>
    </spiderly-panel>


    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="6. IZMENI I DOPUNE USLOVA KORIŠĆENJA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>6.1.</strong> Zadržavamo pravo da u bilo kom trenutku izmenimo ili dopunimo ove uslove korišćenja.</p>
        <p><strong>6.2.</strong> O svim izmenama korisnici će biti blagovremeno obavešteni putem e-maila ili notifikacija na Platformi.</p>
      </panel-body>
    </spiderly-panel>


    <spiderly-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="7. ZAVRŠNE ODREDBE"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>7.1.</strong> Ovi uslovi stupaju na snagu danom objavljivanja na Platformi.</p>
        <p><strong>7.2.</strong> Svi sporovi koji proisteknu iz korišćenja Platforme rešavaće se mirnim putem, a ukoliko to nije moguće, nadležan je sud u <strong>Beogradu</strong>.</p>
      </panel-body>
    </spiderly-panel>


    <spiderly-panel [isLastMultiplePanel]="true" [showPanelHeader]="false">
        <panel-body [normalBottomPadding]="true">
          Za sva pitanja ili nejasnoće, kontaktirajte nas na <strong>filiptrivan5&commat;gmail.com</strong>.
          <p>Hvala što koristite <strong>{{companyName}}</strong>!</p>
        </panel-body>
    </spiderly-panel>

  </div>
</div>
""";
        }

        private static string GetUserAgreementComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { SpiderlyPanelsModule } from 'spiderly';
import { ConfigService } from 'src/app/business/services/config.service';

@Component({
    templateUrl: './user-agreement.component.html',
    imports: [
        SpiderlyPanelsModule
    ],
})
export class UserAgreementComponent implements OnInit {
  companyName = this.config.companyName;

  constructor(
    private config: ConfigService
  ) {}

  ngOnInit() {

  }


}

""";
        }

        private static string GetNotificationsViewComponentHtmlData()
        {
            return $$$"""
<ng-container *transloco="let t">
  <div class="card dashboard-card-wrapper">
    <div class="big-header" style="margin-bottom: 10px;">
      {{t('NotificationList')}}
      <div class="bold-header-separator"></div>
    </div>
    <div style="display: flex; flex-direction: column; position: relative; z-index: 2;">
      <div style="display: flex; justify-content: space-between;">
      </div>
      @for (notification of currentUserNotifications?.data; track $index) {
        <div [class]="(notification.isMarkedAsRead ? 'primary-lighter-color-background opacity-70' : '') + ' transparent-card'" style="margin: 0px;">
          <div class="text-wrapper">
            <div style="margin-bottom: 10px; display: flex; justify-content: space-between; position: relative; font-size: 17.5px;">
              <div>
                <div [class]="notification.isMarkedAsRead ? '' : 'bold'">{{notification.title}}</div>
                <div class="header-separator"></div>
              </div>
              <div>
                <i class="pi pi-ellipsis-h icon-hover" (click)="menuToggle($event, notification)"></i>
                  <p-menu #menu [model]="crudMenu" [popup]="true" appendTo="body"></p-menu>
              </div>
            </div>
            <div>
              {{notification.description}}
            </div>
          </div>
        </div>
      }
      @if (currentUserNotifications?.totalRecords == 0) {
        {{t('YouDoNotHaveAnyNotification')}}
      }
    </div>
    <p-paginator
      (onPageChange)="onLazyLoad($event)"
      [first]="filter.first"
      [rows]="filter.rows" 
      [totalRecords]="currentUserNotifications?.totalRecords">
    </p-paginator>
    <div class="card-overflow-icon">
      <i class="pi pi-bell"></i>
    </div>
  </div>
</ng-container>
""";
        }

        private static string GetNotificationsViewComponentTsData()
        {
            return $$"""
import { LayoutService } from './../../business/services/layout/layout.service';
import { Component, OnInit, ViewChild } from '@angular/core';
import { ApiService } from 'src/app/business/services/api/api.service';
import { MenuItem } from 'primeng/api';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Notification } from 'src/app/business/entities/business-entities.generated';
import { Menu, MenuModule } from 'primeng/menu';
import { PaginatedResult, Filter, SpiderlyMessageService } from 'spiderly';

@Component({
  templateUrl: './notifications-view.component.html',
  imports: [
    TranslocoDirective,
    MenuModule,
    PaginatorModule
  ],
})
export class NotificationsViewComponent implements OnInit {
  currentUserNotifications: PaginatedResult<Notification>;

  crudMenu: MenuItem[] = [];
  @ViewChild('menu') menu: Menu;
  lastMenuToggledNotification: Notification;

  filter = new Filter<Notification>({
    first: 0,
    rows: 10,
  });

  constructor(
    private apiService: ApiService,
    private translocoService: TranslocoService,
    private messageService: SpiderlyMessageService,
    private layoutService: LayoutService,
  ) {}

  ngOnInit() {
    this.crudMenu = [
      {label: this.translocoService.translate('Delete'), command: this.deleteNotificationForCurrentUser, icon: 'pi pi-trash'},
      {label: this.translocoService.translate('MarkAsRead'), command: this.markNotificationAsReadForCurrentUser, icon: 'pi pi-eye'},
      {label: this.translocoService.translate('MarkAsUnread'), command: this.markNotificationAsUnreadForCurrentUser, icon: 'pi pi-eye-slash'},
    ]

    this.getNotificationsForCurrentUser();
  }

  onLazyLoad(event: PaginatorState){
    this.filter.first = event.first;
    this.filter.rows = event.rows;
    this.getNotificationsForCurrentUser();
  }

  getNotificationsForCurrentUser(){
    this.apiService.getNotificationsForCurrentUser(this.filter).subscribe((notifications) => {
      this.currentUserNotifications = notifications;
    });
  }

  menuToggle($event: MouseEvent, notification: Notification) {
    this.menu.toggle($event);
    this.lastMenuToggledNotification = notification;
  }

  deleteNotificationForCurrentUser = () => {
    this.apiService.deleteNotificationForCurrentUser(this.lastMenuToggledNotification.id, this.lastMenuToggledNotification.version).subscribe(() => {
      this.messageService.successMessage(this.translocoService.translate('SuccessfulAction'));
      this.onAfterNotificationCrudOperation();
    });
  }

  markNotificationAsReadForCurrentUser = () => {
    this.apiService.markNotificationAsReadForCurrentUser(this.lastMenuToggledNotification.id, this.lastMenuToggledNotification.version).subscribe(() => {
      this.messageService.successMessage(this.translocoService.translate('SuccessfulAction'));
      this.onAfterNotificationCrudOperation();
    });
  }

  markNotificationAsUnreadForCurrentUser = () => {
    this.apiService.markNotificationAsUnreadForCurrentUser(this.lastMenuToggledNotification.id, this.lastMenuToggledNotification.version).subscribe(() => {
      this.messageService.successMessage(this.translocoService.translate('SuccessfulAction'));
      this.onAfterNotificationCrudOperation();
    });
  }

  onAfterNotificationCrudOperation = () => {
    this.getNotificationsForCurrentUser();
    this.layoutService.setUnreadNotificationsCountForCurrentUser().subscribe(); // Don't need to unsubscribe from the http observable
  }

}

""";
        }

        private static string GetAppRoutesTsData()
        {
            return $$"""
import { InMemoryScrollingOptions, RouterConfigOptions, Routes } from '@angular/router';
import { AuthGuard, NotAuthGuard } from 'spiderly';
import { LayoutComponent } from './business/layout/layout.component';

const layoutRoutes: Routes = [
    {
        path: '',
        loadComponent: () => import('./pages/homepage/homepage.component').then(c => c.HomepageComponent),
        canActivate: [AuthGuard]
    },
    {
        path: 'administration/users',
        loadComponent: () => import('./pages/administration/user/user-list.component').then(c => c.UserListComponent),
        canActivate: [AuthGuard],
    },
    {
        path: 'administration/users/:id',
        loadComponent: () => import('./pages/administration/user/user-details.component').then(c => c.UserDetailsComponent),
        canActivate: [AuthGuard],
    },
    {
        path: 'administration/roles',
        loadComponent: () => import('./pages/administration/role/role-list.component').then(c => c.RoleListComponent),
        canActivate: [AuthGuard],
    },
    {
        path: 'administration/roles/:id',
        loadComponent: () => import('./pages/administration/role/role-details.component').then(c => c.RoleDetailsComponent),
        canActivate: [AuthGuard],
    },
    {
        path: 'administration/notifications',
        loadComponent: () => import('./pages/administration/notification/notification-list.component').then(c => c.NotificationListComponent),
        canActivate: [AuthGuard],
    },
    {
        path: 'administration/notifications/:id',
        loadComponent: () => import('./pages/administration/notification/notification-details.component').then(c => c.NotificationDetailsComponent),
        canActivate: [AuthGuard],
    },
    { 
        path: 'notifications',
        loadComponent: () => import('./pages/notifications-view/notifications-view.component').then(c => c.NotificationsViewComponent),
        canActivate: [AuthGuard]
    },
];

export const routes: Routes = [
    {
        path: '', 
        component: LayoutComponent,
        children: layoutRoutes,
    },
    {
        path: 'login',
        loadComponent: () => import('spiderly').then(c => c.LoginComponent),
        canActivate: [NotAuthGuard],
    },
    {
        path: 'registration', loadComponent: () => import('spiderly').then(c => c.RegistrationComponent),
        canActivate: [NotAuthGuard],
    },
    { path: 'privacy-policy', loadComponent: () => import('./pages/privacy-policy/privacy-policy.component').then(c => c.PrivacyPolicyComponent) },
    { path: 'user-agreement', loadComponent: () => import('./pages/user-agreement/user-agreement.component').then(c => c.UserAgreementComponent) },
    { path: 'not-found', loadComponent: () => import('spiderly').then(c => c.NotFoundComponent) },
    { path: '**', redirectTo: 'not-found' },
];

export const scrollConfig: InMemoryScrollingOptions = {
    scrollPositionRestoration: 'top',
    anchorScrolling: 'enabled',
};

export const routerConfigOptions: RouterConfigOptions = {
    onSameUrlNavigation: 'reload',
};
""";
        }

        private static string GetAppComponentHtmlData()
        {
            return $$"""
<!-- NOTE: Translations on the layout component work only if we wrap everything with transloco -->
<ng-container *transloco="let t">

    <router-outlet></router-outlet>

    <p-confirmDialog 
    [acceptLabel]="t('Confirm')" 
    [rejectLabel]="t('Cancle')" 
    rejectButtonStyleClass="p-button-secondary" 
    [style]="{width: '400px'}" 
    [header]="t('AreYouSure')"
    [message]="t('PleaseConfirmToProceed')"
    icon="pi pi-exclamation-circle"
    ></p-confirmDialog>

</ng-container>

<ngx-spinner bdColor="rgba(0, 0, 0, 0.8)" size="medium" color="#fff" type="ball-clip-rotate-multiple" [fullScreen]="true"></ngx-spinner>
<p-toast [breakpoints]="{ '600px': { width: '100%', right: '0', left: '0' } }"></p-toast>
""";
        }

        private static string GetAppComponentTsData()
        {
            return $$"""
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { PrimeNG } from 'primeng/config';
import { NgxSpinnerModule } from 'ngx-spinner';
import { ToastModule } from 'primeng/toast'
import { ConfirmDialogModule } from 'primeng/confirmdialog'
import { RouterModule } from '@angular/router';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    imports: [
        RouterModule,
        TranslocoDirective,
        NgxSpinnerModule,
        ToastModule,
        ConfirmDialogModule,
    ]
})
export class AppComponent implements OnInit {

    constructor(
        private primengConfig: PrimeNG, 
        private translocoService: TranslocoService
    ) {

    }

    async ngOnInit() {
        this.primengConfig.ripple.set(true);

        this.translocoService.selectTranslateObject('Primeng').subscribe((primengTranslations) => {
            this.primengConfig.setTranslation(primengTranslations);
        });
    }
}
""";
        }

        private static string GetAppConfigTsData()
        {
            return $$"""
import { APP_INITIALIZER, ApplicationConfig, ErrorHandler, PLATFORM_ID, provideZoneChangeDetection } from '@angular/core';
import { PreloadAllModules, provideRouter, withInMemoryScrolling, withPreloading, withRouterConfig } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { routes, scrollConfig, routerConfigOptions } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { providePrimeNG } from 'primeng/config';
import { ThemePreset } from 'src/assets/primeng-theme';
import { AuthBaseService, ConfigBaseService, httpLoadingInterceptor, jsonHttpInterceptor, jwtInterceptor, LayoutBaseService, SpiderlyErrorHandler, SpiderlyTranslocoLoader, TranslateLabelsAbstractService, unauthorizedInterceptor, ValidatorAbstractService } from 'spiderly';
import { SocialAuthServiceConfig, GoogleLoginProvider } from '@abacritt/angularx-social-login';
import { environment } from 'src/environments/environment';
import { TranslateLabelsService } from './business/services/translates/merge-labels';
import { ValidatorService } from './business/services/validators/validators';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { ConfigService } from './business/services/config.service';
import { LayoutService } from './business/services/layout/layout.service';
import { provideTransloco } from '@jsverse/transloco';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideAnimationsAsync(),
    provideHttpClient(withFetch()),
    provideTransloco({
      config: {
        availableLangs: [
          'en', 'en.generated',
        ],
        defaultLang: 'en',
        fallbackLang: 'en.generated',
        failedRetries: 0,
        missingHandler: {
          useFallbackTranslation: true,
          logMissingKey: false,
        },
        reRenderOnLangChange: true,
      },
      loader: SpiderlyTranslocoLoader
    }),
    providePrimeNG({
      theme: {
        preset: ThemePreset,
        options: {
          darkModeSelector: '.dark'
        }
      }
    }),
    provideRouter(
      routes,
      withPreloading(PreloadAllModules),
      withInMemoryScrolling(scrollConfig),
      withRouterConfig(routerConfigOptions)
    ),
    provideClientHydration(withEventReplay()),
    MessageService,
    ConfirmationService,
    DialogService,
    {
      provide: ErrorHandler,
      useClass: SpiderlyErrorHandler,
    },
    {
      provide: APP_INITIALIZER,
      useFactory: authInitializer,
      multi: true,
      deps: [AuthService, PLATFORM_ID],
    },
    {
      provide: 'SocialAuthServiceConfig',
      useValue: {
        autoLogin: false,
        providers: [
          {
            id: GoogleLoginProvider.PROVIDER_ID,
            provider: new GoogleLoginProvider(
              environment.GoogleClientId,
              {
                scopes: 'email',
                oneTapEnabled: false,
                prompt: 'none',
              },
            )
          },
        ],
        onError: (err) => {
          console.error(err);
        }
      } as SocialAuthServiceConfig
    },
    {
      provide: ValidatorAbstractService,
      useClass: ValidatorService,
    },
    {
      provide: TranslateLabelsAbstractService,
      useClass: TranslateLabelsService,
    },
    {
      provide: AuthBaseService,
      useExisting: AuthService
    },
    {
      provide: ConfigBaseService,
      useExisting: ConfigService
    },
    {
      provide: LayoutBaseService,
      useExisting: LayoutService
    },
    provideHttpClient(withInterceptors([
      httpLoadingInterceptor,
      jsonHttpInterceptor,
      jwtInterceptor,
      unauthorizedInterceptor,
    ])),
  ]
};
""";
        }

        #region NET

        private static string GetTranslatePropertiesConfigurationCsData(string appName)
        {
            return $$"""
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using {{appName}}.Shared.Resources;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Resources;

namespace {{appName}}.Shared.FluentValidation
{
    public class TranslatePropertiesConfiguration : IConfigureOptions<MvcOptions>
    {
        public TranslatePropertiesConfiguration()
        {

        }

        public void Configure(MvcOptions options)
        {
            ValidatorOptions.Global.DisplayNameResolver = (type, memberInfo, expression) =>
            {
                string translatedPropertyName =
                    TermsGenerated.ResourceManager.GetTranslation(memberInfo.Name) ??
                    Terms.ResourceManager.GetTranslation(memberInfo.Name) ??
                    SharedTerms.ResourceManager.GetTranslation(memberInfo.Name);

                return translatedPropertyName;
            };
        }
    }
}

""";
        }

        private static string GetTermsDesignerCsData(string appName)
        {
            return $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace {{appName}}.Shared.Resources {
    using System;


    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Terms {

        private static global::System.Resources.ResourceManager resourceMan;

        private static global::System.Globalization.CultureInfo resourceCulture;

        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Terms() {
        }

        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("{{appName}}.Shared.Resources.Terms", typeof(Terms).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }

        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to .
        /// </summary>
        public static string Test {
            get {
                return ResourceManager.GetString("Test", resourceCulture);
            }
        }
    }
}

""";
        }

        private static string GetTermsResxData()
        {
            return $$"""
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- 
    Microsoft ResX Schema 

    Version 2.0

    The primary goals of this format is to allow a simple XML format 
    that is mostly human readable. The generation and parsing of the 
    various data types are done through the TypeConverter classes 
    associated with the data types.

    Example:

    ... ado.net/XML headers & schema ...
    <resheader name="resmimetype">text/microsoft-resx</resheader>
    <resheader name="version">2.0</resheader>
    <resheader name="reader">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
    <resheader name="writer">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
    <data name="Name1"><value>this is my long string</value><comment>this is a comment</comment></data>
    <data name="Color1" type="System.Drawing.Color, System.Drawing">Blue</data>
    <data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64">
        <value>[base64 mime encoded serialized .NET Framework object]</value>
    </data>
    <data name="Icon1" type="System.Drawing.Icon, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64">
        <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>
        <comment>This is a comment</comment>
    </data>

    There are any number of "resheader" rows that contain simple 
    name/value pairs.

    Each data row contains a name, and value. The row also contains a 
    type or mimetype. Type corresponds to a .NET class that support 
    text/value conversion through the TypeConverter architecture. 
    Classes that don't support this are serialized and businessSystemd with the 
    mimetype set.

    The mimetype is used for serialized objects, and tells the 
    ResXResourceReader how to depersist the object. This is currently not 
    extensible. For a given mimetype the value must be set accordingly:

    Note - application/x-microsoft.net.object.binary.base64 is the format 
    that the ResXResourceWriter will generate, however the reader can 
    read any of the formats listed below.

    mimetype: application/x-microsoft.net.object.binary.base64
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
            : and then encoded with base64 encoding.

    mimetype: application/x-microsoft.net.object.soap.base64
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Soap.SoapFormatter
            : and then encoded with base64 encoding.

    mimetype: application/x-microsoft.net.object.bytearray.base64
    value   : The object must be serialized into a byte array 
            : using a System.ComponentModel.TypeConverter
            : and then encoded with base64 encoding.
    -->
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name="Test" xml:space="preserve">
    <value></value>
  </data>
</root>
""";
        }

        private static string GetTermsSrLatnRSResxData()
        {
            return $$"""
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microspider-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microspider-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>

</root>
""";
        }

        private static string GetTermsGeneratedDesignerCsData(string appName)
        {
            return $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace {{appName}}.Shared.Resources {
    using System;


    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class TermsGenerated {

        private static global::System.Resources.ResourceManager resourceMan;

        private static global::System.Globalization.CultureInfo resourceCulture;

        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal TermsGenerated() {
        }

        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("{{appName}}.Shared.Resources.TermsGenerated", typeof(TermsGenerated).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }

        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }

    }
}
""";
        }

        private static string GetTermsGeneratedResxData()
        {
            return $$"""
<?xml version="1.0" encoding="utf-8"?>
<root>
	<!-- 
		Microsoft ResX Schema

		Version 1.3

		The primary goals of this format is to allow a simple XML format 
		that is mostly human readable. The generation and parsing of the 
		various data types are done through the TypeConverter classes 
		associated with the data types.

		Example:

		... ado.net/XML headers & schema ...
		<resheader name="resmimetype">text/microsoft-resx</resheader>
		<resheader name="version">1.3</resheader>
		<resheader name="reader">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
		<resheader name="writer">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
		<data name="Name1">this is my long string</data>
		<data name="Color1" type="System.Drawing.Color, System.Drawing">Blue</data>
		<data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64">
			[base64 mime encoded serialized .NET Framework object]
		</data>
		<data name="Icon1" type="System.Drawing.Icon, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64">
			[base64 mime encoded string representing a byte array form of the .NET Framework object]
		</data>

		There are any number of "resheader" rows that contain simple 
		name/value pairs.

		Each data row contains a name, and value. The row also contains a 
		type or mimetype. Type corresponds to a .NET class that support 
		text/value conversion through the TypeConverter architecture. 
		Classes that don't support this are serialized and stored with the 
		mimetype set.

		The mimetype is used for serialized objects, and tells the 
		ResXResourceReader how to depersist the object. This is currently not 
		extensible. For a given mimetype the value must be set accordingly:

		Note - application/x-microsoft.net.object.binary.base64 is the format 
		that the ResXResourceWriter will generate, however the reader can 
		read any of the formats listed below.

		mimetype: application/x-microsoft.net.object.binary.base64
		value   : The object must be serialized with 
			: System.Serialization.Formatters.Binary.BinaryFormatter
			: and then encoded with base64 encoding.

		mimetype: application/x-microsoft.net.object.soap.base64
		value   : The object must be serialized with 
			: System.Runtime.Serialization.Formatters.Soap.SoapFormatter
			: and then encoded with base64 encoding.

		mimetype: application/x-microsoft.net.object.bytearray.base64
		value   : The object must be serialized into a byte array 
			: using a System.ComponentModel.TypeConverter
			: and then encoded with base64 encoding.
	-->

	<xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
		<xsd:element name="root" msdata:IsDataSet="true">
			<xsd:complexType>
				<xsd:choice maxOccurs="unbounded">
					<xsd:element name="data">
						<xsd:complexType>
							<xsd:sequence>
								<xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
								<xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
							</xsd:sequence>
							<xsd:attribute name="name" type="xsd:string" msdata:Ordinal="1" />
							<xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
							<xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
						</xsd:complexType>
					</xsd:element>
					<xsd:element name="resheader">
						<xsd:complexType>
							<xsd:sequence>
								<xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
							</xsd:sequence>
							<xsd:attribute name="name" type="xsd:string" use="required" />
						</xsd:complexType>
					</xsd:element>
				</xsd:choice>
			</xsd:complexType>
		</xsd:element>
	</xsd:schema>
	<resheader name="resmimetype">
		<value>text/microsoft-resx</value>
	</resheader>
	<resheader name="version">
		<value>1.3</value>
	</resheader>
	<resheader name="reader">
		<value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
	</resheader>
	<resheader name="writer">
		<value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
	</resheader>
</root>
""";
        }

        private static string GetTermsGeneratedSrLatnRSResxData()
        {
            return $$"""
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microspider-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microspider-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>

</root>
""";
        }

        private static string GetUserNotificationCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;

namespace {{appName}}.Business.Entities
{
    [M2M]
    public class UserNotification 
    {
        [M2MWithMany(nameof(Notification.Recipients))]
        public virtual Notification Notification { get; set; }

        [M2MWithMany(nameof(User.Notifications))]
        public virtual User User { get; set; }

        public bool IsMarkedAsRead { get; set; }
    }
}
""";
        }

        private static string GetBusinessPermissionCodesCsData(string appName)
        {
            return $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace {{appName}}.Business.Enums
{
    public static partial class BusinessPermissionCodes
    {

    }
}
""";
        }

        private static string GetUserCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Entities;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.Translation;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [Index(nameof(Email), IsUnique = true)]
    public class User : BusinessObject<long>, IUser
    {
        [UIDoNotGenerate]
        [UIControlWidth("col-12")]
        [DisplayName]
        [CustomValidator("EmailAddress()")]
        [StringLength(70, MinimumLength = 5)]
        [Required]
        public string Email { get; set; }

        public bool? HasLoggedInWithExternalProvider { get; set; }

        public bool? IsDisabled { get; set; }

        [ExcludeServiceMethodsFromGeneration]
        public virtual List<Role> Roles { get; } = new(); // M2M

        public virtual List<Notification> Notifications { get; } = new(); // M2M
    }
}
""";
        }

        private static string GetNotificationControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.DTO;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Services;

namespace {{appName}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class NotificationController : NotificationBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}BusinessService _{{appName.FirstCharToLower()}}BusinessService;

        public NotificationController(
            IApplicationDbContext context, 
            {{appName}}BusinessService {{appName.FirstCharToLower()}}BusinessService
        )
            : base(context, {{appName.FirstCharToLower()}}BusinessService)
        {
            _context = context;
            _{{appName.FirstCharToLower()}}BusinessService = {{appName.FirstCharToLower()}}BusinessService;
        }

        [HttpGet]
        [AuthGuard]
        public async Task SendNotificationEmail(long notificationId, int notificationVersion)
        {
            await _{{appName.FirstCharToLower()}}BusinessService.SendNotificationEmail(notificationId, notificationVersion);
        }

        [HttpDelete]
        [AuthGuard]
        public async Task DeleteNotificationForCurrentUser(long notificationId, int notificationVersion)
        {
            await _{{appName.FirstCharToLower()}}BusinessService.DeleteNotificationForCurrentUser(notificationId, notificationVersion);
        }

        [HttpGet]
        [AuthGuard]
        public async Task MarkNotificationAsReadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _{{appName.FirstCharToLower()}}BusinessService.MarkNotificationAsReadForCurrentUser(notificationId, notificationVersion);
        }

        [HttpGet]
        [AuthGuard]
        public async Task MarkNotificationAsUnreadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _{{appName.FirstCharToLower()}}BusinessService.MarkNotificationAsUnreadForCurrentUser(notificationId, notificationVersion);
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        [UIDoNotGenerate]
        public async Task<int> GetUnreadNotificationsCountForCurrentUser()
        {
            return await _{{appName.FirstCharToLower()}}BusinessService.GetUnreadNotificationsCountForCurrentUser();
        }

        [HttpPost]
        [AuthGuard]
        public async Task<PaginatedResultDTO<NotificationDTO>> GetNotificationsForCurrentUser(FilterDTO filterDTO)
        {
            return await _{{appName.FirstCharToLower()}}BusinessService.GetNotificationsForCurrentUser(filterDTO);
        }

    }
}

""";
        }

        private static string GetSecurityControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Security.SecurityControllers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.DTO;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Resources;
using Spiderly.Security.DTO;
using Spiderly.Shared.Extensions;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;

namespace {{appName}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class SecurityController : SecurityBaseController<User>
    {
        private readonly ILogger<SecurityController> _logger;
        private readonly SecurityBusinessService<User> _securityBusinessService;
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}BusinessService _{{appName.FirstCharToLower()}}BusinessService;


        public SecurityController(
            ILogger<SecurityController> logger, 
            SecurityBusinessService<User> securityBusinessService, 
            IJwtAuthManager jwtAuthManagerService, 
            IApplicationDbContext context, 
            AuthenticationService authenticationService,
            AuthorizationService authorizationService,
            {{appName}}BusinessService {{appName.FirstCharToLower()}}BusinessService
        )
            : base(securityBusinessService, jwtAuthManagerService, context, authenticationService, authorizationService)
        {
            _logger = logger;
            _securityBusinessService = securityBusinessService;
            _context = context;
            _{{appName.FirstCharToLower()}}BusinessService = {{appName.FirstCharToLower()}}BusinessService;
        }

       

    }
}

""";
        }

        private static string GetUserControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Interfaces;
using Azure.Storage.Blobs;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Resources;
using Spiderly.Security.Services;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Entities;

namespace {{appName}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class UserController : UserBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}BusinessService _{{appName.FirstCharToLower()}}BusinessService;
        private readonly AuthenticationService _authenticationService;

        public UserController(
            IApplicationDbContext context, 
            {{appName}}BusinessService {{appName.FirstCharToLower()}}BusinessService, 
            AuthenticationService authenticationService
        )
            : base(context, {{appName.FirstCharToLower()}}BusinessService)
        {
            _context = context;
            _{{appName.FirstCharToLower()}}BusinessService = {{appName.FirstCharToLower()}}BusinessService;
            _authenticationService = authenticationService;
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserDTO> GetCurrentUser()
        {
            long userId = _authenticationService.GetCurrentUserId();
            return await _{{appName.FirstCharToLower()}}BusinessService.GetUserDTO(userId, false); // Don't need to authorize because he is current user
        }

    }
}

""";
        }

        private static string GetNotificationCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using Spiderly.Shared.Interfaces;
using {{appName}}.Business.DTO;

namespace {{appName}}.Business.Entities
{
    public class Notification : BusinessObject<long>, INotification<User>
    {
        [UIControlWidth("col-12")]
        [DisplayName]
        [StringLength(100, MinimumLength = 1)]
        [Required]
        public string Title { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.TextArea))]
        [StringLength(400, MinimumLength = 1)]
        [Required]
        public string Description { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.Editor))]
        [StringLength(1000, MinimumLength = 1)]
        public string EmailBody { get; set; }

        #region UITableColumn
        [UITableColumn(nameof(UserDTO.Email))]
        [UITableColumn(nameof(UserDTO.CreatedAt))]
        #endregion
        [SimpleManyToManyTableLazyLoad]
        public virtual List<User> Recipients { get; } = new(); // M2M
    }
}
""";
        }

        private static string GetNotificationSaveBodyDTOCsData(string appName)
        {
            return $$"""
namespace {{appName}}.Business.DTO
{
    public partial class NotificationSaveBodyDTO
    {
        public bool IsMarkedAsRead { get; set; }
    }
}
""";
        }

        private static string GetNotificationDTOCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity.UI;

namespace {{appName}}.Business.DTO
{
    public partial class NotificationDTO
    {
        /// <summary>
        /// This property is only for currently logged in user
        /// </summary>
        [UIDoNotGenerate]
        public bool? IsMarkedAsRead { get; set; }
    }
}
""";
        }

        private static string GetInfrastructureApplicationDbContextData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using {{appName}}.Business.Entities;
using Spiderly.Infrastructure;

namespace {{appName}}.Infrastructure
{
    public partial class {{appName}}ApplicationDbContext : ApplicationDbContext<User> // https://stackoverflow.com/questions/41829229/how-do-i-implement-dbcontext-inheritance-for-multiple-databases-in-ef7-net-co
    {
        public {{appName}}ApplicationDbContext(DbContextOptions<{{appName}}ApplicationDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

    }
}
""";
        }

        private static string GetNetSolutionData(string appName)
        {
            return $$"""
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.8.34525.116
MinimumVisualStudioVersion = 10.0.40219.1
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.WebAPI", "{{appName}}.WebAPI\{{appName}}.WebAPI.csproj", "{1063DCDA-9291-4FAA-87B2-555E12511EE2}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Infrastructure", "{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj", "{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Business", "{{appName}}.Business\{{appName}}.Business.csproj", "{50AD9ADA-4E90-4E69-97BB-92FA455115DE}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Shared", "{{appName}}.Shared\{{appName}}.Shared.csproj", "{2D65E133-33C4-4169-A175-D744800941D6}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Release|Any CPU.Build.0 = Release|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Release|Any CPU.Build.0 = Release|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Release|Any CPU.Build.0 = Release|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {173A0B43-6F68-4847-ABBF-97106E9B08E6}
	EndGlobalSection
EndGlobal
""";
        }

        private static string GetInitializeScriptSqlData(string appName)
        {
            return $$"""
-- These permissions will be assigned to the first registered user in the application.

begin transaction;

use {{appName}}

insert into Permission(Name, Description, Code) values(N'View users', null, N'ReadUser');
insert into Permission(Name, Description, Code) values(N'Edit existing users', null, N'UpdateUser');
insert into Permission(Name, Description, Code) values(N'Delete users', null, N'DeleteUser');
insert into Permission(Name, Description, Code) values(N'View notifications', null, N'ReadNotification');
insert into Permission(Name, Description, Code) values(N'Edit existing notifications', null, N'UpdateNotification');
insert into Permission(Name, Description, Code) values(N'Add new notifications', null, N'InsertNotification');
insert into Permission(Name, Description, Code) values(N'Delete notifications', null, N'DeleteNotification');
insert into Permission(Name, Description, Code) values(N'View roles', null, N'ReadRole');
insert into Permission(Name, Description, Code) values(N'Edit existing roles', null, N'UpdateRole');
insert into Permission(Name, Description, Code) values(N'Add new roles', null, N'InsertRole');
insert into Permission(Name, Description, Code) values(N'Delete roles', null, N'DeleteRole');

INSERT INTO Role (Version, Name, CreatedAt, ModifiedAt) VALUES (1, N'Admin', getdate(), getdate());

DECLARE @AdminRoleId INT;
DECLARE @AdminUserId INT;
SELECT @AdminRoleId = Id FROM Role WHERE Name = N'Admin';
SELECT TOP 1 @AdminUserId = Id FROM [User] ORDER BY Id;

INSERT INTO UserRole (UserId, RoleId) VALUES (@AdminUserId, @AdminRoleId);

INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 1);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 2);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 3);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 4);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 5);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 6);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 7);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 8);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 9);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 10);
INSERT INTO RolePermission (RoleId, PermissionId) VALUES (@AdminRoleId, 11);

commit;
""";
        }

        private static string GetStartupCsData(string appName)
        {
            return $$"""
using LightInject;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Extensions;
using {{appName}}.WebAPI.DI;
using {{appName}}.Infrastructure;
using Quartz;

public class Startup
{
    public static string _jsonConfigurationFile = "appsettings.json";
    private readonly IHostEnvironment _hostEnvironment;

    public Startup(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        Configuration = configuration;
        _hostEnvironment = hostEnvironment;

        if (_hostEnvironment.IsStaging())
            _jsonConfigurationFile = "appsettings.Staging.json";
        else if (_hostEnvironment.IsProduction())
            _jsonConfigurationFile = "appsettings.Production.json";

        {{appName}}.WebAPI.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<{{appName}}.WebAPI.Settings>(_jsonConfigurationFile);
        {{appName}}.Business.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<{{appName}}.Business.Settings>(_jsonConfigurationFile);
        Spiderly.Infrastructure.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<Spiderly.Infrastructure.Settings>(_jsonConfigurationFile);
        Spiderly.Security.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<Spiderly.Security.Settings>(_jsonConfigurationFile);
        Spiderly.Shared.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<Spiderly.Shared.Settings>(_jsonConfigurationFile);
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.SpiderlyConfigureServices<{{appName}}ApplicationDbContext>();
    }

    public void ConfigureContainer(IServiceContainer container)
    {
        container.RegisterInstance(container);

        container.RegisterFrom<CompositionRoot>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseCors(builder =>
        {
            builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithOrigins(new[] { {{appName}}.WebAPI.SettingsProvider.Current.FrontendUrl })
                .WithExposedHeaders("Content-Disposition"); // Allows frontend to access the 'Content-Disposition' header to retrieve the Excel file name
        });

        app.SpiderlyConfigure(env);

        app.UseEndpoints(endpoints =>
        {
            endpoints
                .MapControllers();
        });
    }
}
""";
        }

        private static string GetWebAPISettingsCsData(string appName)
        {
            return $$"""
namespace {{appName}}.WebAPI
{
    public static class SettingsProvider
    {
        public static Settings Current { internal get; set; } = new Settings();
    }

    public class Settings
    {
        public string FrontendUrl { get; set; }

        public string ExcelContentType { get; set; }
    }
}
""";
        }

        private static string GetProgramCsData(string appName)
        {
            return $$"""
using Serilog;

namespace {{appName}}.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host
                .CreateDefaultBuilder(args)
                .UseSerilog((context, configuration) =>
                {
                    configuration.ReadFrom.Configuration(context.Configuration);
                })
                .UseLightInject()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
""";
        }

        private static string GetWebAPICsProjData(string appName, string spiderlyVersion, bool isRunningFromNuget)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk.Web">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Azure.Storage.Blobs" Version="12.22.2" />
		<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.1" />
		<PackageReference Include="LightInject.Microsoft.Hosting" Version="1.6.1" />
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.2" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.1">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="9.0.1" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.1" />
		<PackageReference Include="Microsoft.Extensions.Azure" Version="1.7.6" />
		<PackageReference Include="Microsoft.IdentityModel.Tokens" Version="7.3.1" />
		<PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.19.5" />
        <PackageReference Include="Serilog.Extensions.Hosting" Version="9.0.0" />
        <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
        <PackageReference Include="Serilog.Sinks.ApplicationInsights" Version="4.0.0" />
        <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
		<PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.3.1" />
	</ItemGroup>

	<ItemGroup>
{{XmlCommented("""
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Infrastructure\Spiderly.Infrastructure.csproj" />
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Security\Spiderly.Security.csproj" />
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Shared\Spiderly.Shared.csproj" />
        <ProjectReference Include="..\..\..\spiderly\Spiderly.SourceGenerators\Spiderly.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
""", isRunningFromNuget)}}
		<ProjectReference Include="..\{{appName}}.Business\{{appName}}.Business.csproj" />
		<ProjectReference Include="..\{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj" />
		<ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="System.IO.FileSystem.Primitives" Version="4.3.0" />
		<PackageReference Include="System.IO.FileSystem" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Handles" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Debug" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Extensions" Version="4.3.0" />
		<PackageReference Include="Microsoft.Win32.Primitives" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Tracing" Version="4.3.0" />
		<PackageReference Include="System.Net.Primitives" Version="4.3.0" />
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Infrastructure" Version="{{spiderlyVersion}}" />
        <PackageReference Include="Spiderly.Security" Version="{{spiderlyVersion}}" />
        <PackageReference Include="Spiderly.Shared" Version="{{spiderlyVersion}}" />
        <PackageReference Include="Spiderly.SourceGenerators" Version="{{spiderlyVersion}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetWebAPICsProjUserData()
        {
            return $$"""
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <ActiveDebugProfile>IIS Express</ActiveDebugProfile>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
    <DebuggerFlavor>ProjectDebugger</DebuggerFlavor>
  </PropertyGroup>
</Project>
""";
        }

        private static string GetAppSettingsJsonData(
            string appName, 
            string emailSender, 
            string emailSenderPassword, 
            string jwtKey, 
            string blobStorageConnectionString, 
            string blobStorageUrl, 
            string sqlServerConnectionString
        )
        {
            return $$"""
{
    "Serilog": {
        "Using": [
            "Serilog.Sinks.ApplicationInsights",
            "Serilog.Sinks.Console"
        ],
        "MinimumLevel": {
        "Default": "Information",
        "Override": {
            "Microsoft": "Warning",
            "System": "Warning"
        }
        },
        "WriteTo": [
        {
            "Name": "Console"
        },
        {
            "Name": "ApplicationInsights",
            "Args": {
            "connectionString": "",
            "telemetryConverter": "Serilog.Sinks.ApplicationInsights.TelemetryConverters.TraceTelemetryConverter, Serilog.Sinks.ApplicationInsights"
            }
        }
        ],
        "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
    },
  "AppSettings": {
    "AllowedHosts": "*",
    "{{appName}}.WebAPI": {
      "FrontendUrl": "http://localhost:4200",
      "ExcelContentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    },
    "{{appName}}.Business": {
    },
    "Spiderly.Infrastructure": {
      "UseGoogleAsExternalProvider": true,
      "AppHasLatinTranslation": false
    },
    "Spiderly.Shared": {
      "ApplicationName": "{{appName}}",
      "EmailSender": "{{emailSender ?? "youremail@gmail.com"}}", // Email address used to send verification emails during login or registration.
      "EmailSenderPassword": "{{emailSenderPassword ?? "xxxx xxxx xxxx xxxx"}}",
      "UnhandledExceptionRecipients": [ // Email addresses that will receive notifications when an unhandled exception occurs in production.
        "{{emailSender ?? "youremail@gmail.com"}}"
      ],
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": 587,
      "JwtKey": "{{jwtKey}}",
      "JwtIssuer": "https://localhost:7260;",
      "JwtAudience": "https://localhost:7260;",
      "ClockSkewMinutes": 1, // Making it to 1 minute because of the frontend sends request exactly when it expires.

      "BlobStorageConnectionString": "{{blobStorageConnectionString}}",
      "BlobStorageUrl": "{{blobStorageUrl}}",
      "BlobStorageContainerName": "files-dev",

      "ConnectionString": "{{sqlServerConnectionString?.Replace(@"\", @"\\")}}",

      "RequestsLimitNumber": 120,
      "RequestsLimitWindow": 60
    },
    "Spiderly.Security": {
      "JwtKey": "{{jwtKey}}",
      "JwtIssuer": "https://localhost:7260;",
      "JwtAudience": "https://localhost:7260;",
      "ClockSkewMinutes": 1, // Making it to 1 minute because of the frontend sends request exactly when it expires. 
      "AccessTokenExpiration": 20,
      "RefreshTokenExpiration": 1440, // 24 hours
      "VerificationTokenExpiration": 5,
      "NumberOfFailedLoginAttemptsInARowToDisableUser": 40,
      "AllowTheUseOfAppWithDifferentIpAddresses": true,
      "AllowedBrowsersForTheSingleUser": 5,
      "GoogleClientId": "xxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com",
      "ExcelContentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    }
  }
}
""";
        }

        private static string GetLaunchSettingsJsonData()
        {
            return $$"""
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:9663",
      "sslPort": 44388
    }
  },
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:5173",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:7068;http://localhost:5173",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": false,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
""";
        }

        private static string GetCompositionRootCsData(string appName)
        {
            return $$"""
using LightInject;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Excel;
using Spiderly.Security.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Emailing;
using {{appName}}.Business.Services;
using {{appName}}.Business.Entities;
using {{appName}}.Shared.FluentValidation;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;

namespace {{appName}}.WebAPI.DI
{
    public class CompositionRoot : ICompositionRoot
    {
        public virtual void Compose(IServiceRegistry registry)
        {
            #region Spiderly

            registry.Register<AuthenticationService>();
            registry.Register<AuthorizationService>();
            registry.Register<SecurityBusinessService<User>>();
            registry.Register<Spiderly.Security.Services.BusinessServiceGenerated<User>>();
            registry.Register<Spiderly.Security.Services.AuthorizationBusinessService<User>>();
            registry.Register<Spiderly.Security.Services.AuthorizationBusinessServiceGenerated<User>>();
            registry.Register<ExcelService>();
            registry.Register<EmailingService>();
            registry.Register<IFileManager, DiskStorageService>();
            registry.RegisterSingleton<IConfigureOptions<MvcOptions>, TranslatePropertiesConfiguration>();
            registry.RegisterSingleton<IJwtAuthManager, JwtAuthManagerService>();

            #endregion

            #region Business

            registry.Register<{{appName}}.Business.Services.{{appName}}BusinessService>();
            registry.Register<{{appName}}.Business.Services.BusinessServiceGenerated>();
            registry.Register<{{appName}}.Business.Services.AuthorizationBusinessService>();
            registry.Register<{{appName}}.Business.Services.AuthorizationBusinessServiceGenerated>();

            #endregion
        }
    }
}
""";
        }

        private static string GetSharedCsProjData(string version, bool isRunningFromNuget)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
{{XmlCommented($$"""
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Shared\Spiderly.Shared.csproj" />
""", isRunningFromNuget)}}
  </ItemGroup>

  <ItemGroup>
  </ItemGroup>

	<ItemGroup>
		<PackageReference Include="System.IO.FileSystem.Primitives" Version="4.3.0" />
		<PackageReference Include="System.IO.FileSystem" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Handles" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Debug" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Extensions" Version="4.3.0" />
		<PackageReference Include="Microsoft.Win32.Primitives" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Tracing" Version="4.3.0" />
		<PackageReference Include="System.Net.Primitives" Version="4.3.0" />
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Shared" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

    <ItemGroup>
	    <EmbeddedResource Update="Resources\Terms.resx">
		    <Generator>PublicResXFileCodeGenerator</Generator>
		    <LastGenOutput>Terms.Designer.cs</LastGenOutput>
	    </EmbeddedResource>
	    <EmbeddedResource Update="Resources\TermsGenerated.resx">
		    <Generator>PublicResXFileCodeGenerator</Generator>
		    <LastGenOutput>TermsGenerated.Designer.cs</LastGenOutput>
	    </EmbeddedResource>
	    <EmbeddedResource Update="Resources\TermsGenerated.sr-Latn-RS.resx">
		    <Generator>PublicResXFileCodeGenerator</Generator>
	    </EmbeddedResource>
    </ItemGroup>
    <ItemGroup>
	    <Compile Update="Resources\Terms.Designer.cs">
		    <DesignTime>True</DesignTime>
		    <AutoGen>True</AutoGen>
		    <DependentUpon>Terms.resx</DependentUpon>
	    </Compile>
	    <Compile Update="Resources\TermsGenerated.Designer.cs">
		    <DesignTime>True</DesignTime>
		    <AutoGen>True</AutoGen>
		    <DependentUpon>TermsGenerated.resx</DependentUpon>
	    </Compile>
    </ItemGroup>

</Project>

""";
        }

        private static string GetInfrastructureCsProjData(string appName, string version, bool isRunningFromNuget)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
	</PropertyGroup>

	<ItemGroup>
	</ItemGroup>

	<ItemGroup>
{{XmlCommented($$"""
		<ProjectReference Include="..\..\..\spiderly\Spiderly.Infrastructure\Spiderly.Infrastructure.csproj" />
""", isRunningFromNuget)}}
		<ProjectReference Include="..\{{appName}}.Business\{{appName}}.Business.csproj" />
		<ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
	</ItemGroup>

	<ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.1">
          <PrivateAssets>all</PrivateAssets>
          <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
		<PackageReference Include="System.IO.FileSystem.Primitives" Version="4.3.0" />
		<PackageReference Include="System.IO.FileSystem" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Handles" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Debug" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Extensions" Version="4.3.0" />
		<PackageReference Include="Microsoft.Win32.Primitives" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Tracing" Version="4.3.0" />
		<PackageReference Include="System.Net.Primitives" Version="4.3.0" />
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Infrastructure" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetBusinessSettingsCsData(string appName)
        {
            return $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace {{appName}}.Business
{
    public static class SettingsProvider
    {
        public static Settings Current { internal get; set; } = new Settings();
    }

    public class Settings
    {

    }
}
""";
        }

        private static string GetBusinessCsProjData(string appName, string version, bool isRunningFromNuget)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
{{XmlCommented($$"""
    <ProjectReference Include="..\..\..\spiderly\Spiderly.Security\Spiderly.Security.csproj" />
    <ProjectReference Include="..\..\..\spiderly\Spiderly.SourceGenerators\Spiderly.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
""", isRunningFromNuget)}}
    <ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="DataMappers\" />
    <Folder Include="Entities\" />
    <Folder Include="Enums\" />
    <Folder Include="Services\" />
  </ItemGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

	<ItemGroup>
		<PackageReference Include="Quartz.Extensions.Hosting" Version="3.13.1" />
		<PackageReference Include="System.IO.FileSystem.Primitives" Version="4.3.0" />
		<PackageReference Include="System.IO.FileSystem" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Handles" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Debug" Version="4.3.0" />
		<PackageReference Include="System.Runtime.Extensions" Version="4.3.0" />
		<PackageReference Include="Microsoft.Win32.Primitives" Version="4.3.0" />
		<PackageReference Include="System.Diagnostics.Tracing" Version="4.3.0" />
		<PackageReference Include="System.Net.Primitives" Version="4.3.0" />
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Security" Version="{{version}}" />
        <PackageReference Include="Spiderly.SourceGenerators" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetAuthorizationServiceCsData(string appName)
        {
            return $$"""
using Azure.Storage.Blobs;
using Spiderly.Security.Services;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Exceptions;
using {{appName}}.Business.Entities;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Enums;

namespace {{appName}}.Business.Services
{
    public class AuthorizationBusinessService : AuthorizationBusinessServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;

        public AuthorizationBusinessService(
            IApplicationDbContext context, 
            AuthenticationService authenticationService
        )
            : base(context, authenticationService)
        {
            _context = context;
            _authenticationService = authenticationService;
        }

        #region User

        public override async Task AuthorizeUserReadAndThrow(long? userId)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminReadPermission = await IsAuthorizedAsync<User>(BusinessPermissionCodes.ReadUser);
                bool isCurrentUser = _authenticationService.GetCurrentUserId() == userId;

                if (isCurrentUser == false && hasAdminReadPermission == false)
                    throw new UnauthorizedException();
            });
        }

        public override async Task AuthorizeUserUpdateAndThrow(UserDTO userDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminUpdatePermission = await IsAuthorizedAsync<User>(BusinessPermissionCodes.UpdateUser);
                if (hasAdminUpdatePermission)
                    return;

                long currentUserId = _authenticationService.GetCurrentUserId();
                if (currentUserId != userDTO.Id)
                    throw new UnauthorizedException();

                User user = await GetInstanceAsync<User, long>(userDTO.Id, null);

                if (
                    userDTO.IsDisabled != user.IsDisabled ||
                    userDTO.HasLoggedInWithExternalProvider != user.HasLoggedInWithExternalProvider
                )
                {
                    throw new UnauthorizedException();
                }
            });
        }

        #endregion

    }
}
""";
        }

        private static string GetBusinessServiceCsData(string appName)
        {
            return $$"""
using {{appName}}.Business.Services;
using {{appName}}.Business.Entities;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Enums;
using {{appName}}.Business.DataMappers;
using {{appName}}.Business.ValidationRules;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using Spiderly.Security.DTO;
using Spiderly.Security.Services;
using Spiderly.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Mapster;
using FluentValidation;
using Spiderly.Shared.Emailing;
using Azure.Storage.Blobs;

namespace {{appName}}.Business.Services
{
    public class {{appName}}BusinessService : {{appName}}.Business.Services.BusinessServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}.Business.Services.AuthorizationBusinessService _authorizationService;
        private readonly AuthenticationService _authenticationService;
        private readonly SecurityBusinessService<User> _securityBusinessService;
        private readonly EmailingService _emailingService;

        public {{appName}}BusinessService(
            IApplicationDbContext context, 
            ExcelService excelService, 
            {{appName}}.Business.Services.AuthorizationBusinessService authorizationService, 
            SecurityBusinessService<User> securityBusinessService, 
            AuthenticationService authenticationService, 
            EmailingService emailingService,
            IFileManager fileManager
        )
            : base(context, excelService, authorizationService, fileManager)
        {
            _context = context;
            _authorizationService = authorizationService;
            _securityBusinessService = securityBusinessService;
            _authenticationService = authenticationService;
            _emailingService = emailingService;
        }

        #region User

        /// <summary>
        /// IsDisabled is handled inside authorization service
        /// </summary>
        protected override async Task OnBeforeSaveUserAndReturnSaveBodyDTO(UserSaveBodyDTO userSaveBodyDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                if (userSaveBodyDTO.UserDTO.Id <= 0)
                    throw new HackerException("You can't add new user.");

                User user = await GetInstanceAsync<User, long>(userSaveBodyDTO.UserDTO.Id, userSaveBodyDTO.UserDTO.Version);

                if (userSaveBodyDTO.UserDTO.Email != user.Email ||
                    userSaveBodyDTO.UserDTO.HasLoggedInWithExternalProvider != user.HasLoggedInWithExternalProvider
                //userSaveBodyDTO.UserDTO.AccessedTheSystem != user.AccessedTheSystem
                )
                {
                    throw new HackerException("You can't change Email, HasLoggedInWithExternalProvider nor AccessedTheSystem from the main UI form.");
                }
            });
        }

        #endregion

        #region Notification

        public async Task SendNotificationEmail(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                await _authorizationService.AuthorizeAndThrowAsync<User>(BusinessPermissionCodes.UpdateNotification);

                // Checking version because if the user didn't save and some other user changed the version, he will send emails to wrong users
                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                List<string> recipients = notification.Recipients.Select(x => x.Email).ToList();

                await _emailingService.SendEmailAsync(recipients, notification.Title, notification.EmailBody);
            });
        }

        /// <summary>
        /// Don't need authorization because user can do whatever he wants with his notifications
        /// </summary>
        public async Task DeleteNotificationForCurrentUser(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                long currentUserId = _authenticationService.GetCurrentUserId();

                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                await _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.Notification.Id == notification.Id)
                    .ExecuteDeleteAsync();
            });
        }

        /// <summary>
        /// Don't need authorization because user can do whatever he wants with his notifications
        /// </summary>
        public async Task MarkNotificationAsReadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                long currentUserId = _authenticationService.GetCurrentUserId();

                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                await _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.Notification.Id == notification.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsMarkedAsRead, true));
            });
        }

        /// <summary>
        /// Don't need authorization because user can do whatever he wants with his notifications
        /// </summary>
        public async Task MarkNotificationAsUnreadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                long currentUserId = _authenticationService.GetCurrentUserId();

                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                await _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.Notification.Id == notification.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsMarkedAsRead, false));
            });
        }

        public async Task<int> GetUnreadNotificationsCountForCurrentUser()
        {
            long currentUserId = _authenticationService.GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
            {
                var notificationUsersQuery = _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.IsMarkedAsRead == false);

                int count = await notificationUsersQuery.CountAsync();

                return count;
            });
        }

        public async Task<PaginatedResultDTO<NotificationDTO>> GetNotificationsForCurrentUser(FilterDTO filterDTO)
        {
            PaginatedResultDTO<NotificationDTO> result = new();
            long currentUserId = _authenticationService.GetCurrentUserId(); // Not doing user.Notifications, because he could have a lot of them.

            await _context.WithTransactionAsync(async () =>
            {
                var notificationUsersQuery = _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId)
                    .Select(x => new
                    {
                        UserId = x.User.Id,
                        NotificationId = x.Notification.Id,
                        IsMarkedAsRead = x.IsMarkedAsRead,
                    });

                int count = await notificationUsersQuery.CountAsync();

                var notificationUsers = await notificationUsersQuery
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ToListAsync();

                List<NotificationDTO> notificationsDTO = new();

                foreach (var item in notificationUsers)
                {
                    NotificationDTO notificationDTO = new();

                    Notification notification = await GetInstanceAsync<Notification, long>(item.NotificationId, null);
                    notificationDTO.Id = notification.Id;
                    notificationDTO.Version = notification.Version;
                    notificationDTO.Title = notification.Title;
                    notificationDTO.Description = notification.Description;
                    notificationDTO.CreatedAt = notification.CreatedAt;

                    notificationDTO.IsMarkedAsRead = item.IsMarkedAsRead;

                    notificationsDTO.Add(notificationDTO);
                }

                notificationsDTO = notificationsDTO.OrderByDescending(x => x.CreatedAt).ToList();

                result.Data = notificationsDTO;
                result.TotalRecords = count;
            });

            return result;
        }

        #endregion

    }
}
""";
        }

        private static string GetMapsterMapperCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes;

namespace {{appName}}.Business.DataMappers
{
    [CustomMapper]
    public static partial class Mapper
    {

    }
}
""";
        }

        #endregion

        #region Angular

        private static string GetVercelJsonData()
        {
            return $$"""
{
    "rewrites": [{ "source": "/(.*)", "destination": "/src/index.html" }]
}
""";
        }

        private static string GetTsConfigSpecJsonData()
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "./out-tsc/spec",
    "types": [
      "jasmine",
    ]
  },
  "include": [
    "src/**/*.spec.ts",
    "src/**/*.d.ts"
  ]
}
""";
        }

        private static string GetTsConfigJsonData(bool isRunningFromNuget)
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "compileOnSave": false,
  "compilerOptions": {
    "baseUrl": "./",
    "paths": {
{{SlashCommented($$"""
        "spiderly": ["../../spiderly/Angular/projects/spiderly/src/public-api"]
""", isRunningFromNuget)}} 
    },
    "outDir": "./dist/out-tsc",
    "esModuleInterop": true,
    "forceConsistentCasingInFileNames": true,
    "strict": false,
    "noImplicitOverride": true,
    "noPropertyAccessFromIndexSignature": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true,
    "sourceMap": true,
    "declaration": false,
    "importHelpers": true,
    "module": "ES2022",
    "moduleResolution": "node",
    "experimentalDecorators": true,
    "target": "ES2022",
    "resolveJsonModule": true,
    "useDefineForClassFields": false,
    "lib": [
      "ES2022",
      "dom"
    ]
  },
  "exclude": ["node_modules", "**/node_modules/*"],
  "angularCompilerOptions": {
    "preserveSymlinks": true,
    "enableI18nLegacyMessageIdFormat": false,
    "fullTemplateTypeCheck": true,
    "strictInjectionParameters": true,
    "strictInputAccessModifiers": true,
    "strictTemplates": true,
    "strictInputTypes": true
  }
}

""";
        }

        private static string GetTsConfigAppJsonData()
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "./out-tsc/app",
    "types": [
    ]
  },
  "files": [
    "src/main.ts"
  ],
  "include": [
    "src/**/*.d.ts"
  ]
}

""";
        }

        private static string GetPackageJsonData(string appName, string version, bool isRunningFromNuget)
        {
            return $$"""
{
    "name": "{{appName}}",
    "version": "0.0.0",
    "scripts": {
        "ng": "ng",
        "start": "ng serve --port=4200 --open --configuration=development",
        "build": "ng build",
        "watch": "ng build --watch --configuration development",
        "test": "ng test",
        "i18n:extract": "transloco-keys-manager extract --langs en sr-Latn-RS",
        "i18n:find": "transloco-keys-manager find"
    },
    "private": true,
    "dependencies": {
{{(isRunningFromNuget /* Note: Can't comment it out because it's json */ ? $$"""
        "spiderly": "{{version}}",
""" : "")
}}
        "@abacritt/angularx-social-login": "2.2.0",
        "@angular/animations": "19.2.13",
        "@angular/common": "19.2.13",
        "@angular/compiler": "19.2.13",
        "@angular/core": "19.2.13",
        "@angular/forms": "19.2.13",
        "@angular/platform-browser": "19.2.13",
        "@angular/platform-browser-dynamic": "19.2.13",
        "@angular/router": "19.2.13",
        "@jsverse/transloco": "7.5.0",
        "file-saver": "2.0.5",
        "json-parser": "3.1.2",
        "ngx-spinner": "19.0.0",
        "primeflex": "3.3.1",
        "primeicons": "7.0.0",
        "primeng": "19.1.3",
        "@primeng/themes": "19.1.3",
        "quill": "2.0.2",
        "rxjs": "7.8.1",
        "tslib": "2.3.0",
        "webpack-dev-server": "4.15.1",
        "zone.js": "0.15.1"
    },
    "devDependencies": {
        "@angular-devkit/build-angular": "19.2.13",
        "@angular/cli": "19.2.13",
        "@angular/compiler-cli": "19.2.13",
        "@jsverse/transloco-keys-manager": "5.1.0",
        "@types/jasmine": "5.1.0",
        "jasmine-core": "5.1.0",
        "karma": "6.4.0",
        "karma-chrome-launcher": "3.2.0",
        "karma-coverage": "2.2.0",
        "karma-jasmine": "5.1.0",
        "karma-jasmine-html-reporter": "2.1.0",
        "typescript": "5.5.4"
    }
}
""";
        }

        private static string GetAngularJsonData(string appName)
        {
            return $$"""
{
  "$schema": "./node_modules/@angular/cli/lib/config/schema.json",
  "version": 1,
  "newProjectRoot": "projects",
  "projects": {
    "{{appName}}": {
      "projectType": "application",
      "schematics": {
        "@schematics/angular:component": {
          "style": "scss",
          "standalone": false
        },
        "@schematics/angular:directive": {
          "standalone": false
        },
        "@schematics/angular:pipe": {
          "standalone": false
        }
      },
      "root": "",
      "sourceRoot": "src",
      "prefix": "app",
      "architect": {
        "build": {
          "builder": "@angular-devkit/build-angular:application",
          "options": {
            "preserveSymlinks": true,
            "outputPath": "dist/{{appName}}",
            "index": "src/index.html",
            "browser": "src/main.ts",
            "polyfills": [
              "zone.js"
            ],
            "tsConfig": "tsconfig.app.json",
            "inlineStyleLanguage": "scss",
            "assets": [
              "src/favicon.ico",
              "src/assets",
              "src/robots.txt"
            ],
            "styles": [
              "src/assets/styles.scss"
            ],
            "scripts": []
          },
          "configurations": {
            "production": {
              "budgets": [
                {
                  "type": "initial",
                  "maximumWarning": "1mb",
                  "maximumError": "3mb"
                },
                {
                  "type": "anyComponentStyle",
                  "maximumWarning": "2kb",
                  "maximumError": "4kb"
                }
              ],
              "outputHashing": "all",
              "fileReplacements": [
                {
                  "replace": "src/environments/environment.ts",
                  "with": "src/environments/environment.prod.ts"
                }
              ]
            },
            "development": {
              "optimization": false,
			  "extractLicenses": false,
              "sourceMap": true,
			  "outputHashing": "all",
			  "namedChunks": true,
              "aot": false
            }
          },
          "defaultConfiguration": "production"
        },
        "serve": {
          "builder": "@angular-devkit/build-angular:dev-server",
          "configurations": {
            "production": {
              "buildTarget": "{{appName}}:build:production"
            },
            "development": {
              "buildTarget": "{{appName}}:build:development"
            }
          },
          "defaultConfiguration": "development"
        },
        "extract-i18n": {
          "builder": "@angular-devkit/build-angular:extract-i18n",
          "options": {
            "buildTarget": "{{appName}}:build"
          }
        },
        "test": {
          "builder": "@angular-devkit/build-angular:karma",
          "options": {
            "polyfills": [
              "zone.js",
              "zone.js/testing"
            ],
            "tsConfig": "tsconfig.spec.json",
            "inlineStyleLanguage": "scss",
            "assets": [
              "src/assets"
            ],
            "styles": [
              "src/assets/styles.scss"
            ],
            "scripts": []
          }
        }
      }
    }
  },
  "cli": {
    "analytics": false
  }
}
""";
        }

        private static string GetEditOrConfigData()
        {
            return $$"""
# Editor configuration, see https://editorconfig.org
root = true

[*]
charset = utf-8
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[*.ts]
quote_type = single

[*.md]
max_line_length = off
trim_trailing_whitespace = false
""";
        }

        private static string GetMainTsData()
        {
            return $$"""
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
""";
        }

        private static string GetIndexHtmlData(string appName)
        {
            return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>{{appName}}</title>
  <meta name="description" content="{{appName}}">
  <meta name="author" content="{{appName}}">
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <!-- When you add your favicon the href should be: ./assets/images/logo/favicon.ico -->
  <link rel="icon" type="image/x-icon" href="data:image/x-icon;base64,AAABAAEAICAAAAEAIACoEAAAFgAAACgAAAAgAAAAQAAAAAEAIAAAAAAAABAAACMuAAAjLgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB3J9sAdyfbAHcn2yh3J9ssdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbKncn25V3J9sLdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgkwAHcn2wB3J9sMdyfbr3cn2zp3J9sAdyfbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdyfbAHcn2wB3J9uNdyfbgHcn2wB3J9sABAEHAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB3J9sAdyfbAHcn21l3J9vDdyfbEXcn2wB3J9sAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbJXcn29l3J9tRdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAWx6nAHcn2wB3J9sEdyfbrHcn26p3J9sEdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdyfbAHcn2wB3J9tZdyfb43cn2y13J9sAdyfbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdyfbAncn2wB3J9sAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB3J9sAdyfbAHcn2xh3J9vTdyfbfHcn2wB3J9sAdyfbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB3J9s9dyfbBHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbAHcn24V3J9vQdyfbFncn2wB3J9sAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdyfbAHcn2wB3J9sAdyfbAHcn2wAAAAAAAAAAAHcn25B3J9sOdyfbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbMHcn2+N3J9thdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbAHcn2wB3J9sAdyfbAAAAAAAAAAAAdyfbrXcn2yF3J9sAdyfbAAAAAAAAAAAAAAAAAAAAAAAAAAAAdyfJAHcnyQB3J8kAdyfKAHcn2wB3J9sJdyfbwncn2593J9sAdyfbAAAAAAAAAAAAAAAAAAYCCwB3J9sAeCfbAHcn2wB3J9sadyfbV3cn2wx3J9sAAAAAAAAAAAB3J9urdyfbQHcn2wB3J9sAAAAAAAAAAAAAAAAAdyfLAHcnyQB3J8kAdyfLAXcntQF3J94AdyfbAHcn2wx3J9vIdyfbjncn2wB3J9sAdyfbAHcn2wB3J9sAdyfbAHcn2wB3J9sAdyfbKXcn27V3J9uUdyfbCncn2wAAAAAAAAAAAHcn2553J9tldyfbAHcn2wAAAAAAAAAAAAAAAAB3J9UAdyfPAHcnzgB3J8YLdyfPY3cn2nF3J94XdyfbGHcn29t3J9ttdyfbAHcn2wB3J9sCdyfbB3cn2wV3J9sCdyfbAXcn20d3J9vOdyfbkncn2w13J9sAdyfbAHcn2wB3J9sAdyfbhXcn24p3J9sAdyfbAAAAAAAAAAAAAAAAAHcn0wB3J6oAdyfLNHgnyQ93J9codyfX0Xcn1q53J9k6dyfb5Hcn2093J9sAdyfbKXcn25V3J9u/dyfbtncn26p3J9undyfb4Hcn24Z3J9sJdyfbAHcn2wB3J9sAdyfbEHcn2zB3J9todyfbqncn2wR3J9sAAAAAAAAAAAAAAAAAdyfbAHcn2wh3J9iidSjNT3ArtX1xKrngciq8+XQpwqZ3J9TjdyfaRHcn2293J9vgdyfb5ncn27J3J9uwdyfbs3cn27V3J9tudyfbBncn2wB3J9sAdyfbAHcn2w53J9uTdyfbfXcn20h3J9vAdyfbEXcn2wB3J9sAAAAAAAAAAAB3J9sAdyfdCHcn2rd1KM3mdCnL/XQpy/9yKsH/cSu4/nMpv+93J9LGdyfb93cn27Z3J9sydyfbAHcn2wJ3J9sEdyfbBHcn2wB3J9sAdyfbAHcn2wB3J9sMdyfbl3cn26h3J9sRdyfbLHcn2853J9svdyfbAHcn2wB3J9sAdyfbAHcn2wB3J8QAdyfQXXcn1vR3J9z/dyfc/3cn2v90Kcr/cSu4/3Uoxf53J9e8dyfbfXcn2113J9svdyfbDXcn2wB3J9sAdyfbAHcn2wB3J9sAdyfbB3cn2493J9vJdyfbJHcn2wB3J9sSdyfbrncn28F3J9tFdyfbA3cn2wB3J9sAdyfbAHcn1AB3J9EedyfWvncn2/93J9v/dyfb/3cn2v9yKsP/ciq7/Hcn0uV3J9zkdyfc9Xcn2+13J9vHdyfbh3cn20R3J9sXdyfbE3cn2y53J9uNdyfb4ncn20J3J9sAdyfbAHcn2y93J9sodyfbi3cn2+F3J9uXdyfbIHcn2wR3J9stdyfbgncn2tV3J9D2dyfY+Hcn2/93J9v/dyfc/3Uoz/9xK7j7ciq81nIqvrhzKcevdijVqXcn28J3J9vsdyfb9Xcn29h3J9vTdyfb6Xcn2+R3J9tkdyfbAHcn2wB3J9sAdyfbQncn2193J9sBdyfbUncn29p3J9vUdyfbsHcn2+p3J9v4dyfbvXcn0393J9DadyfX/3cn2/x3J9z/dSjQ/3AruP9wK7X/cCu1/3ArtflwK7bScSu6cHYo1jV3J9tmdyfboHcn2413J9tZdyfbKHcn2wJ3J9sAdyfbAAAAAAB3J9sYdyfbqXcn2yl3J9sAdyfbLHcn27B3J9vqdyfbqHcn20l3J9sgdyfbp3cn1vl3J86PdyfSy3cn1OZ2J9jzdSjR/3MpyP9xKr3/cCu2/3Artv9wK7b4cCu1gWUxfgJoL40AdyfbAHcn2wB3J9sAdyfbAHcn2wB3J9sAAAAAAHcn2wB3J9uHdyfbvXcn2093J9sKdyfbCXcn2yV3J9sEdyfbCXcn2513J9v8dyfbh3cn2h53J9jTdyfXx3cn2/J3J9z/dyfb/3Yn1/90Kcn/cSu5/3Artv9wK7b4cCu2XnArtwB2KNQAdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAdyfbAHcn2xZ3J9uAdyfb2Xcn27x3J9tadyfbD3cn2wd3J9uMdyfb/Hcn25F3J9sHdyfbMXcn2+h3J9updyfb9ncn2/93J9v/dyfb/3cn2/91KM7/cCu5/3Artv9wK7bJcCu2E3ArtgBxK7kAAAAAAAAAAAAAAAAAAAAAAAAAAAB3J9sAdyfbAHcn2wB3J9stdyfbl3cn2+N3J9vHdyfbqHcn2/h3J9uddyfbDXcn2wB3J9tXdyfb63cn23V3J9vwdyfb/3cn2/93J9v/dyfb/3cn2/9zKcj/cCu2/3ArtvVwK7ZAcCu2AGktnwAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbAHcn2wB3J9sCdyfbMXcn25J3J9vqdyfbqncn2xN3J9sAdyfbAHcn24F3J9vgdyfbNncn29B3J9v/dyfb/3cn2/93J9v/dyfb/3Yo1v9xKrv/cCu2/3ArtmBwK7YAcCu4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB3J9sAdyfbAHcn2wB3J9sAdyfbAXcn2yx3J9sXdyfbAHcn2wB3J9sDdyfbrHcn28x3J9sLdyfbeHcn2/93J9v/dyfb/3cn2/93J9v/dyfb/3MpxP9wK7X+cCu2XHArtgBwK7gAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdyjbAHcn2wB3J9sAdyfbAHcn2wB3J9sAdyfbAHcn2wF3J9uPdyfb5Xcn2y13J9sRdyfbtHcn2/93J9v/dyfb/3cn2/93J9z/dCnM/3ArtutwK7YwcCu2AHArtgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAkFDgB+P7wAfD65AAgEDAB3J9sAdyfbAHcn2x53J9vQdyfbo3cn2wR3J9sfdyfbq3cn2/p3J9v/dyfb/3cn3P91KNH/cCu4kXArswRwK7cAcSq+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbAHcn21F3J9vpdyfbVXcn2wB3J9sLdyfbWXcn26h3J9vHdyfbwHYn13pxKr4RcSq9AHArtgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHcn2wB3J9sAdyfbBHcn25Z3J9vRdyfbTXcn2yZ3J9sNdyfbCHcn2w13J9sKcyrEAHIqwABxKr4AcSu6AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALw9XAHcn2wB3J9sAdyfbJHcn28J3J9vGdyfbnncn22p3J9srdyfbAXcn2wB3J9sAdyfbAHcn2wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/4H///+A////gP///8B////Af///wH///8A//x/gP/8f4B//H+Afgx/wHwMPgBwDDgAAAw4AAAAOAAAADgAAAAYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAcAAAAfAAAAHwAAAB/AAAAf8AAAH/wAAB//wAA//+AAP//gAH8=">
</head>
<body>
  <app-root></app-root>
</body>
</html>
""";
        }

        private static string GetEnvironmentProdTsData(string appName)
        {
            return $$"""
export const environment = {
  production: true,
  apiUrl: 'https://your-prod-api-url/api',
  frontendUrl: 'http://your-prod-frontend-url',
  GoogleClientId: 'xxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com',
  companyName: '{{appName}}',
};
""";
        }

        private static string GetEnvironmentTsData(string appName)
        {
            return $$"""
export const environment = {
  production: false,
  apiUrl: 'https://localhost:44388/api',
  frontendUrl: 'http://localhost:4200',
  GoogleClientId: 'xxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com',
  companyName: '{{appName}}',
};
""";
        }

        private static string GetPrimeNGThemeTsData()
        {
            return $$"""
import Aura from '@primeng/themes/aura';
import { definePreset } from '@primeng/themes';

export const ThemePreset = definePreset(Aura, {
  semantic: {
    surface: {
      0: '#e5e7eb',
    },
    primary: {
      50: '{pink.50}',
      100: '{pink.100}',
      200: '{pink.200}',
      300: '{pink.300}',
      400: '{pink.400}',
      500: '{pink.500}',
      600: '{pink.600}',
      700: '{pink.700}',
      800: '{pink.800}',
      900: '{pink.900}',
      950: '{pink.950}',
      color: '{pink.600}',
      contrastColor: '{surface.0}',
      hoverColor: '{pink.500}',
      activeColor: '{pink.400}',
    },
  },
  components: {
    panel: {
      colorScheme: {
        dark: {
          root: {
            header: {
              background: '{surface.900}',
            },
            background: '{surface.800}',
          },
        },
      },
    },
  },
});
""";
        }

        private static string GetStylesScssData(bool isRunningFromNuget)
        {
            return $$"""
//#region PrimeNG

@use "../../node_modules/primeflex/primeflex.scss";
@use "../../node_modules/primeicons/primeicons.css";
@use "../../node_modules/ngx-spinner/animations/ball-clip-rotate-multiple.css";

//#endregion

//#region Spiderly

{{SlashCommented("""
@use "../../../../spiderly/Angular/projects/spiderly/src/lib/styles/styles.scss";
""", isRunningFromNuget)}}

{{SlashCommented("""
@use "../../node_modules/spiderly/styles/styles/styles.scss";
""", !isRunningFromNuget)}}

//#endregion
""";
        }

        private static string GetTranslocoSrLatnRSJsonCode()
        {
            return $$$"""
{
    "SelectFromTheList": "Odaberite...",
    "OnDate": "Na datum",
    "Submit": "Potvrdite",
    "UserList": "Korisnici",
    "PartnerList": "Partneri",
    "HasLoggedInWithExternalProvider": "Prijavljen sa eksternim provajderom",
    "IsDisabled": "Blokiran",
    "SuperRoles": "Super uloge",
    "Save": "Sačuvajte",
    "RoleList": "Uloge",
    "Permissions": "Dozvole",
    "Settings": "Podešavanja",
    "RecipientsForNotification": "Primaoci",
    "PermissionList": "Dozvole",
    "NotificationList": "Notifikacije",
    "NotifyUsers": "Obavestite korisnike",
    "Recipients": "Primaoci",
    "SendEmailNotification": "Pošaljite email notifikaciju",
    "AgreementsOnRegister": "Klikom na Slažem se i pridružujem ili Nastavite, prihvatate",
    "UserAgreement": "uslove korišćenja",
    "PrivacyPolicy": "politiku privatnosti",
    "and": "i",
    "CookiePolicy": "politiku upotrebe kolčića",
    "AgreeAndJoin": "Slažem se i pridružujem",
    "AlreadyHasAccount": "Već imate profil? Prijavite se",
    "NewJoinNow": "Novi ste? Napravite profil",
    "ContinueWithGoogle": "Nastavite sa Google nalogom",
    "or": "ili",
    "All": "Sve",
    "AccountVerificationHeader": "Verifikacija profila",
    "AccountVerificationTitle": "Verifikujte svoju email adresu",
    "AccountVerificationDescription": "Poslali smo vam verifikacioni kod na {{ email }}. Molimo vas da proverite inbox ili spam folder i unesete kod koji smo vam poslali kako biste završili proces. Hvala!",
    "GoToGmail": "Idi na Gmail",
    "GoToYahoo": "Idi na Yahoo",
    "ResendVerificationCodeFirstPart": "Ako niste pronašli, možete",
    "ResendVerificationCodeLinkSecondPart": "ponovo poslati verifikacioni kod.",
    "ForgotPassword": "Zaboravili ste lozinku?",
    "Login": "Prijavite se",
    "RememberYourPassword": "Setili ste se lozinke?",
    "ResetPassword": "Promenite lozinku",
    "DragAndDropFilesHereToUpload": "Ovde prevucite i otpustite datoteke da biste ih otpremili.",
    "PleaseConfirmToProceed": "Molimo Vas potvrdite da biste nastavili.",
    "Cancle": "Odustanite",
    "Confirm": "Potvrdite",
    "Clear": "Uklonite",
    "ExportToExcel": "Izvezite u Excel",
    "Select": "Odaberite",
    "NoRecordsFound": "Ne postoji nijedan zapis.",
    "Loading": "Učitavanje",
    "TotalRecords": "Ukupno zapisa",
    "AddNew": "Dodajte",
    "Return": "Nazad",
    "Currency": "RSD",
    "Actions": "Akcije",
    "Details": "Detalji",
    "User": "Korisnik",
    "YouDoNotHaveAnyNotification": "Nemate nijednu notifikaciju.",
    "CreatedAt": "Kreirano",
    "Delete": "Obrišite",
    "Name": "Naziv",
    "Title": "Naslov",
    "SuccessfulAttempt": "Vaš pokušaj je obrađen.",
    "MarkAsRead": "Označite kao pročitano",
    "MarkAsUnread": "Označite kao nepročitano",
    "Email": "Email",
    "Slug": "Url putanja",
    "YourProfile": "Vaš profil",
    "Logout": "Odjavite se",
    "Home": "Početna",
    "SuperAdministration": "Super administracija",
    "Administration": "Administracija",
    "SuccessfullySentVerificationCode": "Verifikacioni kod je uspešno poslat.",
    "YouHaveSuccessfullyVerifiedYourAccount": "Uspešno ste verifikovali svoj profil.",
    "YouHaveSuccessfullyChangedYourPassword": "Uspešno ste promenili lozinku.",
    "SuccessfulAction": "Uspešno izvršena operacija",
    "Warning": "Upozorenje",
    "Error": "Greška",
    "ServerLostConnectionDetails": "Veza je izgubljena. Molimo proverite vašu internet konekciju. Ako se problem nastavi, kontaktirajte naš tim za podršku.",
    "ServerLostConnectionTitle": "Veza je izgubljena",
    "PermissionErrorDetails": "Nemate dozvolu za ovu operaciju.",
    "PermissionErrorTitle": "Nemate dozvolu",
    "NotFoundDetails": "Zatraženi resurs nije pronađen, molimo pokušajte ponovo.",
    "NotFoundTitle": "Nije pronađeno",
    "UnexpectedErrorTitle": "Dogodila se greška",
    "UnexpectedErrorDetails": "Naš tim je obavešten i radimo na rešenju problema. Molimo Vas da pokušate ponovo kasnije.",
    "ColorPickerPlaceholder": "npr. #ff0000",
    "True": "Da",
    "False": "Ne",
    "Empty": "Prazno",
    "DatesBefore": "Datumi pre",
    "DatesAfter": "Datumi posle",
    "Equals": "Jednako",
    "MoreThan": "Više od",
    "LessThan": "Manje od",
    "AreYouSureToDelete": "Da li ste sigurni?",
    "SuccessfullyDeletedMessage": "Uspešno brisanje.",
    "Yes": "Da",
    "No": "Ne",
    "SuccessfulSaveToastDescription": "Uspešno sačuvano.",
    "SuccessfulSyncToastDescription": "Uspešno ste ažurirali podatke.",
    "YouHaveSomeInvalidFieldsDescription": "Neka od polja na formi nisu ispravno uneta, molimo Vas da proverite koja i pokušate ponovo.",
    "YouHaveSomeInvalidFieldsTitle": "Neispravna polja na formi",
    "Remove": "Ukloni",
    "AddAbove": "Dodaj iznad",
    "AddBelow": "Dodaj ispod",
    "ListCanNotBeEmpty": "Lista '{{value}}' ne može biti prazna.",
    "NotEmpty": "Polje ne sme biti prazno.",
    "NotEmptyNumberRangeMaxNumberRangeMin": "Polje ne sme biti prazno i vrednost mora da bude između {{min}} i {{max}}.",
    "NotEmptyLengthEmailAddress": "Polje ne sme biti prazno, mora da ima minimum {{min}}, a maksimum {{max}} karaktera i mora biti validna email adresa.",
    "NotEmptyLength": "Polje ne sme biti prazno i mora da ima minimum {{min}}, a maksimum {{max}} karaktera.",
    "NotEmptySingleLength": "Polje ne sme biti prazno i mora da ima {{length}} karaktera.",
    "Length": "Polje mora da ima minimum {{min}}, a maksimum {{max}} karaktera.",
    "NotEmptyNumberRangeMin": "Polje ne sme biti prazno i vrednost mora da bude veća ili jednaka {{min}}.",
    "NumberRangeMin": "Vrednost polja mora da bude veća ili jednaka {{min}}.",
    "SingleLength": "Polje mora da ima {{length}} karaktera.",
    "NotEmptyPrecisionScale": "Vrednost polja mora da ima ukupno {{precision}} cifara, i broj cifara nakon zareza ne sme biti veci od {{scale}}.",
    "IdToken": "/",
    "Browser": "/",
    "NewPassword": "Nova lozinka",
    "NewToJoinNow": "Novi ste? Napravite profil",
    "ExpireAt": "Ističe",
    "UserEmail": "Email",
    "AccessToken": "/",
    "Token": "/",
    "Password": "Lozinka",
    "RefreshToken": "/",
    "IpAddress": "Ip adresa",
    "Reload": "Osvežite",
    "TokenString": "/",
    "Status": "Status",
    "Message": "Poruka",
    "SelectedPermissionIds": "/",
    "SelectedUserIds": "/",
    "RoleDTO": "/",
    "VerificationCode": "Verifikacioni kod",
    "NameLatin": "Naziv latinicom",
    "Description": "Opis",
    "DescriptionLatin": "Opis latinicom",
    "Code": "Kod",
    "Id": "Id",
    "Version": "Verzija",
    "ModifiedAt": "Izmenjeno",
    "Roles": "Uloge",
    "Users": "Korisnici",
    "ExternalProvider": "/",
    "ForgotPasswordVerificationToken": "/",
    "JwtAuthResult": "/",
    "AuthResult": "/",
    "LoginVerificationToken": "/",
    "RefreshTokenRequest": "/",
    "Registration": "/",
    "RegistrationVerificationResult": "/",
    "RegistrationVerificationToken": "/",
    "RoleSaveBody": "/",
    "VerificationTokenRequest": "/",
    "Permission": "Dozvola",
    "Role": "Uloga",
    "RoleUser": "/",
    "IsMarkedAsRead": "Označeno kao pročitano",
    "Checked": "Čekirano",
    "NotificationDTO": "Notifikacija",
    "TableFilter": "Filter tabele",
    "SelectedIds": "Izabrani",
    "UnselectedIds": "Odčekirani",
    "IsAllSelected": "Sve je izabrano",
    "UserDTO": "/",
    "SelectedRoleIds": "/",
    "Price": "Cena",
    "Category": "Kategorija",
    "LinkToWebsite": "Link do sajta",
    "EmailBody": "Sadržaj email-a",
    "Notifications": "Notifikacije",
    "LogoImageData": "/",
    "LogoImage": "Logo",
    "PrimaryColor": "Primarna boja",
    "OrderNumber": "Redni broj",
    "ValidFrom": "Važi od",
    "ValidTo": "Važi do",
    "Guid": "Guid",
    "Product": "Proizvod",
    "Transaction": "Transakcija",
    "NumberOfFailedAttemptsInARow": "Broj neuspešnih pokušaja uzastopno",
    "BirthDate": "Datum rođenja",
    "Gender": "Pol",
    "Notification": "Notifikacija",
    "User": "Korisnik",
    "Brand": "Brend",
    "NotificationSaveBody": "/",
    "QrCode": "QR kod",
    "NotificationUser": "/",
    "Primeng": {
      "dayNames": [
        "Nedelja",
        "Ponedeljak",
        "Utorak",
        "Sreda",
        "Četvrtak",
        "Petak",
        "Subota"
      ],
      "dayNamesShort": [
        "Ned",
        "Pon",
        "Uto",
        "Sre",
        "Čet",
        "Pet",
        "Sub"
      ],
      "dayNamesMin": [
        "Ne",
        "Po",
        "Ut",
        "Sr",
        "Če",
        "Pe",
        "Su"
      ],
      "monthNames": [
        "Januar",
        "Februar",
        "Mart",
        "April",
        "Maj",
        "Jun",
        "Jul",
        "Avgust",
        "Septembar",
        "Oktobar",
        "Novembar",
        "Decembar"
      ],
      "monthNamesShort": [
        "Jan",
        "Feb",
        "Mar",
        "Apr",
        "Maj",
        "Jun",
        "Jul",
        "Avg",
        "Sep",
        "Okt",
        "Nov",
        "Dec"
      ],
      "today": "Danas",
      "weekHeader": "Vik",
      "clear": "Uklonite",
      "apply": "Pretražite",
      "emptyMessage": "Nema rezultata",
      "emptyFilterMessage": "Nema rezultata"
    },
    "EmptyMessage": "Nema rezultata",
    "ClearFilters": "Uklonite sve filtere",
    "ApplyFilters": "Primeni filtere",
    "YouDoNotHaveAnyNotifications": "Nemate nijednu notifikaciju.",
    "LoginRequired": "Morate biti prijavljeni da biste izvršili ovu radnju. Molimo prijavite se i pokušajte ponovo.",
    "BadRequestDetails": "Sistem ne može da obradi zahtev. Molimo vas da proverite zahtev i pokušate ponovo."
}
""";
        }

        private static string GetTranslocoEnJsonCode()
        {
            return $$$"""
{
  "SelectFromTheList": "Select...",
  "OnDate": "On date",
  "TierList": "Loyalty Tiers",
  "Partner": "Partner",
  "Submit": "Confirm",
  "UserList": "Users",
  "SuperRoles": "Super roles",
  "HasLoggedInWithExternalProvider": "Logged in with external provider",
  "IsDisabled": "Blocked",
  "PartnerRoleList": "Roles",
  "Save": "Save",
  "AddNewTier": "Add new loyalty tier",
  "AddNewBusinessSystemTier": "Add new discount product group",
  "SegmentationList": "Segmentations",
  "AddNewSegmentationItem": "Add new segmentation item",
  "RoleList": "Roles",
  "Permissions": "Permissions",
  "NotifyUsers": "Notify users",
  "Settings": "Settings",
  "Recipients": "Recipients",
  "RecipientsForNotification": "Recipients",
  "SendEmailNotification": "Send email notification",
  "PartnerList": "Partners",
  "SelectThePartner": "Select partner",
  "AgreementsOnRegister": "By clicking Agree and Join or Continue, you accept the",
  "UserAgreement": "terms of use",
  "PrivacyPolicy": "privacy policy",
  "and": "and",
  "CookiePolicy": "cookie policy",
  "AgreeAndJoin": "Agree and Join",
  "AlreadyHasAccount": "Already have an account? Log in",
  "ContinueWithGoogle": "Continue with Google account",
  "or": "or",
  "All": "All",
  "AccountVerificationHeader": "Profile Verification",
  "AccountVerificationTitle": "Verify your email address",
  "AccountVerificationDescription": "We have sent a verification code to {{email}}. Please check your inbox or spam folder and enter the code we sent to complete the process. Thank you!",
  "GoToGmail": "Go to Gmail",
  "GoToYahoo": "Go to Yahoo",
  "ResendVerificationCodeFirstPart": "If you didn't find it, you can",
  "ResendVerificationCodeLinkSecondPart": "resend the verification code.",
  "ForgotPassword": "Forgot password?",
  "Login": "Log in",
  "NewToJoinNow": "New here? Create a profile",
  "RememberYourPassword": "Remembered your password?",
  "ResetPassword": "Reset password",
  "DragAndDropFilesHereToUpload": "Drag and drop files here to upload.",
  "PleaseConfirmToProceed": "Please confirm to proceed.",
  "Cancle": "Cancel",
  "Confirm": "Confirm",
  "Clear": "Clear",
  "ExportToExcel": "Export to Excel",
  "Select": "Select",
  "SyncDiscountProductGroups": "Sync discount product groups",
  "NoRecordsFound": "No records found.",
  "Loading": "Loading",
  "TotalRecords": "Total records",
  "AddNew": "Add New",
  "Return": "Return",
  "ProductsForYouTitle": "Products for You",
  "Currency": "$",
  "PartnerIntermediateStepTitle": "You can change partners at any time",
  "PartnerIntermediateStepDescription": "Choose the partner whose loyalty program you want to visit.",
  "Actions": "Actions",
  "Details": "Details",
  "User": "User",
  "Points": "Points",
  "Tier": "Loyalty tier",
  "Segmentation": "Segmentation",
  "CreatedAt": "Created at",
  "FirstTimeFieldFillTooltipText": "Fill in the field for the first time and earn extra points!",
  "Delete": "Delete",
  "Gender": "Gender",
  "Name": "Name",
  "PointsForTheFirstTimeFill": "Points for first-time fill",
  "Title": "Title",
  "SuccessfulAttempt": "Your attempt has been processed.",
  "MarkAsRead": "Mark as read",
  "MarkAsUnread": "Mark as unread",
  "Email": "Email",
  "Slug": "URL path",
  "YourProfile": "Your profile",
  "Logout": "Log out",
  "Home": "Home",
  "SuperAdministration": "Super Administration",
  "Administration": "Administration",
  "SuccessfullySentVerificationCode": "Verification code sent successfully.",
  "YouHaveSuccessfullyVerifiedYourAccount": "You have successfully verified your account.",
  "YouHaveSuccessfullyChangedYourPassword": "You have successfully changed your password.",
  "SuccessfulAction": "Operation successful",
  "Warning": "Warning",
  "Error": "Error",
  "ServerLostConnectionDetails": "Connection lost. Please check your internet connection. If the problem persists, please contact our support team.",
  "ServerLostConnectionTitle": "Connection Lost",
  "PermissionErrorDetails": "You do not have permission for this operation.",
  "PermissionErrorTitle": "Permission Denied",
  "NotFoundDetails": "The requested resource was not found, please try again.",
  "NotFoundTitle": "Not Found",
  "UnexpectedErrorTitle": "An error occurred",
  "UnexpectedErrorDetails": "Our team has been notified and we are working on a solution. Please try again later.",
  "ColorPickerPlaceholder": "e.g., #ff0000",
  "True": "True",
  "False": "False",
  "Empty": "Empty",
  "Revert": "Revert to state",
  "DatesBefore": "Dates before",
  "DatesAfter": "Dates after",
  "Equals": "Equals",
  "MoreThan": "More than",
  "LessThan": "Less than",
  "AreYouSure": "Are you sure?",
  "SuccessfullyDeletedMessage": "Successfully deleted.",
  "Yes": "Yes",
  "No": "No",
  "SuccessfulSaveToastDescription": "Successfully saved.",
  "SuccessfulSyncToastDescription": "Successfully updated data.",
  "YouHaveSomeInvalidFieldsDescription": "Some fields on the form were not entered correctly, please check them and try again.",
  "YouHaveSomeInvalidFieldsTitle": "Invalid Fields on Form",
  "Remove": "Remove",
  "AddAbove": "Add above",
  "AddBelow": "Add below",
  "ListCanNotBeEmpty": "The list '{{value}}' cannot be empty.",
  "NotEmpty": "Field cannot be empty.",
  "NotEmptyNumberRangeMaxNumberRangeMin": "Field cannot be empty and value must be between {{min}} and {{max}}.",
  "NotEmptyLengthEmailAddress": "Field cannot be empty, must have a minimum of {{min}} and a maximum of {{max}} characters, and must be a valid email address.",
  "NotEmptyLength": "Field cannot be empty and must have a minimum of {{min}} and a maximum of {{max}} characters.",
  "NotEmptySingleLength": "Field cannot be empty and must have {{length}} characters.",
  "Length": "Field must have a minimum of {{min}} and a maximum of {{max}} characters.",
  "NotEmptyNumberRangeMin": "Field cannot be empty and value must be greater than or equal to {{min}}.",
  "NumberRangeMin": "Field value must be greater than or equal to {{min}}.",
  "SingleLength": "Field must have {{length}} characters.",
  "NotEmptyPrecisionScale": "Field value must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "IdToken": "/",
  "Browser": "/",
  "NewPassword": "New password",
  "ExpireAt": "Expires at",
  "UserEmail": "Email",
  "AccessToken": "/",
  "Token": "/",
  "Password": "Password",
  "RefreshToken": "/",
  "IpAddress": "IP address",
  "BusinessSystemUpdatePointsScheduledTaskList": "Points updates",
  "SuccessfullyDoneBusinessSystemUpdatePointsScheduledTaskList": "Points updates successfully performed",
  "UpdatePoints": "Update points",
  "AutomaticUpdatePoints": "Automatic points update",
  "File": "File",
  "ManualUpdatePoints": "Manual points update",
  "ExcelUpdatePoints": "Excel points update",
  "ManualUpdatePointsFromDate": "Manual points update from date",
  "ManualUpdatePointsToDate": "Manual points update to date",
  "Reload": "Refresh",
  "TransactionsTo": "Transactions to",
  "TransactionsFrom": "Transactions from",
  "IsManuallyStarted": "Manually started",
  "TransactionList": "Transactions",
  "TokenString": "/",
  "Status": "Status",
  "Message": "Message",
  "SelectedPermissionIds": "/",
  "SelectedUserIds": "/",
  "RoleDTO": "/",
  "VerificationCode": "Verification code",
  "NameLatin": "Name (Latin)",
  "Description": "Description",
  "DescriptionLatin": "Description (Latin)",
  "Code": "Code",
  "Id": "ID",
  "Version": "Version",
  "ModifiedAt": "Modified at",
  "Roles": "Roles",
  "Users": "Users",
  "ExternalProvider": "/",
  "ForgotPasswordVerificationToken": "/",
  "JwtAuthResult": "/",
  "AuthResult": "/",
  "LoginVerificationToken": "/",
  "RefreshTokenRequest": "/",
  "Registration": "/",
  "RegistrationVerificationResult": "/",
  "RegistrationVerificationToken": "/",
  "RoleSaveBody": "/",
  "VerificationTokenRequest": "/",
  "Permission": "Permission",
  "Role": "Role",
  "RoleUser": "/",
  "IsMarkedAsRead": "Marked as read",
  "Checked": "Checked",
  "PointsMultiplier": "Points multiplier",
  "NotificationDTO": "Notification",
  "TableFilter": "Table filter",
  "SelectedIds": "Selected",
  "UnselectedIds": "Unselected",
  "IsAllSelected": "All selected",
  "TransactionCode": "Transaction code",
  "Discount": "Discount",
  "PartnerNotificationDTO": "Notification",
  "PartnerRoleDTO": "Role",
  "SelectedPartnerUserIds": "/",
  "UserDTO": "/",
  "SelectedRoleIds": "/",
  "PartnerUserDTO": "/",
  "SelectedPartnerRoleIds": "/",
  "SelectedSegmentationItemIds": "/",
  "Price": "Price",
  "Category": "Category",
  "LinkToWebsite": "Link to website",
  "SegmentationDTO": "/",
  "SegmentationItemsDTO": "Segmentation items",
  "EmailBody": "Email content",
  "Notifications": "Notifications",
  "PartnerProfile": "Partner profile",
  "SuccessfulSaveAndRefreshThePageToastDescription": "Successfully saved. To see partner changes, please refresh the page.",
  "NotificationList": "Notifications",
  "AddNewDiscountProductGroup": "Add new discount product group",
  "LogoImageData": "/",
  "StartUpdatePointsScheduledTask": "Start automatic points update",
  "PauseUpdatePointsScheduledTask": "Pause automatic points update",
  "LogoImage": "Logo",
  "Info": "Information",
  "PrimaryColor": "Primary color",
  "PointsForTheFirstTimeGenderFill": "Points for first-time gender fill",
  "PointsForTheFirstTimeBirthDateFill": "Points for first-time birth date fill",
  "ProductsRecommendationEndpoint": "Product recommendation path",
  "PartnerNotifications": "Notifications",
  "HasFilledGenderForTheFirstTime": "Gender filled for the first time",
  "HasFilledBirthDateForTheFirstTime": "Birth date filled for the first time",
  "CheckedSegmentationItems": "Checked segmentation items",
  "OrderNumber": "Order number",
  "ValidFrom": "Valid from",
  "ValidTo": "Valid to",
  "Guid": "GUID",
  "Product": "Product",
  "Transaction": "Transaction",
  "NumberOfFailedAttemptsInARow": "Number of failed attempts in a row",
  "BirthDate": "Birth Date",
  "Notification": "Notification",
  "PartnerUser": "User",
  "SegmentationItem": "Segmentation item",
  "User": "User",
  "Brand": "Brand",
  "MergedPartnerUser": "User",
  "NotificationSaveBody": "/",
  "PartnerNotificationSaveBody": "/",
  "PartnerRoleSaveBody": "/",
  "PartnerUserSaveBody": "/",
  "QrCode": "QR Code",
  "SegmentationSaveBody": "/",
  "UserSaveBody": "/",
  "NotificationUser": "/",
  "PartnerNotification": "Notification",
  "PartnerNotificationPartnerUser": "/",
  "PartnerPermission": "Permission",
  "PartnerRole": "Role",
  "TransactionProduct": "Transaction product",
  "TransactionStatus": "Transaction status",
  "Primeng": {
    "dayNames": [
      "Sunday",
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday"
    ],
    "dayNamesShort": [
      "Sun",
      "Mon",
      "Tue",
      "Wed",
      "Thu",
      "Fri",
      "Sat"
    ],
    "dayNamesMin": [
      "Su",
      "Mo",
      "Tu",
      "We",
      "Th",
      "Fr",
      "Sa"
    ],
    "monthNames": [
      "January",
      "February",
      "March",
      "April",
      "May",
      "June",
      "July",
      "August",
      "September",
      "October",
      "November",
      "December"
    ],
    "monthNamesShort": [
      "Jan",
      "Feb",
      "Mar",
      "Apr",
      "May",
      "Jun",
      "Jul",
      "Aug",
      "Sep",
      "Oct",
      "Nov",
      "Dec"
    ],
    "today": "Today",
    "weekHeader": "Week",
    "clear": "Clear",
    "apply": "Apply",
    "emptyMessage": "No results",
    "emptyFilterMessage": "No results"
  },
  "LeftCornerPartnersEmptyMessage": "You don't have a profile for any partner",
  "EmptyMessage": "No results",
  "ClearFilters": "Clear all filters",
  "ApplyFilters": "Apply filters",
  "PartnerNotificationList": "Notifications",
  "PartnerUserList": "Users",
  "YouDoNotHaveAnyNotification": "You do not have any notifications.",
  "YouDoNotHaveAnyAchievement": "You haven't earned any points yet.",
  "PointsHistory": "Points History",
  "LoginRequired": "You need to be logged in to perform this action. Please log in and try again.",
  "BadRequestDetails": "The system cannot process the request. Please check your request and try again.",
  "BusinessSystemList": "Business systems",
  "BusinessSystem": "Business system"
}
""";
        }

        private static string GetValidatorsTsCode()
        {
            return $$"""
import { TranslocoService } from '@jsverse/transloco';
import { Injectable } from '@angular/core';
import { ValidatorServiceGenerated } from "./validators.generated";
import { ValidatorAbstractService, SpiderlyFormControl, SpiderlyValidatorFn } from 'spiderly';

@Injectable({
    providedIn: 'root',
})
export class ValidatorService extends ValidatorAbstractService {

    constructor(
        protected override translocoService: TranslocoService,
        private validatorServiceGenerated: ValidatorServiceGenerated,
    ) {
        super(translocoService);
    }

    override setValidator = (formControl: SpiderlyFormControl, className: string): SpiderlyValidatorFn => {
        return this.validatorServiceGenerated.setValidator(formControl, className);
    }

}
""";
        }

        private static string GetConfigServiceTsCode()
        {
            return $$"""
import { Injectable } from "@angular/core";
import { environment } from "src/environments/environment";
import { ConfigBaseService } from 'spiderly';

@Injectable({
  providedIn: 'root',
})
export class ConfigService extends ConfigBaseService
{
    override production: boolean = environment.production;
    override apiUrl: string = environment.apiUrl;
    override frontendUrl: string = environment.frontendUrl;
    override GoogleClientId: string = environment.GoogleClientId;
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

        private static string GetMergeLabelsCode()
        {
            return $$"""
import { Injectable } from "@angular/core";
import { TranslateLabelsGeneratedService } from "./labels.generated";
import { TranslateLabelsAbstractService } from 'spiderly';

@Injectable({
    providedIn: 'root',
})
export class TranslateLabelsService extends TranslateLabelsAbstractService {

    constructor(
        private translateLabelsGeneratedService: TranslateLabelsGeneratedService,
    ) {
        super();
    }

    translate = (name: string) => {
        let result = null;

        result = this.translateLabelsGeneratedService.translate(name);
        if (result != null)
            return result;

        return name;
    }
}
""";
        }

        private static string GetMergeClassNamesTsCode()
        {
            return $$"""
import { Injectable } from "@angular/core";
import { TranslateClassNamesGeneratedService } from "./class-names.generated";

@Injectable({
    providedIn: 'root',
})
export class TranslateClassNamesService {

    constructor(
        private translateClassNamesGeneratedService: TranslateClassNamesGeneratedService,
    ) {
    }

    translate(name: string){
        let result = null;

        result = this.translateClassNamesGeneratedService.translate(name);
        if (result != null)
            return result;

        return name;
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
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { ConfigService } from '../config.service';
import { AuthBaseService } from 'spiderly';

@Injectable({
  providedIn: 'root',
})
export class AuthService extends AuthBaseService implements OnDestroy {

  constructor(
    protected override router: Router,
    protected override http: HttpClient,
    protected override externalAuthService: SocialAuthService,
    protected override apiService: ApiService,
    protected override config: ConfigService,
    @Inject(PLATFORM_ID) protected override platformId: Object,
  ) {
    super(router, http, externalAuthService, apiService, config, platformId);
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
import { LayoutBaseService } from 'spiderly';
import { AuthService } from '../auth/auth.service';

@Injectable({
  providedIn: 'root',
})
export class LayoutService extends LayoutBaseService implements OnDestroy {

    constructor(
        protected override apiService: ApiService,
        protected override config: ConfigService,
        protected override authService: AuthService,
    ) {
        super(apiService, config, authService);

        this.initUnreadNotificationsCountForCurrentUser();
    }

}

""";
        }

        private static string GetLayoutComponentHtmlCode(bool hasTopMenu)
        {
            return $$"""
<spiderly-layout [menu]="menu" {{(hasTopMenu ? "[isSideMenuLayout]=\"false\"" : "")}}></spiderly-layout>
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
import { BusinessPermissionCodes } from '../enums/business-enums.generated';

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
                                permissionCodes?.includes(BusinessPermissionCodes.ReadUser) ||
                                permissionCodes?.includes(SecurityPermissionCodes.ReadRole) ||
                                permissionCodes?.includes(BusinessPermissionCodes.ReadNotification)
                            )
                        },
                        items: [
                            {
                                label: this.translocoService.translate('UserList'),
                                icon: 'pi pi-fw pi-user',
                                routerLink: [`/${this.config.administrationSlug}/users`],
                                hasPermission: (permissionCodes: string[]): boolean => { 
                                    return (
                                        permissionCodes?.includes(BusinessPermissionCodes.ReadUser)
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
                            {
                                label: this.translocoService.translate('NotificationList'),
                                icon: 'pi pi-fw pi-bell',
                                routerLink: [`/${this.config.administrationSlug}/notifications`],
                                hasPermission: (permissionCodes: string[]): boolean => { 
                                    return (
                                        permissionCodes?.includes(BusinessPermissionCodes.ReadNotification)
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

        private static string GetGitIgnoreData()
        {
            return $$"""
# C#
**/.vs/
**/*.exe
**/*.dll
**/*.log
**/bin/
**/obj/
**/*.user
**/*.suo
**/*.pdb
**/appsettings*.json

# Angular
**/dist/
**/tmp/
**/out-tsc/
**/bazel-out/
**/.angular/cache/

# Node
**/node_modules/
**/npm-debug.log
**/yarn-error.log
**/*.env
**/*.env.local

# IDEs and editors
**/.idea/
**/.project
**/.classpath
**/.c9/
**/*.launch
**/.settings/
**/*.sublime-workspace

# Visual Studio Code
**/.vscode/*
**/!.vscode/settings.json
**/!.vscode/tasks.json
**/!.vscode/launch.json
**/!.vscode/extensions.json
**/.history/*

# Miscellaneous
**/.sass-cache/
**/connect.lock
**/coverage
**/libpeerconnection.log
**/testem.log
**/typings
**/*.pid
**/*.bak
**/*.tmp

# System files
**/.DS_Store
**/Thumbs.db
""";
        }

        private static string GetREADMEData(string appName, string spiderlyVersion)
        {
            return $$"""
# {{appName}}
This project was generated with [Spiderly CLI](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.CLI) version {{spiderlyVersion}}.

For more information about Spiderly, visit our [documentation](https://www.spiderly.dev/docs/getting-started).
""";
        }

        private static string GetFrontendREADMEData(string appName, string spiderlyVersion)
        {
            return $$"""
# {{appName}}
This project was generated with [Spiderly CLI](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.CLI) version {{spiderlyVersion}}.

## Development server
Run `ng serve` for a dev server. Navigate to http://localhost:4200/. The app will automatically reload if you change any of the source files.

## Code scaffolding
Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|module`.

### Further help
To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI README](https://github.com/angular/angular-cli/blob/main/README.md).
""";
        }

        private static string GetFaviconIcoData()
        {
            return ""; // Can't add favicon as text, we need to use base64, we are not deleting this just because user could find the right place to change it easily.
        }

        private static string GetLogoSvgData()
        {
            return """
<svg xmlns="http://www.w3.org/2000/svg" width="1080" zoomAndPan="magnify" viewBox="0 0 810 810" height="1080" preserveAspectRatio="xMidYMid meet" xmlns:v="https://vecta.io/nano"><g fill="#db2777"><path d="M383.691 25.176c-8.957 12.43-25.23 36.102-38.301 55.664l-17.734 25.59c-1.828 2.285-5.758 7.586-8.59 11.883-2.926 4.297-6.219 8.773-7.406 10.055-3.199 3.566-3.383 5.211-1.371 14.074 1.008 4.57 4.48 22.488 7.77 39.852l8.777 45.246 5.027 24.676c1.098 6.035 2.742 13.254 3.473 15.996.824 2.742 1.461 5.668 1.461 6.398 0 .82-2.191 2.559-5.117 3.93l-5.121 2.559-16.543-16.27c-22.672-22.211-87.57-88.656-95.434-97.617-3.473-4.02-7.953-8.68-9.781-10.328l-3.473-3.016-3.75 2.195c-2.102 1.277-6.121 3.199-8.863 4.387s-6.949 3.199-9.234 4.66c-4.754 2.926-30.531 14.992-45.156 21.207-5.484 2.285-12.891 5.848-16.453 7.859-3.477 2.012-12.523 6.672-20.113 10.422-12.246 6.121-22.668 11.789-45.43 24.859-3.93 2.195-7.68 4.023-8.316 4.023-.734 0-1.922.73-2.742 1.645-1.648 1.828-11.887 31.805-18.832 55.48l-8.684 28.332c-2.285 6.949-4.023 12.887-3.84 12.98.457.457 22.668-30.711 30.441-42.684 8.133-12.613 18.281-30.437 19.102-33.547.367-1.461 2.469-2.832 7.496-4.934 3.84-1.555 12.891-5.574 20.203-8.867l17.918-7.859c2.559-1.098 7.949-3.656 11.973-5.758 4.113-2.195 11.336-5.395 15.996-7.129 4.754-1.828 9.875-4.023 11.426-4.937s5.027-2.559 7.773-3.562c2.742-1.098 10.875-4.57 18.188-7.863s13.895-6.031 14.719-6.031c.73 0 1.918-.547 2.469-1.098 1.277-1.277 1.734-.914 28.977 28.52 13.895 14.988 26.051 27.691 62.797 65.441 5.945 6.125 10.789 11.516 10.789 11.883 0 .457-1.008 1.555-2.195 2.375-2.102 1.555-2.469 1.465-12.98-2.832-6.031-2.375-14.535-6.031-19.105-8.043-11.609-5.211-37.02-15.996-45.703-19.469-4.023-1.555-13.437-5.574-20.844-8.867l-21.664-9.047c-7.859-3.016-8.133-3.016-11.973-1.738-2.195.824-4.57 2.012-5.395 2.742s-3.473 2.375-5.941 3.746c-2.469 1.281-6.398 3.934-8.773 5.941-2.379 1.922-4.937 3.566-5.668 3.566-1.281 0-16.637 11.059-25.32 18.371-6.766 5.574-34.098 24.313-50.551 34.641-18.742 11.699-25.414 16.82-26.418 20.566-.457 1.461-1.371 8.133-2.102 14.805s-1.738 14.441-2.195 17.367c-1.918 10.512-6.398 42.137-7.863 54.66-.73 7.035-2.191 19.191-3.199 26.961s-2.465 20.75-3.199 28.793c-.82 8.043-2.285 20.383-3.289 27.328-1.828 12.34-1.828 13.16-.367 25.594 1.648 14.348 7.223 36.469 8.137 32.535 1.555-6.852 8.957-44.691 10.238-52.555.824-5.027 3.016-16.359 5.027-25.133 1.918-8.777 4.938-24.039 6.672-33.82l6.855-37.93 10.148-58.59 1.004-6.398 6.219-4.66c3.473-2.559 8.5-5.852 11.152-7.223 2.742-1.371 6.215-3.379 7.676-4.57 1.555-1.094 3.199-2.191 3.656-2.281.551-.094 1.738-.641 2.742-1.281 1.922-1.187 14.809-8.316 26.145-14.625 3.84-2.102 11.52-6.945 17.004-10.785 5.484-3.746 13.254-8.684 17.273-10.875l9.145-5.211c1.551-1.098 2.465-.914 6.672 1.188 2.648 1.371 7.586 3.289 10.879 4.387 7.859 2.379 24.039 8.5 29.891 11.336 7.586 3.563 32.543 13.801 37.934 15.445 6.125 1.918 21.758 7.953 24.043 9.23 1.461.824 1.461 1.281-.551 7.406-1.918 6.031-2.559 6.855-7.77 10.785-10.602 8.043-16.82 16.633-20.84 29.246-6.125 19.285-2.102 39.305 11.426 56.211 6.488 7.953 6.582 8.137 5.574 4.48-1.277-4.48-1.094-10.055.551-17.457 1.828-8.043 7.496-23.398 11.152-30.437l2.742-5.211 1.918 4.297c2.469 5.574 10.969 13.801 18.191 17.641 6.488 3.473 17.824 7.039 25.23 7.953 2.832.363 5.301 1.004 5.574 1.371.273.457-1.098 2.742-3.016 5.207-1.918 2.379-5.211 6.855-7.223 9.871-2.102 3.02-5.574 7.223-7.77 9.234-6.398 6.215-19.012 13.434-25.687 14.715-2.648.547-2.465.641 3.02 2.285 7.496 2.102 21.664 2.191 32.082.184 9.234-1.738 21.848-7.586 29.344-13.621 9.141-7.219 17.004-18.461 22.855-32.902 2.465-6.125 3.379-7.406 7.402-10.055 2.559-1.738 4.754-3.016 4.844-2.926.457.457 6.492 45.883 7.223 54.566.367 3.746 1.188 10.145 1.918 14.168 1.648 8.867 3.934 30.07 4.023 37.109 0 5.484-1.281 9.23-8.684 25.043l-7.496 16.91c-1.738 4.297-3.473 8.133-3.93 8.684-.367.457-1.371 3.199-2.285 5.941s-3.016 7.859-4.754 11.242c-1.738 3.473-4.297 9.414-5.758 13.25-1.371 3.934-5.668 14.441-9.418 23.492s-7.586 18.918-8.41 21.938c-.914 3.016-3.016 9.137-4.75 13.707-3.934 10.328-10.789 33.727-12.066 41.59-.551 3.289-2.195 10.055-3.75 15.078-2.832 9.598-5.758 20.66-14.168 54.387-2.832 11.242-5.578 21.297-6.125 22.211-1.918 3.563 3.293-.551 15.176-12.25 11.699-11.332 12.066-11.883 14.168-18.371 1.188-3.656 4.203-11.789 6.672-18.098 2.559-6.305 7.586-20.109 11.242-30.711 6.125-17.73 13.438-35.465 19.656-47.895 2.559-5.207 21.754-51.273 26.965-64.895 1.645-4.297 6.949-16.359 11.793-26.871l8.773-19.379c0-.273 2.926-6.215 6.398-13.25 6.035-12.066 6.398-13.164 6.398-18.922 0-3.383-1.004-12.887-2.285-21.203l-4.57-30.988-4.113-30.801c-1.004-8.133-3.016-21.113-4.477-28.789-1.465-7.77-2.562-14.168-2.379-14.441.367-.277 5.211 3.016 26.512 18.188 4.02 2.926 11.148 7.129 15.723 9.504 4.66 2.285 10.602 5.578 13.254 7.223 2.648 1.734 10.785 6.305 18.008 10.238s14.992 8.59 17.277 10.328l4.203 3.199 21.48-.641c20.57-.551 32.359-1.187 66.457-3.383l15.082-.914 6.125 4.938c11.059 8.863 22.395 17.547 34.918 26.871 6.766 5.117 16.363 12.703 21.391 16.816 10.148 8.684 19.289 15.996 25.047 20.109 4.66 3.383 4.297 3.473 15.906-4.023l7.859-5.117-8.316-8.137c-13.805-13.344-40.77-37.746-67.371-60.961l-17.277-15.355-7.859-7.133-51.922.367-51.922.457-18.008-11.883-30.805-20.656-23.129-15.445c-5.758-3.75-10.145-7.039-9.871-7.312.184-.273 2.379.184 4.844.914 2.379.82 8.961 2.285 14.445 3.383 5.574 1.094 14.809 3.199 20.566 4.66l10.605 2.648 23.672-5.48c25.688-5.941 50.277-12.613 77.699-21.023l25.598-7.68 17.641-5.027 9.414-2.738 16.637 3.195 29.711 6.035 12.98 2.926 10.328 10.234c5.578 5.578 13.895 14.168 18.375 19.195 4.57 4.934 18.922 19.375 31.992 31.898l30.164 29.523c3.477 3.563 7.039 6.578 7.863 6.578s3.746-1.187 6.488-2.738c2.742-1.465 6.035-2.742 7.406-2.742 3.746 0 3.656-1.098-.09-5.578-1.922-2.191-7.133-8.773-11.609-14.625-16.637-21.75-23.77-30.707-30.531-38.297-8.32-9.414-15.816-18.645-24.59-30.16-3.656-4.754-11.152-13.895-16.82-20.293l-10.238-11.605-8.684-2.742c-11.336-3.566-42.051-10.512-75.141-17.094-1.461-.273-9.324 1.645-21.023 5.121-10.148 3.105-23.309 6.852-29.07 8.316l-21.023 5.758c-5.758 1.738-14.625 4.02-19.652 5.117-12.891 2.926-61.336 15.355-66.457 17-4.203 1.371-4.934 1.281-20.566-1.918-18.008-3.656-20.934-4.57-21.664-6.582-.457-1.094 0-1.187 2.469-.547 4.57 1.188 32.266.824 39.945-.547 82.453-14.262 141.961-73.488 148.176-147.43 1.738-19.836-.73-35.738-8.316-54.293-10.879-26.691-30.988-44.148-57.957-50.363-8.133-1.918-34.367-2.102-44.426-.367-18.098 3.199-29.523 7.039-45.703 15.266-34.187 17.551-63.258 49.539-77.152 85.094-5.027 12.797-6.488 18.371-8.773 32.449-3.293 20.93-2.012 42.684 3.656 61.785 1.461 4.844 2.469 8.957 2.285 9.141s-1.922.09-3.93-.273l-3.566-.551-1.918-8.406c-2.742-12.156-7.953-39.668-11.152-58.68l-5.027-28.793c-1.281-6.762-2.926-16.633-3.656-21.934-.824-5.301-1.738-10.879-2.195-12.34-.641-2.285-.184-3.84 2.375-8.684 3.477-6.766 19.016-31.352 38.941-61.789 7.406-11.332 13.438-20.93 13.438-21.387 0-1.918 5.301-3.562 17.824-5.391 7.59-1.191 19.473-3.293 26.418-4.754l30.625-5.852c17.367-3.105 27.148-5.301 28.246-6.305.273-.273-2.195-.551-5.395-.551s-17.734-.82-32.359-1.824l-48.449-3.199-27.605-1.922-5.574-.547zm0 0"/><path d="M299.465 465.965c-5.027-2.008-8.043-5.391-9.23-9.32-5.941 3.836-12.523 7.035-16.73 7.859-2.648.547-2.465.641 3.02 2.285 6.672 1.918 18.648 2.102 28.52.73-1.922-.363-3.75-.82-5.578-1.555zm0 0"/></g><path fill="#b62b70" d="M425.98 323.289c-.457-1.098 0-1.187 2.465-.547.184.09.457.09.734.18-1.098-2.375-.734-5.023-.734-7.586l-1.187-2.648c-.363-.73-.457-1.555-.547-2.379-1.281-1.734-2.742-3.473-4.207-5.117l-2.648-2.742-.457-.457-4.57-4.113-17.645-15.172c-.273.09-.547.184-.82.184-.551.09-1.191.09-1.738.184s-1.187.09-1.738.18c8.32 13.348 6.949 30.621 3.109 45.152-2.832 9.691-7.496 18.281-13.984 25.777-5.941 9.141-13.437 16.727-22.578 22.848-16.273 11.793-37.297 19.746-57.59 19.746-16.73 0-31.812-6.309-42.871-18.465.09.824.457 3.199.547 3.566.367 1.828.73 3.656 1.188 5.48.914 3.566 2.105 6.949 3.656 10.238l1.828-3.562 1.922 4.293c2.469 5.578 10.969 13.805 18.191 17.641 6.488 3.473 17.824 7.039 25.227 7.953 2.836.367 5.305 1.004 5.578 1.371.184.273-.367 1.371-1.281 2.742 1.922-.09 3.84.09 5.395.73.547.273 1.188.457 1.828.641h.914c.09 0 .273 0 .457-.094 3.383-.82 6.945-1.551 10.422-2.008l1.918-.273c1.371-.184 2.469-.277 3.293-.367h-.094c3.016-.273 3.016-.273.094 0 1.734.184 4.477-.914 6.121-1.371 4.113-1.004 8.137-2.285 12.25-3.109l1.555-.273c.273-.547.457-1.187.73-1.734 2.469-6.125 3.383-7.406 7.406-10.055 2.559-1.738 4.754-3.016 4.844-2.926s.457 2.195.914 5.574c2.559-3.105 6.215-5.574 9.688-7.129.551-.273 1.008-.457 1.555-.73.094 0 .457-.273 1.188-.641l2.379-1.277c.09-.094.184-.094.184-.184.09-.184.363-.457.73-.914 1.004-1.645 2.285-3.016 3.473-4.48.273-.273 2.012-2.832 1.465-2.008 1.277-2.012 2.465-4.113 3.836-6.125 2.379-3.383 5.395-6.309 8.32-9.141 1.098-1.098 2.285-2.102 3.383-3.109-.094 0 1.004-1.094 1.555-1.645-.367.367 2.008-3.289 2.285-3.746.73-1.371 1.461-2.832 2.191-4.297 3.473-7.312 6.582-14.168 11.063-20.84-.824-2.832.09-6.215 1.371-8.684 0 0 0-.09.09-.09.094-.184.094-.457.184-.641.09-1.098.09-2.285.09-3.383 0-2.738.914-5.301 2.379-7.586-3.75-.914-4.844-1.734-5.301-2.832zm0 0"/><path d="M255.223 420.082a36.54 36.54 0 0 1-6.488 4.023c-3.473 1.734-8.137 2.648-12.25 2.285a67.4 67.4 0 0 0 9.781 16.359c6.488 7.953 6.582 8.137 5.574 4.48-1.277-4.48-1.094-10.055.551-17.461.547-2.648 1.555-6.031 2.832-9.687zm116.551-153.004c-.094-.367-.094-.73-.184-1.098-.184-.457-.184-.914-.273-1.281v-.09l-.914-4.387c-.184-.457-.277-.914-.277-1.371 0-.273-.09-.547-.09-.824v-.09c0-.184 0-.273-.09-.457v-.273c-.094-.641-.367-1.281-.551-1.828l-.73-1.922-7.859 1.828c-8.594 2.836-18.285 4.391-27.332 4.207.547 3.93 1.734 7.313 2.008 9.047.641 2.742 1.555 5.668 1.465 6.398 0 .367-.551.914-1.371 1.465 10.145-3.293 20.566-4.48 31.262-3.566.457 0 .914-.09 1.461-.09 1.191 0 2.469-.094 3.656 0 .277 0 .551 0 .918.09zm-60.242-1.008l-1.645-1.645-1.187-.82c-6.766 9.32-15.082 17.184-25.047 23.582-1.645 1.188-3.289 2.375-5.027 3.473a11.22 11.22 0 0 0 1.461 1.461c4.938 5.117 9.051 9.781 10.332 11.426.18-.09.273-.184.457-.273.09 0 .09-.094.18-.094 3.84-3.473 8.047-6.672 12.523-9.504 7.223-5.301 14.902-9.414 23.129-12.43zm-2.285 171.105c9.965-4.113 20.477-6.035 31.445-5.668 4.848 0 9.508.367 14.078 1.281 2.195-3.84 4.207-7.953 5.941-12.434 1.008-2.465 1.738-4.203 2.559-5.484-4.113 1.738-8.316 3.293-12.613 4.391-10.238 4.293-21.023 6.305-32.359 6.031h-1.281l.094.09c.273.457-1.098 2.742-3.016 5.211a159.29 159.29 0 0 0-4.848 6.582zm-47.258-86.559c-1.004 1.098-2.379 2.195-4.48 3.75-5.941 4.477-10.418 9.047-13.984 14.441-.09.09-.09.18-.184.273-.184.273-.273.547-.367.82l-.09.094 7.313 7.035c7.039 8.594 9.691 19.195 9.781 29.891.367-.551.641-1.098 1.008-1.645.363-.641.73-1.465 1.094-2.105 1.098-2.648 2.195-4.934 3.109-6.762l1.098-2.102c-7.586-12.98-7.312-29.156-4.297-43.691zm14.261-48.35l-2.195-.914c-.184 0-.367-.09-.457-.09-.551-.09-1.098-.273-1.555-.457l-2.102-.73-2.195-.551-2.469-.73-1.734-.547c-.641 5.211-1.555 10.328-2.836 15.172-2.102 7.402-5.117 14.258-8.867 20.656a8.51 8.51 0 0 1 1.281.641c.73.457 1.461.82 2.195 1.277s1.551.734 2.285 1.191c.18.09.363.18.547.363 3.383 1.371 6.125 2.559 6.949 3.109.09-.273.273-.551.363-.73a5.17 5.17 0 0 1 1.098-1.648 83.76 83.76 0 0 1 12.066-19.742c2.742-4.387 5.758-8.5 9.051-12.246-1.555.09-4.023-.914-11.426-4.023zm0 0" fill="#c82777"/><path fill="#b62b70" d="M608.25 120.469c-6.762-16.543-17.094-29.613-30.437-38.57-1.008 23.398-5.121 46.797-12.066 69.191-7.039 22.574-16.91 44.148-32.086 62.52-13.711 16.543-30.895 29.613-49.637 40.031-19.469 10.785-41.043 17.641-62.434 23.398-9.133 2.469-18.363 4.477-27.695 6.035 3.016 2.922 5.668 6.305 7.953 9.871 1.734 2.742 2.469 5.484 2.469 8.316 3.105 4.477 5.848 9.141 8.043 14.168.73 1.734 1.277 3.473 1.828 5.207 3.656 1.465 7.859 2.105 11.883 2.742v-.09c-.457-1.098 0-1.187 2.469-.547 4.57 1.188 32.266.82 39.945-.551 82.453-14.258 141.961-73.484 148.176-147.43 1.648-19.832-.82-35.738-8.41-54.293zm0 0"/><path fill="#c82777" d="M309.156 466.789c-6.492-2.742-12.25-6.672-15.812-12.246-6.582 4.66-14.902 8.957-19.84 9.961-2.648.547-2.465.641 3.02 2.285 7.496 2.102 21.664 2.191 32.086.184.18-.094.363-.184.547-.184zM442.34 328.773l-1.371-.457c-1.277-.457-2.559-.73-3.84-1.098l-3.93-1.187-1.098-.273-2.465-.73-1.738-.551c-.547-.184-1.098-.547-1.738-.641-.09.277 0 .641 0 1.008v1.004l-.09 2.012-.367 4.023c-.273 2.648-.73 5.301-1.277 7.949l-1.555 6.855c-3.199 10.879-8.41 20.473-15.723 28.883-6.672 10.238-15.082 18.738-25.32 25.684-2.836 2.285-5.852 4.297-8.867 6.125v.363c.184 1.465.273 2.836.457 4.297.184 1.188.273 2.469.457 3.656.09.824.273 1.645.457 2.469l.547 2.012c.277.914.367 1.918.551 2.922 2.285-1.918 4.66-3.746 7.219-5.391 7.133-5.574 15.359-9.871 23.586-13.07-.184-.824-.273-1.371-.367-2.102-1.461-7.77-2.285-12.34-2.012-12.613.277-.184 1.648.547 10.879 7.035 3.199-9.32 8.504-19.012 14.902-26.414 1.828-2.832 3.289-4.387 5.391-6.945-5.758-3.746-8.684-6.125-8.41-6.398.184-.273 2.563.184 5.027.914 1.555.547 4.023-.73 7.68 0a90.83 90.83 0 0 1 4.207-4.203c-2.195-8.043-2.285-16.727-1.191-25.137zm0 0"/></svg>
""";
        }

        #endregion

        #region Helpers

        private static string XmlCommented(string input, bool shouldComment)
        {
            if (shouldComment)
            {
                return $"<!-- {input} -->";
            }

            return input;
        }

        private static string SlashCommented(string input, bool shouldComment)
        {
            if (shouldComment)
            {
                return $"// {input}";
            }

            return input;
        }

        #endregion

    }
}
