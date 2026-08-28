using CaseConverter;
using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Extensions;

namespace Spiderly.Shared.Helpers
{
    public static class NetAndAngularFilesGenerator
    {
        /// <summary>
        /// The entity source files <c>spiderly init</c> scaffolds into
        /// <c>Backend/{AppName}.Business/Entities/</c>.
        /// <para>
        /// Internal (not public) so a test can compile the REAL template entities rather than keep a copy that
        /// drifts, without that need widening a shipped package's public API — see the
        /// <c>InternalsVisibleTo</c> in <c>Spiderly.Shared.csproj</c>. They
        /// carry relational shapes no other fixture has — <c>UserExternalLogin</c> is the only
        /// <c>[Required]</c> navigation beside an explicit non-nullable foreign-key scalar in the whole
        /// codebase, and the reason an explicit-FK generator bug was caught at all was that
        /// <c>spiderly init</c> happens to scaffold it. A second copy of these in a test project is the same
        /// drift that once broke CI with <c>CS1729</c> (see <c>tests/e2e-fixtures/CLAUDE.md</c>).
        /// </para>
        /// </summary>
        internal static List<SpiderlyFile> GetEntityFiles(string appName) => new()
        {
            new SpiderlyFile { Name = "User.cs", Data = GetUserCsData(appName) },
            new SpiderlyFile { Name = "Role.cs", Data = GetRoleCsData(appName) },
            new SpiderlyFile { Name = "Permission.cs", Data = GetPermissionCsData(appName) },
            new SpiderlyFile { Name = "RolePermission.cs", Data = GetRolePermissionCsData(appName) },
            new SpiderlyFile { Name = "UserRole.cs", Data = GetUserRoleCsData(appName) },
            new SpiderlyFile { Name = "UserExternalLogin.cs", Data = GetUserExternalLoginCsData(appName) },
            new SpiderlyFile { Name = "OutboxMessage.cs", Data = GetOutboxMessageCsData(appName) },
        };

        /// <summary>
        /// Generates the starter project template for a Spiderly application, including both backend and frontend components.
        /// </summary>
        public static void Generate(string outputPath, ProjectGenerationOptions options)
        {
            string appName = options.AppName;
            string spiderlyVersion = options.SpiderlyVersion;
            bool isRunningFromNuget = options.IsRunningFromNuget;
            DbProviderCodes dbProvider = options.DbProvider;
            PackageManagerCodes packageManager = options.PackageManager;

            SpiderlyFolder appStructure = new SpiderlyFolder
            {
                Name = appName.ToKebabCase(),
                ChildFolders =
        {
            new SpiderlyFolder
            {
                Name = ".vscode",
                Files =
                {
                    new SpiderlyFile { Name = "extensions.json", Data = GetExtensionsJsonData(dbProvider) },
                    new SpiderlyFile { Name = "launch.json", Data = GetLaunchJsonData(appName, packageManager) },
                    new SpiderlyFile { Name = "settings.json", Data = GetSettingsJsonData() },
                    new SpiderlyFile { Name = "tasks.json", Data = GetTasksJsonData(appName) },
                }
            },
            new SpiderlyFolder
            {
                Name = "Frontend",
                ChildFolders =
                {
                    new SpiderlyFolder
                    {
                        Name = "tests",
                        ChildFolders =
                        {
                            new SpiderlyFolder
                            {
                                Name = "e2e",
                                ChildFolders =
                                {
                                    new SpiderlyFolder
                                    {
                                        Name = "specs",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "auth.spec.ts", Data = GetAuthSpecData() },
                                            new SpiderlyFile { Name = "user-crud.spec.ts", Data = GetUserCrudSpecData() },
                                        }
                                    },
                                    new SpiderlyFolder
                                    {
                                        Name = "page-objects",
                                        Files =
                                        {
                                            new SpiderlyFile { Name = "base-page.ts", Data = GetBasePageObjectData() },
                                            new SpiderlyFile { Name = "login-page.ts", Data = GetLoginPageObjectData() },
                                            new SpiderlyFile { Name = "user-list-page.ts", Data = GetUserListPageObjectData() },
                                        }
                                    }
                                },
                                Files =
                                {
                                    new SpiderlyFile { Name = ".gitignore", Data = GetE2EGitignoreData() }
                                }
                            }
                        }
                    },
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
                                            new SpiderlyFile { Name = "en.json", Data = GetTranslocoEnJsonCode() },
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
                                    new SpiderlyFile { Name = "tailwind.css", Data = GetTailwindCssData(isRunningFromNuget) },
                                }
                            },
                            new SpiderlyFolder
                            {
                                Name = "environments",
                                Files =
                                {
                                    new SpiderlyFile { Name = "environment.ts", Data = GetEnvironmentTsData(appName) },
                                    new SpiderlyFile { Name = "environment.prod.ts", Data = GetEnvironmentProdTsData(appName) },
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
                    new SpiderlyFile { Name = ".postcssrc.json", Data = GetPostCssRcJsonData() },
                    new SpiderlyFile { Name = ".prettierrc", Data = GetPrettierRcData() },
                    new SpiderlyFile { Name = "angular.json", Data = GetAngularJsonData(appName) },
                    new SpiderlyFile { Name = "package.json", Data = GetPackageJsonData(appName, spiderlyVersion, isRunningFromNuget) },
                    new SpiderlyFile { Name = "playwright.config.ts", Data = GetPlaywrightConfigData(packageManager) },
                    new SpiderlyFile { Name = "README.md", Data = GetFrontendREADMEData(appName, spiderlyVersion) },
                    new SpiderlyFile { Name = "tsconfig.app.json", Data = GetTsConfigAppJsonData() },
                    new SpiderlyFile { Name = "tsconfig.json", Data = GetTsConfigJsonData(isRunningFromNuget) },
                    new SpiderlyFile { Name = "tsconfig.spec.json", Data = GetTsConfigSpecJsonData() },
                    new SpiderlyFile { Name = "vercel.json", Data = GetVercelJsonData(appName) },
                }
            },
            new SpiderlyFolder
            {
                Name = "Backend",
                ChildFolders =
                {
                    new SpiderlyFolder
                    {
                        Name = ".config",
                        Files =
                        {
                            new SpiderlyFile { Name = "dotnet-tools.json", Data = GetDotnetToolsJsonData(spiderlyVersion) },
                        }
                    },
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
                                Name = "Entities",
                                Files = GetEntityFiles(appName)
                            },
                            new SpiderlyFolder
                            {
                                Name = "Enums",
                                Files =
                                {
                                    new SpiderlyFile { Name = "PermissionCodes.cs", Data = GetPermissionCodesCsData(appName) },
                                }
                            },
                            new SpiderlyFolder
                            {
                                Name = "Services",
                                Files =
                                {
                                    new SpiderlyFile { Name = $"AuthorizationService.cs", Data = GetAuthorizationServiceCsData(appName) },
                                    new SpiderlyFile { Name = $"SecurityService.cs", Data = GetSecurityServiceCsData(appName) },
                                    new SpiderlyFile { Name = $"OutboxMessageService.cs", Data = GetOutboxMessageServiceCsData(appName) },
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
                        Files = GetInfrastructureFiles(appName, spiderlyVersion, isRunningFromNuget, dbProvider)
                    },
                    new SpiderlyFolder
                    {
                        Name = $"{appName}.Migrations",
                        Files =
                        {
                            new SpiderlyFile { Name = $"{appName}.Migrations.csproj", Data = GetMigrationsCsProjData(appName, dbProvider) },
                            new SpiderlyFile { Name = "MigrationsDbContextFactory.cs", Data = GetMigrationsDbContextFactoryCsData(appName, dbProvider) },
                            new SpiderlyFile { Name = "Program.cs", Data = GetMigrationsProgramCsData() },
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
                                Name = "Translations",
                                Files =
                                {
                                    new SpiderlyFile { Name = "en.json", Data = GetSeedTranslationsJsonData() },
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
                                    new SpiderlyFile { Name = "SecurityController.cs", Data = GetSecurityControllerCsData(appName) },
                                    new SpiderlyFile { Name = "UserController.cs", Data = GetUserControllerCsData(appName) },
                                    new SpiderlyFile { Name = "OutboxMessageController.cs", Data = GetOutboxMessageControllerCsData(appName) },
                                }
                            },
                            new SpiderlyFolder
                            {
                                Name = "Extensions",
                                Files =
                                {
                                    new SpiderlyFile { Name = "AppServiceExtensions.cs", Data = GetAppServiceExtensionsCsData(appName) },
                                }
                            },
                        },
                        Files =
                        {
                            new SpiderlyFile { Name = "appsettings.json", Data = GetAppSettingsJsonData(appName) },
                            new SpiderlyFile { Name = "appsettings.Development.json", Data = GetAppSettingsDevelopmentJsonData(appName) },
                            new SpiderlyFile { Name = "appsettings.Development.local.example.json", Data = GetAppSettingsDevelopmentLocalExampleJsonData() },
                            new SpiderlyFile { Name = "appsettings.Production.json", Data = GetAppSettingsProductionJsonData() },
                            new SpiderlyFile { Name = $"{appName}.WebAPI.csproj", Data = GetWebAPICsProjData(appName, spiderlyVersion, isRunningFromNuget, dbProvider) },
                            new SpiderlyFile { Name = $"{appName}.WebAPI.csproj.user", Data = GetWebAPICsProjUserData() },
                            new SpiderlyFile { Name = "Program.cs", Data = GetProgramCsData(appName) },
                            new SpiderlyFile { Name = "Settings.cs", Data = GetWebAPISettingsCsData(appName) },
                            new SpiderlyFile { Name = "Startup.cs", Data = GetStartupCsData(appName, dbProvider) },
                        }
                    },
                },
                Files =
                {
                    new SpiderlyFile { Name = $"{appName}.sln", Data = GetNetSolutionData(appName) }
                }
            }
        },
                Files =
        {
            new SpiderlyFile { Name = ".gitignore", Data = GetGitIgnoreData() },
            new SpiderlyFile { Name = "README.md", Data = GetREADMEData(appName, spiderlyVersion) },
            new SpiderlyFile { Name = "CLAUDE.md", Data = GetClaudeMdData(appName) },
        }
            };

            GenerateProjectStructure(appStructure, outputPath);
        }

        private static void GenerateProjectStructure(SpiderlyFolder appStructure, string path)
        {
            string newPath = GenerateFolder(appStructure, path);

            foreach (SpiderlyFile file in appStructure.Files)
                GenerateFile(file, newPath);

            foreach (SpiderlyFolder folder in appStructure.ChildFolders)
                GenerateProjectStructure(folder, newPath);
        }

        private static string GenerateFolder(SpiderlyFolder appStructure, string path)
        {
            if (string.IsNullOrEmpty(appStructure.Name))
                return path;

            Helper.MakeFolder(path, appStructure.Name);

            return Path.Combine(path, appStructure.Name);
        }

        private static void GenerateFile(SpiderlyFile file, string path)
        {
            string filePath = Path.Combine(path, file.Name);

            Helper.FileOverrideCheck(filePath);

            Helper.WriteToFile(file.Data, filePath);
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
import { {{entityName}}MainUIForm, {{entityName}}SaveBody } from 'src/app/business/entities/entities.generated';
import { {{entityName}}BaseDetailsComponent } from 'src/app/business/components/base-details.generated';
import { BaseFormComponent, SpiderlyFormGroup, SpiderlyMessageService, BaseFormService, SpiderlyPanelsModule, SpiderlyControlsModule } from 'spiderly';

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
export class {{entityName}}DetailsComponent extends BaseFormComponent<{{entityName}}MainUIForm, {{entityName}}SaveBody> implements OnInit {
    override mainUIFormClass = {{entityName}}MainUIForm;
    override saveBodyClass = {{entityName}}SaveBody;

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
    [parentFormGroup]="parentFormGroup" 
    (onSave)="onSave()"
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
import { {{entityName}} } from 'src/app/business/entities/entities.generated';
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
    delete{{entityName}}ListObservableMethod = this.apiService.delete{{entityName}}List;

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
    [deleteListFromTableObservableMethod]="delete{{entityName}}ListObservableMethod"
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
import { {{entityName}} } from 'src/app/business/entities/entities.generated';
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
    [deleteListFromTableObservableMethod]="deleteRoleListObservableMethod"
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
import { Column, SpiderlyDataTableComponent } from 'spiderly';
import { Role } from 'src/app/business/entities/entities.generated';

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
    deleteRoleListObservableMethod = this.apiService.deleteRoleList;

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
    [deleteListFromTableObservableMethod]="deleteUserListObservableMethod"
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
import { User } from 'src/app/business/entities/entities.generated';
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
    deleteUserListObservableMethod = this.apiService.deleteUserList;

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
        🎉 Congratulations! Your app is running. Check out the <a href="https://www.spiderly.dev/docs" target="_blank" rel="noopener noreferrer">documentation</a> to learn more.
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
      <h1 class="remove-h-css">PRIVACY POLICY</h1>
      <div class="bold-header-separator"></div>
    </div>

    <p style="margin-bottom: 20px;">This page is a placeholder generated by Spiderly. Replace the content below with your own privacy policy, since the data you collect and the laws that apply (e.g. GDPR, CCPA) depend on your specific application and aren't something we can predict for you.</p>

    <spiderly-panel [isFirstMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="1. DATA WE COLLECT AND HOW WE USE IT"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>1.1.</strong> Describe here what personal data <strong>{{companyName}}</strong> collects (e.g. account details, usage data) and why.</p>
        <p><strong>1.2.</strong> Describe here how that data is stored, shared, and protected, and how users can request access to or deletion of their data.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isLastMultiplePanel]="true" [showPanelHeader]="false">
        <panel-body [normalBottomPadding]="true">
          For any questions or requests regarding your privacy, contact us at <strong>your-email&commat;example.com</strong>.
          <p>Thank you for using <strong>{{companyName}}</strong>!</p>
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
      <h1 class="remove-h-css">TERMS OF USE</h1>
      <div class="bold-header-separator"></div>
    </div>

    <p style="margin-bottom: 20px;">This page is a placeholder generated by Spiderly. Replace the content below with your own terms of use, since the rules that apply to your application aren't something we can predict for you.</p>

    <spiderly-panel [isFirstMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="1. GENERAL TERMS"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>1.1.</strong> Describe here the rules and obligations that apply when using <strong>{{companyName}}</strong>.</p>
        <p><strong>1.2.</strong> Describe here any limitations of liability and how disputes will be resolved.</p>
      </panel-body>
    </spiderly-panel>

    <spiderly-panel [isLastMultiplePanel]="true" [showPanelHeader]="false">
        <panel-body [normalBottomPadding]="true">
          For any questions, contact us at <strong>your-email&commat;example.com</strong>.
          <p>Thank you for using <strong>{{companyName}}</strong>!</p>
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
];

export const routes: Routes = [
    {
        path: '', 
        component: LayoutComponent,
        children: layoutRoutes,
    },
    {
        path: 'login',
        loadComponent: () => import('spiderly').then(c => c.SpiderlyLoginComponent),
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
    [rejectLabel]="t('Cancel')" 
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
import { AuthServiceBase, authInitializer, ConfigServiceBase, httpLoadingInterceptor, jsonHttpInterceptor, jwtInterceptor, LayoutServiceBase, SpiderlyErrorHandler, SpiderlyTranslocoLoader, unauthorizedInterceptor, ValidatorAbstractService } from 'spiderly';
import { environment } from 'src/environments/environment';
import { ValidatorService } from './business/services/validators/validators';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { ConfigService } from './business/services/config.service';
import { LayoutService } from './business/services/layout/layout.service';
import { provideTransloco } from '@jsverse/transloco';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { provideMarkdown } from 'ngx-markdown';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideAnimationsAsync(),
    provideHttpClient(withFetch()),
    provideMarkdown(),
    provideTransloco({
      config: {
        availableLangs: ['en'],
        defaultLang: 'en',
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
      provide: ValidatorAbstractService,
      useClass: ValidatorService,
    },
    {
      provide: AuthServiceBase,
      useExisting: AuthService
    },
    {
      provide: ConfigServiceBase,
      useExisting: ConfigService
    },
    {
      provide: LayoutServiceBase,
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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace {{appName}}.Shared.FluentValidation
{
    public class TranslatePropertiesConfiguration : IConfigureOptions<MvcOptions>
    {
        private readonly IStringLocalizer _localizer;

        public TranslatePropertiesConfiguration(IStringLocalizer localizer)
        {
            _localizer = localizer;
        }

        public void Configure(MvcOptions options)
        {
            ValidatorOptions.Global.DisplayNameResolver = (type, memberInfo, expression) =>
            {
                LocalizedString result = _localizer[memberInfo.Name];
                return result.ResourceNotFound ? null : result.Value;
            };
        }
    }
}

""";
        }

        private static string GetSeedTranslationsJsonData()
        {
            return """
{
  "And": "And",
  "AuthenticationEmailDoesNotExistException": "An account with the entered email address does not exist. Please check the email address or create a new account.",
  "AuthenticationIncorectPasswordException": "Incorrect password. Please try again or reset your password if you've forgotten it.",
  "BirthDate": "Birth Date",
  "Code": "Code",
  "ConcurrencyException": "This record has been modified or deleted by another user.",
  "CreatedAt": "Created At",
  "Description": "Description",
  "DisabledAccountException": "Your account is disabled, please contact the administrator.",
  "Discount": "Discount",
  "EmailAccountVerificationTitle": "Account verification",
  "EmailBody": "Email Body",
  "EmailSendError": "An error occurred while sending the email, our team has been informed and will fix it as soon as possible. Thank you for your patience.",
  "EntityDoesNotExistInDatabase": "The record you're looking for doesn't exist in database.",
  "EntityDoesNotExistInDatabaseForDeleteRequest": "Your deletion request couldn't be completed as the entity doesn't exist in our database. Maybe it's already deleted.",
  "ExpiredRefreshTokenException": "Your session has expired, please login again.",
  "ExpiredVerificationCodeException": "Your verification code has expired. Please request a new code to continue.",
  "ExternalProviderNotConfiguredException": "Signing in with this provider is currently unavailable. Please try another sign-in method.",
  "FileContainsActiveContent": "The file contains disallowed active content (scripts or event handlers).",
  "FileContentDoesNotMatchType": "File content does not match declared type '{0}'.",
  "FileIsEmpty": "File is empty.",
  "FileSizeExceeded": "File size must not exceed {0} MB.",
  "FileTypeNotAllowed": "File type '{0}' is not allowed.",
  "GlobalError": "An error occurred in the system, our team has been informed and will fix it as soon as possible. Thank you for your patience.",
  "Id": "Id",
  "ImageHeightMustBeExact": "Image height must be exactly {0}px (current: {1}px).",
  "ImageWidthMustBeExact": "Image width must be exactly {0}px (current: {1}px).",
  "IsDisabled": "Is Disabled",
  "LatestVerificationCodeException": "Please use the most recent verification code, as multiple codes were sent.",
  "LogoImage": "Logo Image",
  "ModifiedAt": "Modified At",
  "Name": "Name",
  "OnlyThirdPartyAccountButTriedToRegisterOrLoginException": "Your account already exists with third-party (eg. Google) authentication. If you want to set up a password as well, please use the 'Forgot password?' option to reset it or log in to your profile and add a password.",
  "OrderNumber": "Order Number",
  "PartnerDisplayName": "Partner",
  "PartnerId": "Partner",
  "PartnerList": "Partners",
  "Password": "Password",
  "Points": "Points",
  "PrimaryColor": "Primary Color",
  "ResetPasswordEmailDoesNotExistException": "An account with the entered email address does not exist. Please check the email address or create a new account.",
  "RoleList": "Roles",
  "SameEmailAlreadyExistsException": "An account with this email address already exists.",
  "Slug": "Slug",
  "Title": "Title",
  "TwoDifferentIpAddressesRefreshException": "You can't use the application with two different IP addresses at the same time, please login again.",
  "UnauthorizedAccessExceptionMessage": "You don't have the necessary rights to perform the operation.",
  "UserDisplayName": "User",
  "UserId": "User",
  "UserList": "Users",
  "ValidFrom": "Valid From",
  "ValidTo": "Valid To",
  "VerificationCodeDevelopmentMode": "Your verification code: {0}\n\n(Shown here because you're in development environment without emailing set up)",
  "Version": "Version"
}
""";
        }

        private static string GetPermissionCodesCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes;

namespace {{appName}}.Business.Enums
{
    [SpiderlyEnum]
    public static partial class PermissionCodes
    {

    }
}
""";
        }

        private static string GetUserCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [Index(nameof(Email), IsUnique = true)]
    [SpiderlyEntity]
    public class User : BusinessObject<long>, IUser
    {
        [UIDoNotGenerate]
        [DisplayName]
        [Email]
        [StringLength(70, MinimumLength = 5)]
        [Required]
        public string Email { get; set; } = null!;

        public bool? IsDisabled { get; set; }

        public virtual List<Role> Roles { get; } = new(); // M2M
        IReadOnlyCollection<IRole> ISecurityPrincipal.Roles => Roles; // Roles moved to the principal base

        [UIDoNotGenerate]
        public virtual List<UserExternalLogin> ExternalLogins { get; } = new();
    }
}
""";
        }

        private static string GetUserExternalLoginCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    // Links a user to an external identity-provider login, keyed by the provider's stable subject.
    // [UIDoNotGenerate]: auth plumbing, not admin-editable. A non-nullable FK (UserId) requires
    // [Required] on the navigation, otherwise the SPIDERLY006 diagnostic fires.
    [Index(nameof(Provider), nameof(ProviderKey), IsUnique = true)]
    [UIDoNotGenerate]
    [SpiderlyEntity]
    public class UserExternalLogin : BusinessObject<long>, IUserExternalLogin
    {
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Provider { get; set; } = null!;

        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string ProviderKey { get; set; } = null!;

        public long UserId { get; set; }

        [Required]
        [WithMany(nameof(User.ExternalLogins))]
        public virtual User User { get; set; } = null!;
    }
}
""";
        }

        private static string GetRoleCsData(string appName)
        {
            return $$"""
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [SpiderlyEntity]
    public class Role : BusinessObject<int>, IRole
    {
        [DisplayName]
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
        public virtual List<User> Users { get; } = new(); // M2M
        IReadOnlyCollection<IUser> IRole.Users => Users;

        [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
        public virtual List<Permission> Permissions { get; } = new(); // M2M
        IReadOnlyCollection<IPermission> IRole.Permissions => Permissions;
    }
}
""";
        }

        private static string GetPermissionCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [Index(nameof(Code), IsUnique = true)]
    [UIDoNotGenerate]
    [SpiderlyEntity]
    public class Permission : ReadonlyObject<int>, IPermission
    {
        [DisplayName]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Code { get; set; } = null!;

        public virtual List<Role> Roles { get; } = new(); // M2M
        IReadOnlyCollection<IRole> IPermission.Roles => Roles;
    }
}

""";
        }

        private static string GetRolePermissionCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;

namespace {{appName}}.Business.Entities
{
    [M2M]
    [SpiderlyEntity]
    public class RolePermission
    {
        [M2MWithMany(nameof(Role.Permissions))]
        public virtual Role Role { get; set; } = null!;

        [M2MWithMany(nameof(Permission.Roles))]
        public virtual Permission Permission { get; set; } = null!;
    }
}

""";
        }

        private static string GetUserRoleCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;

namespace {{appName}}.Business.Entities
{
    [M2M]
    [SpiderlyEntity]
    public class UserRole
    {
        [M2MWithMany(nameof(User.Roles))]
        public virtual User User { get; set; } = null!;

        [M2MWithMany(nameof(Role.Users))]
        public virtual Role Role { get; set; } = null!;
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
using Spiderly.Security.DTO;
using Spiderly.Shared.Extensions;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;

namespace {{appName}}.WebAPI.Controllers
{
    [SpiderlyController]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class SecurityController : SecurityBaseController<User, Role, UserExternalLogin>
    {
        private readonly ILogger<SecurityController> _logger;
        private readonly SecurityService<User, UserExternalLogin> _securityService;
        private readonly IApplicationDbContext _context;

        public SecurityController(
            ILogger<SecurityController> logger,
            SecurityService<User, UserExternalLogin> securityService,
            IJwtAuthManager jwtAuthManagerService,
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            AuthorizationService authorizationService
        )
            : base(securityService, jwtAuthManagerService, context, authenticationService, authorizationService)
        {
            _logger = logger;
            _securityService = securityService;
            _context = context;
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
using Spiderly.Shared.DTO;
using Spiderly.Security.Services;
using Microsoft.Extensions.Localization;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Entities;

namespace {{appName}}.WebAPI.Controllers
{
    [SpiderlyController]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class UserController : UserBaseController
    {
        private readonly UserServiceGenerated _userService;
        private readonly AuthenticationService _authenticationService;

        public UserController(
            IApplicationDbContext context,
            IServiceProvider serviceProvider,
            UserServiceGenerated userService,
            AuthenticationService authenticationService,
            IStringLocalizer localizer
        )
            : base(context, serviceProvider, localizer)
        {
            _userService = userService;
            _authenticationService = authenticationService;
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserDTO> GetCurrentUser()
        {
            long userId = _authenticationService.GetCurrentUserId();
            return await _userService.GetUserDTO(userId); // Current user reading own profile; admin reads are gated by [AuthGuard("ReadUser")] on the generated endpoint
        }

    }
}

""";
        }

        private static string GetOutboxMessageCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    /// <summary>
    /// Transactional-outbox row, written inside the same transaction as the entity change it represents
    /// (via Spiderly's IOutbox). The recurring OutboxDispatcherJob sweeps pending rows (DispatchedAt IS NULL)
    /// and routes each to the IOutboxHandler whose Code matches HandlerCode. Payload carries semantic intent
    /// (e.g. ids), not rendered content.
    ///
    /// Surfaced as a read-only Spiderly admin page; operators act through the OutboxMessageController
    /// Retry / Dismiss endpoints, and OutboxMessageService default-filters the list to pending rows.
    /// </summary>
    [SpiderlyEntity]
    public class OutboxMessage : BusinessObject<long>, IOutboxMessage
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string HandlerCode { get; set; } = null!;

        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Payload { get; set; } = null!;

        public DateTime? DispatchedAt { get; set; }

        [Required]
        public int AttemptCount { get; set; }

        public DateTime? LastAttemptedAt { get; set; }

        [StringLength(2000)]
        public string? LastError { get; set; }

        public DateTime? NextAttemptAt { get; set; }

        public long? DismissedByUserId { get; set; }
    }
}
""";
        }

        private static string GetOutboxMessageServiceCsData(string appName)
        {
            return $$"""
using {{appName}}.Business.Entities;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Classes;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Extensions;

namespace {{appName}}.Business.Services
{
    /// <summary>
    /// Default-filters the outbox admin table to pending rows (DispatchedAt IS NULL) so the page surfaces
    /// stuck/failing events without an explicit filter. To inspect dispatched history, add an explicit
    /// DispatchedAt filter in the table UI — its presence in the FilterDTO suppresses this default.
    /// </summary>
    [SpiderlyService]
    public class OutboxMessageService : OutboxMessageServiceGenerated
    {
        public OutboxMessageService(EntityServiceDependencies deps) : base(deps) { }

        public override async Task<PaginatedResult<OutboxMessage>> GetPaginatedOutboxMessageResult(
            FilterDTO filterDTO, IQueryable<OutboxMessage> query)
        {
            bool hasExplicitDispatchedFilter = filterDTO.Filters != null
                && filterDTO.Filters.ContainsKey(nameof(OutboxMessage.DispatchedAt).FirstCharToLower());

            if (!hasExplicitDispatchedFilter)
                query = query.Where(x => x.DispatchedAt == null);

            return await base.GetPaginatedOutboxMessageResult(filterDTO, query);
        }
    }
}
""";
        }

        private static string GetOutboxMessageControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Enums;
using {{appName}}.Business.Services;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Interfaces;

namespace {{appName}}.WebAPI.Controllers
{
    /// <summary>
    /// Admin row actions on the OutboxMessage table beyond the generated read:
    /// <list type="bullet">
    /// <item><description><c>Retry</c> — clears AttemptCount + LastError + NextAttemptAt so the next OutboxDispatcherJob
    /// sweep picks the row up (e.g. after an external dependency that was down recovers).</description></item>
    /// <item><description><c>Dismiss</c> — marks the row handled out-of-band (sets DispatchedAt +
    /// DismissedByUserId); the sweep skips it.</description></item>
    /// </list>
    /// Both require the generated UpdateOutboxMessage permission. Insert/Delete are intentionally never granted —
    /// rows are produced by IOutbox and consumed by OutboxDispatcherJob.
    /// </summary>
    [UIDoNotGenerate]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    [SpiderlyController]
    public class OutboxMessageController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthorizationService _authorizationService;
        private readonly AuthenticationService _authenticationService;

        public OutboxMessageController(
            IApplicationDbContext context,
            AuthorizationService authorizationService,
            AuthenticationService authenticationService)
        {
            _context = context;
            _authorizationService = authorizationService;
            _authenticationService = authenticationService;
        }

        [HttpPost]
        [AuthGuard]
        public async Task<IActionResult> Retry(long id)
        {
            await _authorizationService.AuthorizeAndThrowAsync(PermissionCodes.UpdateOutboxMessage);

            OutboxMessage? row = await _context.DbSet<OutboxMessage>()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
                throw new BusinessException("Message not found.");

            // Fail loud rather than silently re-emit a side effect that already happened.
            if (row.DispatchedAt != null)
                throw new BusinessException("Message already dispatched or dismissed — cannot retry.");

            row.AttemptCount = 0;
            row.LastError = null;
            row.LastAttemptedAt = null;
            // Clear the backoff/dead-letter gate so the next sweep picks the row up immediately.
            row.NextAttemptAt = null;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        [AuthGuard]
        public async Task<IActionResult> Dismiss(long id)
        {
            await _authorizationService.AuthorizeAndThrowAsync(PermissionCodes.UpdateOutboxMessage);

            OutboxMessage? row = await _context.DbSet<OutboxMessage>()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
                throw new BusinessException("Message not found.");

            // Idempotent: dismissing an already-dispatched/dismissed row is a no-op.
            if (row.DispatchedAt != null)
                return Ok();

            long currentUserId = _authenticationService.GetCurrentUserId();
            row.DispatchedAt = DateTime.UtcNow;
            row.DismissedByUserId = currentUserId;
            await _context.SaveChangesAsync();

            return Ok();
        }
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

            SeedData(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
""";
        }

        private static string GetInfrastructureApplicationDbContextSeedDataData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using {{appName}}.Business.Entities;

namespace {{appName}}.Infrastructure
{
    // Demo seed data lives in its own partial file so it can be replaced (e.g. by the e2e fixture overlay)
    // without copying the DbContext plumbing (constructor / OnModelCreating), which is what causes template drift.
    public partial class {{appName}}ApplicationDbContext
    {
        private static void SeedData(ModelBuilder modelBuilder)
        {
            Permission[] permissions =
            [
                new Permission { Id = 1, Name = "View users", Code = "ReadUser" },
                new Permission { Id = 2, Name = "Edit existing users", Code = "UpdateUser" },
                new Permission { Id = 3, Name = "Add new users", Code = "InsertUser" },
                new Permission { Id = 4, Name = "Delete users", Code = "DeleteUser" },
                new Permission { Id = 5, Name = "View roles", Code = "ReadRole" },
                new Permission { Id = 6, Name = "Edit existing roles", Code = "UpdateRole" },
                new Permission { Id = 7, Name = "Add new roles", Code = "InsertRole" },
                new Permission { Id = 8, Name = "Delete roles", Code = "DeleteRole" }
            ];

            modelBuilder.Entity<Permission>().HasData(permissions);

            DateTime seedDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<Role>().HasData(new Role
            {
                Id = 1,
                Name = "Admin",
                CreatedAt = seedDate,
                ModifiedAt = seedDate,
            });

            modelBuilder.Entity<Role>()
                .HasMany(r => r.Permissions)
                .WithMany(p => p.Roles)
                .UsingEntity(j => j.HasData(
                    new { RoleId = 1, PermissionId = 1 },
                    new { RoleId = 1, PermissionId = 2 },
                    new { RoleId = 1, PermissionId = 3 },
                    new { RoleId = 1, PermissionId = 4 },
                    new { RoleId = 1, PermissionId = 5 },
                    new { RoleId = 1, PermissionId = 6 },
                    new { RoleId = 1, PermissionId = 7 },
                    new { RoleId = 1, PermissionId = 8 }
                ));
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
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Migrations", "{{appName}}.Migrations\{{appName}}.Migrations.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
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
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.Build.0 = Release|Any CPU
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


        private static string GetStartupCsData(string appName, DbProviderCodes dbProvider)
        {
            return $$"""
using Hangfire;
using Hangfire.Dashboard;
using Serilog;
using Spiderly.Shared.Emailing;
using Spiderly.Security.Extensions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Services;
using {{appName}}.WebAPI.Extensions;
using {{appName}}.Infrastructure;
using {{appName}}.Business.Entities;

public class Startup
{
    public IConfiguration Configuration { get; }

    // Composition-time-only Spiderly.Shared settings (connection string, CORS frontend URL). Runtime
    // services consume the focused *Options classes (e.g. JwtOptions) via the .NET Options pattern,
    // registered inside AddSpiderly(Configuration, ...).
    private readonly Spiderly.Shared.Settings _spiderlySharedSettings;

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
        _spiderlySharedSettings = configuration.GetSection(Spiderly.Shared.Settings.ConfigurationSection)
            .Get<Spiderly.Shared.Settings>() ?? new();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<{{appName}}.Business.Settings>(Configuration.GetSection({{appName}}.Business.Settings.ConfigurationSection));
        services.Configure<{{appName}}.WebAPI.Settings>(Configuration.GetSection({{appName}}.WebAPI.Settings.ConfigurationSection));

        string spiderlyConnectionString = _spiderlySharedSettings.ConnectionString;
        services.AddHangfire(config =>
            config.{{(dbProvider == DbProviderCodes.SQLServer ? "UseSqlServerStorage(spiderlyConnectionString)" : "UseHangfirePostgreSqlStorage(spiderlyConnectionString)")}}
        );
        services.AddHangfireServer();

        services.AddHealthChecks()
            .AddDbContextCheck<{{appName}}ApplicationDbContext>();

        services.AddSpiderly<{{appName}}ApplicationDbContext>(Configuration, spiderly =>
        {
            spiderly.{{(dbProvider == DbProviderCodes.SQLServer ? "UseSQLServer()" : "UsePostgreSQL()")}};
            spiderly.UseCulture("en");
            spiderly.UseTranslations();
            // One call wires the co-required auth core (current-user/login/JWT, the User principal, and the
            // [AuthGuard(...)] handler forwarded to AuthorizationService) and enables authentication. Add API keys
            // with .AddApiKeys<ApiKey>() if the app exposes them.
            spiderly.AddSecurity<User, UserExternalLogin, {{appName}}.Business.Services.AuthorizationService>();
            spiderly.AddTokenStorage();
            spiderly.AddExcel();
            spiderly.AddEmailing<EmailingService>();
            // File storage is selected per blob property via [DiskStorage], [S3PublicStorage],
            // [S3PrivateStorage], or a custom StorageAttribute subclass. DiskStorageService is
            // pre-registered in AppServiceExtensions; opt in to S3 adapters there when needed.
            // Scaffolded apps are <Nullable>enable</Nullable> and have no deployed API consumers yet, so
            // the spec reflects C# nullability from day one: a required member is `required` and
            // non-nullable in the generated client instead of optional-and-nullable. An existing app
            // adds this as a deliberate contract tightening, on its own deploy.
            spiderly.AddSwagger(options => options.SupportNonNullableReferenceTypes());
            spiderly.AddRateLimiting();
            spiderly.AddForwardedHeaders();
            spiderly.AddOutbox<OutboxMessage>();
            spiderly.AddNotifications(_ => { });
        });

        services.AddAppServices();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.SpiderlyConfigureForwardedHeaders();

        app.UseCors(builder =>
        {
            builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithOrigins(new[] { _spiderlySharedSettings.FrontendUrl })
                .WithExposedHeaders("Content-Disposition", RequestIdMiddleware.HeaderName); // Content-Disposition: Excel file name; X-Request-Id: cross-origin JS can read the correlation id
        });

        app.UseMiddleware<RequestIdMiddleware>();

        app.UseSerilogRequestLogging();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.SpiderlyConfigureSwagger();
        }

        if (env.IsProduction())
        {
            app.UseHttpsRedirection();
        }

        app.SpiderlyConfigureLocalization();

        app.SpiderlyConfigureExceptionHandling();

        app.UseRouting();

        // After UseRouting so [IgnoreCsrf] endpoint metadata is resolvable. Global + opt-out by design:
        // a cookie-authenticated write with no X-CSRF header is rejected here, for every endpoint.
        app.UseSpiderlyCsrf();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseRateLimiter();

        app.SpiderlyUseHangfirePrincipalFilter();

        app.SpiderlyUseOutboxRecurringJob<OutboxMessage>();

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new LocalRequestsOnlyAuthorizationFilter() },
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health");
            endpoints.MapControllers();
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
    public class Settings
    {
        public const string ConfigurationSection = "AppSettings:{{appName}}.WebAPI";
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
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .UseSerilog((context, configuration) =>
                {
                    configuration.ReadFrom.Configuration(context.Configuration);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
""";
        }

        private static string GetWebAPICsProjData(string appName, string spiderlyVersion, bool isRunningFromNuget, DbProviderCodes dbProvider)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk.Web">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<GenerateDocumentationFile>true</GenerateDocumentationFile>
		<NoWarn>1591</NoWarn>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.1" />
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.14" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.1">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="9.0.1" />
		<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="9.0.1" />
		{{(dbProvider == DbProviderCodes.SQLServer ? "<PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"9.0.1\" />" : "<PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.1\" />")}}
		<PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
		{{(dbProvider == DbProviderCodes.SQLServer ? "<PackageReference Include=\"Hangfire.SqlServer\" Version=\"1.8.*\" />" : "<PackageReference Include=\"Hangfire.PostgreSql\" Version=\"1.20.*\" />")}}
		<PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.7.0" />
		<PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.19.5" />
        <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
        <PackageReference Include="Serilog.Extensions.Hosting" Version="9.0.0" />
        <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
        <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
		<PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.7.0" />
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
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
    <DebuggerFlavor>ProjectDebugger</DebuggerFlavor>
  </PropertyGroup>
</Project>
""";
        }

        private static string GetAppSettingsJsonData(string appName)
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "Serilog": {
    "Using": [
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
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  },
  "AppSettings": {
    "AllowedHosts": "*",
    "{{appName}}.WebAPI": {
    },
    "{{appName}}.Business": {
    },
    "Spiderly.Infrastructure": {
    },
    "Spiderly.Shared": {
      "ApplicationName": "{{appName}}",
      "ExternalProviders": [],
      "EmailSender": {
        "email": "youremail@gmail.com",
        "name": ""
      }
    },
    "Spiderly.Security": {
    }
  }
}
""";
        }

        private static string GetAppSettingsDevelopmentJsonData(string appName)
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "AppSettings": {
    "{{appName}}.WebAPI": {
    },
    "{{appName}}.Business": {
    },
    "Spiderly.Infrastructure": {
    },
    "Spiderly.Shared": {
      "FrontendUrl": "http://localhost:4200",
      "CookieDomain": "localhost"
    },
    "Spiderly.Security": {
    }
  }
}
""";
        }

        private static string GetAppSettingsDevelopmentLocalExampleJsonData()
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "_comment": "Copy this file to appsettings.Development.local.json and fill in real dev secrets. The .local.json file is gitignored.",
  "AppSettings": {
    "Spiderly.Shared": {
      "ConnectionString": "",
      "JwtKey": ""
    }
  }
}
""";
        }

        private static string GetAppSettingsProductionJsonData()
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "AppSettings": {
    "Spiderly.Shared": {
      // 2 = Cloudflare -> reverse-proxy -> backend; drop to 1 if only one proxy hop. See the Spiderly 'deployment' skill for TrustedProxyNetworks.
      "ForwardLimit": 2
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
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:44388",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}

""";
        }

        private static string GetAppServiceExtensionsCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spiderly.Security;
using Spiderly.Security.Extensions;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Services;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Services;
using {{appName}}.Shared.FluentValidation;

namespace {{appName}}.WebAPI.Extensions
{
    public static class AppServiceExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            #region Spiderly

            // The auth core (current-user/login/JWT, the User principal, and the [AuthGuard(...)] handler forwarded
            // to AuthorizationService) is wired by spiderly.AddSecurity<...>() in Startup; AuthorizationService and its
            // generated base are registered by the generated AddEntityServices(). Only app-specific extras remain here.
            // Narrow current-user slice so domain services depend on the abstraction, not the full HTTP/auth-bound
            // AuthenticationService (registered by AddSecurity). Stateless forwarder.
            services.AddTransient<Spiderly.Shared.Interfaces.ICurrentUserAccessor>(sp => sp.GetRequiredService<AuthenticationService>());

            services.AddSingleton<IConfigureOptions<MvcOptions>, TranslatePropertiesConfiguration>();

            // DiskStorageService is the dev default; add S3PublicStorageService / S3PrivateStorageService here when adopting S3.
            services.AddSingleton<DiskStorageService>();

            #endregion

            #region Business

            services.AddTransient<SecurityService<User, UserExternalLogin>>();
            // AddEntityServices() also registers AuthorizationService and its generated base (the latter forwarded to
            // it) — the generated EntityServiceDependencies injects AuthorizationServiceGenerated.
            services.AddEntityServices();

            #endregion

            return services;
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
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
{{XmlCommented($$"""
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Shared\Spiderly.Shared.csproj" />
""", isRunningFromNuget)}}
  </ItemGroup>

  <ItemGroup>
  </ItemGroup>

	<ItemGroup>
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Shared" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

    <ItemGroup>
	    <None Update="Translations\*.json" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>

</Project>

""";
        }

        private static List<SpiderlyFile> GetInfrastructureFiles(string appName, string spiderlyVersion, bool isRunningFromNuget, DbProviderCodes dbProvider)
        {
            List<SpiderlyFile> files = new()
        {
            new SpiderlyFile { Name = $"{appName}ApplicationDbContext.cs", Data = GetInfrastructureApplicationDbContextData(appName) },
            new SpiderlyFile { Name = $"{appName}ApplicationDbContext.SeedData.cs", Data = GetInfrastructureApplicationDbContextSeedDataData(appName) },
            new SpiderlyFile { Name = $"{appName}.Infrastructure.csproj", Data = GetInfrastructureCsProjData(appName, spiderlyVersion, isRunningFromNuget, dbProvider) },
        };

            if (dbProvider == DbProviderCodes.PostgreSQL)
            {
                files.Add(new SpiderlyFile { Name = "HangfireStorageExtensions.cs", Data = GetHangfireStorageExtensionsCsData(appName) });
            }

            return files;
        }

        private static string GetHangfireStorageExtensionsCsData(string appName)
        {
            return $$"""
using Hangfire;
using Hangfire.PostgreSql;

namespace {{appName}}.Infrastructure
{
    public static class HangfireStorageExtensions
    {
        public static IGlobalConfiguration UseHangfirePostgreSqlStorage(this IGlobalConfiguration config, string connectionString)
        {
            return config.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionString)
            );
        }
    }
}
""";
        }

        private static string GetInfrastructureCsProjData(string appName, string version, bool isRunningFromNuget, DbProviderCodes dbProvider)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<GenerateDocumentationFile>true</GenerateDocumentationFile>
		<NoWarn>1591</NoWarn>
	</PropertyGroup>

	<ItemGroup>
{{(dbProvider == DbProviderCodes.PostgreSQL ? "\t\t<PackageReference Include=\"Hangfire.PostgreSql\" Version=\"1.20.*\" />" : "")}}
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
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Infrastructure" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetMigrationsCsProjData(string appName, DbProviderCodes dbProvider)
        {
            return $$"""
<!--
	Lightweight startup project for EF Core design-time tools (migrations).
	Using this instead of WebAPI as the startup project allows running
	"spiderly add-migration" and "spiderly update-database" while the
	backend is running — WebAPI's DLLs are locked by the running process,
	but this project builds to its own output directory.
-->
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<OutputType>Exe</OutputType>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.1">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="9.0.1" />
		{{(dbProvider == DbProviderCodes.SQLServer ? "<PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"9.0.1\" />" : "<PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.1\" />")}}
		<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.1" />
		<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="9.0.1" />
	</ItemGroup>

	<ItemGroup>
		<None Include="..\{{appName}}.WebAPI\appsettings.json" Link="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
		<None Include="..\{{appName}}.WebAPI\appsettings.*.json" Link="%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
	</ItemGroup>

</Project>
""";
        }

        private static string GetMigrationsDbContextFactoryCsData(string appName, DbProviderCodes dbProvider)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using {{appName}}.Infrastructure;

namespace {{appName}}.Migrations
{
    public class MigrationsDbContextFactory : IDesignTimeDbContextFactory<{{appName}}ApplicationDbContext>
    {
        public {{appName}}ApplicationDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddJsonFile("appsettings.Development.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string connectionString = configuration[$"{Spiderly.Shared.Settings.ConfigurationSection}:ConnectionString"];

            DbContextOptionsBuilder<{{appName}}ApplicationDbContext> optionsBuilder = new();
            optionsBuilder.UseLazyLoadingProxies();
            {{(dbProvider == DbProviderCodes.SQLServer ? "optionsBuilder.UseSqlServer(connectionString);" : "optionsBuilder.UseNpgsql(connectionString);")}}

            return new {{appName}}ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
""";
        }

        private static string GetMigrationsProgramCsData()
        {
            return $$"""
return;
""";
        }

        private static string GetBusinessSettingsCsData(string appName)
        {
            return $$"""
namespace {{appName}}.Business
{
    public class Settings
    {
        public const string ConfigurationSection = "AppSettings:{{appName}}.Business";
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
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>1591</NoWarn>
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
using Microsoft.Extensions.Localization;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Enums;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;

namespace {{appName}}.Business.Services
{
    [SpiderlyService]
    public class AuthorizationService : AuthorizationServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;
        private readonly SecurityService<User, UserExternalLogin> _securityService;

        public AuthorizationService(
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            SecurityService<User, UserExternalLogin> securityService,
            IStringLocalizer localizer,
            IPrincipalRegistry principalRegistry
        )
            : base(context, authenticationService, localizer, principalRegistry)
        {
            _context = context;
            _authenticationService = authenticationService;
            _securityService = securityService;
        }

        #region User

        public override async Task AuthorizeUserUpdateAndThrow(UserDTO userDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                User user = await GetInstanceAsync<User, long>(userDTO.Id, null);

                if (user.Email != userDTO.Email)
                    throw new SecurityViolationException($"No one can change {nameof(userDTO.Email)} from the main UI form.");

                bool hasAdminUpdatePermission = await IsAuthorizedAsync(PermissionCodes.UpdateUser);
                if (hasAdminUpdatePermission)
                    return;

                long currentUserId = _authenticationService.GetCurrentUserId();
                if (currentUserId != userDTO.Id)
                    throw new SecurityViolationException($"User without admin update permission which is not current user tryed to update user.");

                if (userDTO.IsDisabled != user.IsDisabled)
                    throw new SecurityViolationException($"User without admin update permission tryed to change {nameof(userDTO.IsDisabled)}.");
            });
        }

        public override async Task AuthorizeUserInsertAndThrow(UserDTO userDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminInsertPermission = await IsAuthorizedAsync(PermissionCodes.InsertUser);
                if (hasAdminInsertPermission)
                    return;

                throw new SecurityViolationException("User without admin insert permission tryed to add new user.");
            });
        }

        #endregion

    }
}
""";
        }

        private static string GetSecurityServiceCsData(string appName)
        {
            return $$"""
using {{appName}}.Business.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Spiderly.Security;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.ExternalAuth;
using Spiderly.Shared.Interfaces;

namespace {{appName}}.Business.Services
{
    public class SecurityService<TUser, TUserExternalLogin> : SecurityServiceBase<TUser, TUserExternalLogin>
        where TUser : class, IUser, new()
        where TUserExternalLogin : class, IUserExternalLogin, new()
    {
        private readonly IApplicationDbContext _context;

        public SecurityService(
            IApplicationDbContext context,
            IJwtAuthManager jwtAuthManagerService,
            IEmailingService emailingService,
            AuthenticationService authenticationService,
            IWebHostEnvironment environment,
            IStringLocalizer localizer,
            IOptions<AuthPolicyOptions> authPolicyOptions,
            IExternalAuthProviderRegistry externalAuthProviderRegistry,
            ExternalAuthCodeFlow externalAuthCodeFlow,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<Spiderly.Shared.Settings> sharedSettings
        )
            : base(context, jwtAuthManagerService, emailingService, authenticationService, environment, localizer, authPolicyOptions, externalAuthProviderRegistry, externalAuthCodeFlow, dataProtectionProvider, sharedSettings)
        {
            _context = context;
        }

        /// <summary>
        /// Assigns admin role to the first user in the app. 
        /// This is a performance bottleneck.
        /// Delete this method once the first user has admin permissions.
        /// </summary>
        public override async Task OnAfterLogin(AuthResultDTO authResultDTO)
        {
            bool isFirstUserEver = await _context.DbSet<User>().CountAsync() == 1;
            if (isFirstUserEver)
            {
                Role? adminRole = await _context.DbSet<Role>().FirstOrDefaultAsync(x => x.Name == "Admin");
                if (adminRole != null)
                {
                    User? user = await _context.DbSet<User>().FirstOrDefaultAsync(x => x.Id == authResultDTO.UserId);
                    if (user != null && !user.Roles.Any())
                    {
                        user.Roles.Add(adminRole);
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

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
    [SpiderlyDataMapper]
    public static partial class Mapper
    {

    }
}
""";
        }

        #endregion

        #region Angular

        private static string GetExtensionsJsonData(DbProviderCodes dbProvider)
        {
            string dbExtension = dbProvider == DbProviderCodes.PostgreSQL
                ? "ckolkman.vscode-postgres"
                : "ms-mssql.mssql";

            return $$"""
{
  "recommendations": [
    "angular.ng-template",
    "formulahendry.auto-rename-tag",
    "{{dbExtension}}",
    "esbenp.prettier-vscode"
  ]
}

""";
        }


        private static string GetLaunchJsonData(string appName, PackageManagerCodes packageManager)
        {
            string pmCommand = packageManager.GetCommandName();

            return $$"""
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch Backend (.NET)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "dotnet",
      "args": [
        "watch",
        "run",
        "--project",
        "${workspaceFolder}/Backend/{{appName}}.WebAPI/{{appName}}.WebAPI.csproj"
      ],
      "cwd": "${workspaceFolder}/Backend/{{appName}}.WebAPI",
      "stopAtEntry": false,
      "launchSettingsProfile": "http",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/Views"
      }
    },
    {
      "name": "Launch Frontend (Angular)",
      "type": "node",
      "request": "launch",
      "runtimeExecutable": "{{pmCommand}}",
      "runtimeArgs": ["start"],
      "cwd": "${workspaceFolder}/Frontend"
    }
  ],
  "compounds": [
    {
      "name": "Launch (Backend + Frontend)",
      "configurations": ["Launch Backend (.NET)", "Launch Frontend (Angular)"],
      "stopAll": true,
      "presentation": {
        "hidden": false,
        "group": "{{appName}}",
        "order": 1
      }
    }
  ]
}

""";
        }

        private static string GetSettingsJsonData()
        {
            return """
{
  "editor.defaultFormatter": "esbenp.prettier-vscode",
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll": "explicit",
    "source.organizeImports": "explicit"
  },

  "[csharp]": {
    "editor.defaultFormatter": "ms-dotnettools.csharp"
  }
}
""";
        }

        private static string GetDotnetToolsJsonData(string spiderlyVersion)
        {
            return $$"""
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "9.0.1",
      "commands": [
        "dotnet-ef"
      ]
    },
    "Spiderly.CLI": {
      "version": "{{spiderlyVersion}}",
      "commands": [
        "spiderly"
      ]
    }
  }
}
""";
        }

        private static string GetTasksJsonData(string appName)
        {
            return $$"""
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "build",
        "Backend/{{appName}}.WebAPI/{{appName}}.WebAPI.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "options": {
        "cwd": "${workspaceFolder}"
      },
      "problemMatcher": "$msCompile",
      "group": {
        "kind": "build",
        "isDefault": true
      }
    }
  ]
}

""";
        }

        private static string GetVercelJsonData(string appName)
        {
            return $$"""
{
    "rewrites": [{ "source": "/(.*)", "destination": "/src/index.html" }],
    "outputDirectory": "dist/{{appName}}/browser"
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
        "test:e2e": "playwright test",
        "test:e2e:ui": "playwright test --ui",
        "test:e2e:headed": "playwright test --headed",
        "test:e2e:debug": "playwright test --debug",
        "test:e2e:report": "playwright show-report",
        "i18n:extract": "transloco-keys-manager extract --langs en",
        "i18n:find": "transloco-keys-manager find"
    },
    "private": true,
    "dependencies": {
{{(isRunningFromNuget /* Note: Can't comment it out because it's json */ ? $$"""
        "spiderly": "{{version}}",
""" : "")}}
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
        "marked": "15.0.12",
        "ngx-markdown": "19.1.1",
        "ngx-spinner": "19.0.0",
        "primeicons": "7.0.0",
        "primeng": "19.1.3",
        "@primeng/themes": "19.1.3",
        "quill": "2.0.2",
        "rxjs": "7.8.1",
        "tslib": "2.3.0",
        "zone.js": "0.15.1"
    },
    "devDependencies": {
        "@angular-devkit/build-angular": "19.2.13",
        "@angular/cli": "19.2.13",
        "@angular/compiler-cli": "19.2.13",
        "@jsverse/transloco-keys-manager": "6.2.2",
        "@playwright/test": "1.60.0",
        "@types/jasmine": "5.1.0",
        "@types/node": "22.10.5",
        "jasmine-core": "5.1.0",
        "karma": "6.4.0",
        "karma-chrome-launcher": "3.2.0",
        "karma-coverage": "2.2.0",
        "karma-jasmine": "5.1.0",
        "karma-jasmine-html-reporter": "2.1.0",
        "tailwindcss": "^4.0.0",
        "@tailwindcss/postcss": "^4.0.0",
        "tailwindcss-primeui": "0.6.1",
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
                            "src/assets/tailwind.css",
                            "src/assets/styles.scss"
                        ],
                        "scripts": [],
                        "stylePreprocessorOptions": {
                            "sass": {
                                "silenceDeprecations": ["global-builtin", "import"]
                            }
                        }
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
                            "src/assets/tailwind.css",
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

        private static string GetPrettierRcData()
        {
            return """
{
    "printWidth": 120
}
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

        private static string GetEnvironmentTsData(string appName)
        {
            return $$"""
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api',
  frontendUrl: 'http://localhost:4200',
  companyName: '{{appName}}',
};
""";
        }

        private static string GetEnvironmentProdTsData(string appName)
        {
            return $$"""
export const environment = {
  production: true,
  apiUrl: 'https://your-production-api.com/api',
  frontendUrl: 'https://your-production-frontend.com',
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
      50: '{zinc.50}',
      100: '{zinc.100}',
      200: '{zinc.200}',
      300: '{zinc.300}',
      400: '{zinc.400}',
      500: '{zinc.500}',
      600: '{zinc.600}',
      700: '{zinc.700}',
      800: '{zinc.800}',
      900: '{zinc.900}',
      950: '{zinc.950}',
      color: '{zinc.950}',          
      contrastColor: '{surface.0}',
      hoverColor: '{zinc.800}',
      activeColor: '{zinc.700}',
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

        private static string GetTailwindCssData(bool isRunningFromNuget)
        {
            return $$"""
@layer primeng;
@import "tailwindcss/theme";
/* Bridges PrimeNG's runtime theme variables (--p-surface-*, --p-primary-* from the Aura preset) to
   Tailwind color tokens, so utilities like `bg-primary-50` / `border-surface-200` resolve to the
   active theme instead of being dead classes. MUST stay before `tailwindcss/utilities`: it registers
   the tokens via `@theme inline`, and Tailwind generates utilities at the utilities import, so tokens
   added after that point are silently ignored. The tokens are unlayered and the enabled utilities land
   in the `utilities` layer, so don't move this into `@layer primeng`. Note: Tailwind v4 defaults a bare
   `border` (no color modifier) to `currentColor` — always name a color. */
@import "tailwindcss-primeui";
@import "tailwindcss/utilities";

{{SlashCommented("""
@source "../../../../spiderly/Angular/projects/spiderly/src/lib";
""", isRunningFromNuget)}}

{{SlashCommented("""
@source "../../node_modules/spiderly";
""", !isRunningFromNuget)}}
""";
        }

        private static string GetPostCssRcJsonData()
        {
            return """
{
    "plugins": {
        "@tailwindcss/postcss": {}
    }
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
  "IsDisabled": "Account is disabled",
  "PartnerRoleList": "Roles",
  "Save": "Save",
  "AddNewTier": "Add new loyalty tier",
  "AddNewBusinessSystemTier": "Add new discount product group",
  "SegmentationList": "Segmentations",
  "AddNewSegmentationItem": "Add new segmentation item",
  "RoleList": "Roles",
  "Permissions": "Permissions",
  "Settings": "Settings",
  "PartnerList": "Partners",
  "SelectThePartner": "Select partner",
  "AgreementsOnRegister": "By clicking Login, you accept the",
  "UserAgreement": "terms of use",
  "PrivacyPolicy": "privacy policy",
  "and": "and",
  "CookiePolicy": "cookie policy",
  "AgreeAndJoin": "Agree and Join",
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
  "RememberYourPassword": "Remembered your password?",
  "ResetPassword": "Reset password",
  "DragAndDropFilesHereToUpload": "Drag and drop files here to upload.",
  "PleaseConfirmToProceed": "Please confirm to proceed.",
  "DeleteBulkConfirmation": "Are you sure you want to delete {{count}} items?",
  "Cancel": "Cancel",
  "Confirm": "Confirm",
  "Clear": "Clear",
  "Write": "Write",
  "Preview": "Preview",
  "UploadingImage": "Uploading image…",
  "ImageUploadFailed": "Image upload failed.",
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
  "Profile": "Profile",
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
  "ExternalLoginExpiredDetails": "Your sign-in took too long and expired. Please try again.",
  "ExternalLoginFailedDetails": "Sign-in failed. Please try again.",
  "ServerLostConnectionDetails": "Connection lost. Please check your internet connection. If the problem persists, please contact our support team.",
  "ServerLostConnectionTitle": "Connection Lost",
  "PermissionErrorDetails": "You do not have permission for this operation.",
  "PermissionErrorTitle": "Permission Denied",
  "NotFoundDetails": "The requested resource was not found, please try again.",
  "NotFoundTitle": "Not Found",
  "UnexpectedErrorTitle": "An error occurred",
  "UnexpectedErrorDetails": "Our team has been notified and we are working on a solution. Please try again later.",
  "ErrorReference": "Error reference: {{traceId}}",
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
  "StartsWith": "Starts with",
  "Contains": "Contains",
  "AreYouSure": "Are you sure?",
  "SuccessfullyDeletedMessage": "Successfully deleted.",
  "SuccessfullyDeletedListMessage": "Successfully deleted selected records.",
  "DeleteSelected": "Delete selected",
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
  "Length": "Field must have a minimum of {{min}} and a maximum of {{max}} characters.",
  "MaxLength": "Field must have a maximum of {{max}} characters.",
  "SingleLength": "Field must have {{length}} characters.",
  "NumberRangeMin": "Field value must be greater than or equal to {{min}}.",
  "NumberRangeMinNumberRangeMax": "Field value must be between {{min}} and {{max}}.",
  "PrecisionScale": "Field value must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NumberRangeMinPrecisionScale": "Field value must be greater than or equal to {{min}}, must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NotEmptyLength": "Field cannot be empty and must have a minimum of {{min}} and a maximum of {{max}} characters.",
  "NotEmptyMaxLength": "Field cannot be empty and must have a maximum of {{max}} characters.",
  "NotEmptySingleLength": "Field cannot be empty and must have {{length}} characters.",
  "NotEmptyNumberRangeMin": "Field cannot be empty and value must be greater than or equal to {{min}}.",
  "NotEmptyNumberRangeMinNumberRangeMax": "Field cannot be empty and value must be between {{min}} and {{max}}.",
  "NotEmptyPrecisionScale": "Field cannot be empty, value must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NotEmptyNumberRangeMinPrecisionScale": "Field cannot be empty, value must be greater than or equal to {{min}}, must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NotEmptyLengthEmailAddress": "Field cannot be empty, must have a minimum of {{min}} and a maximum of {{max}} characters, and must be a valid email address.",
  "ImageWidthMustBeExact": "Image width must be exactly {{ 0 }}px (current: {{ 1 }}px).",
  "ImageHeightMustBeExact": "Image height must be exactly {{ 0 }}px (current: {{ 1 }}px).",
  "FileSizeExceeded": "File size must not exceed {{ 0 }} MB.",
  "IdToken": "/",
  "Browser": "/",
  "NewPassword": "New password",
  "ExpiresAt": "Expires at",
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
  "RoleSaveBody": "/",
  "VerificationTokenRequest": "/",
  "Permission": "Permission",
  "Role": "Role",
  "RoleUser": "/",
  "Checked": "Checked",
  "PointsMultiplier": "Points multiplier",
  "TableFilter": "Table filter",
  "SelectedIds": "Selected",
  "UnselectedIds": "Unselected",
  "IsAllSelected": "All selected",
  "TransactionCode": "Transaction code",
  "Discount": "Discount",
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
  "PartnerProfile": "Partner profile",
  "SuccessfulSaveAndRefreshThePageToastDescription": "Successfully saved. To see partner changes, please refresh the page.",
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
  "PartnerUser": "User",
  "SegmentationItem": "Segmentation item",
  "User": "User",
  "Brand": "Brand",
  "MergedPartnerUser": "User",
  "PartnerRoleSaveBody": "/",
  "PartnerUserSaveBody": "/",
  "QrCode": "QR Code",
  "SegmentationSaveBody": "/",
  "UserSaveBody": "/",
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
  "Columns": "Columns",
  "ResetToDefault": "Reset to default",
  "PartnerUserList": "Users",
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
**/FileStorage

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
**/pnpm-debug.log
**/.pnpm-debug.log
**/*.env
**/*.env.local

# Local dev overrides (real secrets stay out of git)
**/appsettings.Development.local.json
**/appsettings.*.local.json

# IDEs and editors
**/.idea/
**/.project
**/.classpath
**/.c9/
**/*.launch
**/.settings/
**/*.sublime-workspace

# Visual Studio Code
.vscode/*
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json
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

# Spiderly agent guidance — machine-local junctions regenerated by `spiderly agent-sync`
# (AGENTS.md is committed; these links point at absolute node_modules paths, so they're per-machine)
**/.claude/skills/spiderly-*

# Spiderly machine-local config (e.g. agent-sync workspace target); committed config is .spiderly/config.json
**/.spiderly/*.local.json

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

        private static string GetClaudeMdData(string appName)
        {
            return $$"""
# {{appName}}

A Spiderly application — .NET 9 backend + Angular 19 admin panel, scaffolded by Spiderly CLI.

## What is Spiderly

Spiderly is a code generator. You define EF Core entities as C# classes decorated with custom attributes; source generators emit DTOs, controllers, services, FluentValidation rules, Mapster mappers, Angular CRUD pages, TypeScript entity classes, validators, and translation entries.

**Hand-written code extends generated base classes.** Generated files are regenerated on every build — never edit them directly. Custom logic lives in entity service overrides, custom controllers, custom DTOs, and override hooks.

**The Angular files are generated by the .NET build, not by `ng`.** The generators are Roslyn source generators in the backend, but the Angular ones don't emit into the C# compilation — they write `.ts` files straight to disk in the sibling `Frontend/` project (entities, enums, validators, the API service, base detail pages, as `*.generated.ts`). So you regenerate the frontend by building the `Backend/` solution, not via the Angular CLI.

## Layout

- `Backend/` — .NET 9 solution
  - `{{appName}}.WebAPI` — ASP.NET host
  - `{{appName}}.Business` — entity services (`{Entity}Service : {Entity}ServiceGenerated`), hand-written DTOs, business logic
  - `{{appName}}.Infrastructure` — `EntityModels/` (entity classes), DbContext, EF migrations
  - `{{appName}}.Migrations` — lightweight startup project for `dotnet ef` (avoids DLL locking when the WebAPI is running)
- `Frontend/` — Angular 19 SPA (admin panel)
  - `src/app/business/entities` — generated TypeScript entity classes
  - `src/app/business/components` — generated and custom CRUD pages
  - `src/app/business/services` — API service, auth service, layout service
- `tests/e2e/` — Playwright end-to-end tests

## Key conventions

- **Classification attributes are required** on hand-written classes (source generators enroll by attribute, not namespace):
  - Entities → `[SpiderlyEntity]`
  - M2M junctions → `[M2M]` **and** `[SpiderlyEntity]`
  - Custom controllers → `[SpiderlyController]`
  - Hand-written DTOs → `[SpiderlyDTO]`
  - Entity services extending the generated base → `[SpiderlyService]`
  - Hand-written partial mapper class → `[SpiderlyDataMapper]`
  - C# enums / class-based string-constant enums exposed to Angular → `[SpiderlyEnum]`
- **Database table names are singular** — match the entity class name exactly (`Category` class → `"Category"` table).
- **`bool?` is preferred** over non-nullable `bool` for checkbox properties; treat `null` as `false`.
- **EF migrations**: run `dotnet ef` commands from `Backend/{{appName}}.Migrations/` as the startup project.

## Working with Claude Code

Spiderly's AI-agent guidance is **version-matched to your installed package**. The `spiderly` npm package ships docs + skills under `Frontend/node_modules/spiderly/agent/`, and `spiderly agent-sync` (run automatically by `spiderly init`) projects them into this project:

- **`AGENTS.md`** — an always-on pointer telling agents to read version-matched Spiderly reference docs from `Frontend/node_modules/spiderly/agent/docs/`. `CLAUDE.md` imports it via `@AGENTS.md`, so it's always in context. Cross-agent (Cursor, Copilot, Codex too).
- **`.claude/skills/spiderly-*`** — on-demand, trigger-based skills for deeper workflows (scaffold an entity, EF migrations, deployment, upgrade). Run `/skills` to list.

Re-run **`spiderly agent-sync`** anytime to refresh both after upgrading the package — it's idempotent and reconciles renamed/removed skills automatically.
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
                return $"/* {input} */";
            }

            return input;
        }

        private static string GetPlaywrightConfigData(PackageManagerCodes packageManager)
        {
            string pmCommand = packageManager.GetCommandName();

            return $$"""
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e/specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: {
    command: '{{pmCommand}} start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
  },
});
""";
        }

        private static string GetE2EGitignoreData()
        {
            return """
test-results/
playwright-report/
playwright/.cache/
""";
        }

        private static string GetBasePageObjectData()
        {
            return """
import { Page } from '@playwright/test';

export class BasePage {
  constructor(protected page: Page) {}

  async navigate(path: string) {
    await this.page.goto(path);
  }

  async waitForNavigation() {
    await this.page.waitForLoadState('networkidle');
  }

  async clickButton(text: string) {
    await this.page.getByRole('button', { name: text }).click();
  }

  async fillInput(label: string, value: string) {
    await this.page.getByLabel(label).fill(value);
  }

  async getTableRowCount() {
    return await this.page.locator('tbody tr').count();
  }
}
""";
        }

        private static string GetLoginPageObjectData()
        {
            return """
import { Page } from '@playwright/test';
import { BasePage } from './base-page';

export class LoginPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto() {
    await this.navigate('/');
  }

  async login(email: string, password: string) {
    await this.page.getByLabel('Email').fill(email);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Login' }).click();
    await this.waitForNavigation();
  }

  async isLoggedIn() {
    return await this.page.locator('[data-testid="user-menu"]').isVisible();
  }

  async logout() {
    await this.page.locator('[data-testid="user-menu"]').click();
    await this.page.getByRole('menuitem', { name: 'Logout' }).click();
  }
}
""";
        }

        private static string GetUserListPageObjectData()
        {
            return """
import { Page } from '@playwright/test';
import { BasePage } from './base-page';

export class UserListPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto() {
    await this.navigate('/administration/user');
  }

  async clickAddNew() {
    await this.clickButton('Add New');
  }

  async searchUser(searchTerm: string) {
    await this.page.getByPlaceholder('Search').fill(searchTerm);
    await this.waitForNavigation();
  }

  async deleteUser(userName: string) {
    const row = this.page.locator('tr', { hasText: userName });
    await row.getByRole('button', { name: 'Delete' }).click();
    await this.page.getByRole('button', { name: 'Confirm' }).click();
  }

  async editUser(userName: string) {
    const row = this.page.locator('tr', { hasText: userName });
    await row.getByRole('button', { name: 'Edit' }).click();
  }
}
""";
        }

        private static string GetAuthSpecData()
        {
            return """
import { test, expect } from '@playwright/test';
import { LoginPage } from '../page-objects/login-page';

test.describe('Authentication', () => {
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    await loginPage.goto();
  });

  test('should display login page', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();
  });

  test('should login with valid credentials', async ({ page }) => {
    await loginPage.login('admin@example.com', 'Admin123!');
    await expect(page).toHaveURL(/.*homepage/);
    const isLoggedIn = await loginPage.isLoggedIn();
    expect(isLoggedIn).toBe(true);
  });

  test('should show error with invalid credentials', async ({ page }) => {
    await loginPage.login('invalid@example.com', 'wrongpassword');
    await expect(page.getByText(/invalid credentials/i)).toBeVisible();
  });

  test('should logout successfully', async ({ page }) => {
    await loginPage.login('admin@example.com', 'Admin123!');
    await loginPage.logout();
    await expect(page).toHaveURL('/');
  });
});
""";
        }

        private static string GetUserCrudSpecData()
        {
            return """
import { test, expect } from '@playwright/test';
import { LoginPage } from '../page-objects/login-page';
import { UserListPage } from '../page-objects/user-list-page';

test.describe('User CRUD Operations', () => {
  let loginPage: LoginPage;
  let userListPage: UserListPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    userListPage = new UserListPage(page);

    await loginPage.goto();
    await loginPage.login('admin@example.com', 'Admin123!');
  });

  test('should display users list', async ({ page }) => {
    await userListPage.goto();
    await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  });

  test('should create a new user', async ({ page }) => {
    await userListPage.goto();
    await userListPage.clickAddNew();

    await page.getByLabel('First Name').fill('John');
    await page.getByLabel('Last Name').fill('Doe');
    await page.getByLabel('Email').fill('john.doe@example.com');
    await page.getByLabel('Password').fill('Password123!');

    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('User created successfully')).toBeVisible();
    await expect(page.locator('tr', { hasText: 'john.doe@example.com' })).toBeVisible();
  });

  test('should edit an existing user', async ({ page }) => {
    await userListPage.goto();
    await userListPage.editUser('john.doe@example.com');

    await page.getByLabel('First Name').clear();
    await page.getByLabel('First Name').fill('Jane');

    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('User updated successfully')).toBeVisible();
    await expect(page.locator('tr', { hasText: 'Jane' })).toBeVisible();
  });

  test('should delete a user', async ({ page }) => {
    await userListPage.goto();
    const initialCount = await userListPage.getTableRowCount();

    await userListPage.deleteUser('jane.doe@example.com');

    await expect(page.getByText('User deleted successfully')).toBeVisible();

    const newCount = await userListPage.getTableRowCount();
    expect(newCount).toBe(initialCount - 1);
  });

  test('should search for users', async ({ page }) => {
    await userListPage.goto();
    await userListPage.searchUser('admin');

    await expect(page.locator('tr', { hasText: 'admin' })).toBeVisible();
  });
});
""";
        }

        #endregion

    }
}
