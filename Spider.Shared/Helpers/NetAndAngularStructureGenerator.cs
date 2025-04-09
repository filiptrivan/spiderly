using Spider.Shared.Classes;
using Spider.Shared.Extensions;
using CaseConverter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Spider.Shared.Helpers
{
    public static class NetAndAngularStructureGenerator
    {
        public static void Generate(string outputPath, string appName, string primaryColor)
        {
            SpiderFolder appStructure = new SpiderFolder
            {
                Name = appName,
                ChildFolders =
                {
                    new SpiderFolder
                    {
                        Name = appName.ToKebabCase(),
                        ChildFolders =
                        {
                            new SpiderFolder
                            {
                                Name = "Angular",
                                ChildFolders =
                                {
                                    new SpiderFolder
                                    {
                                        Name = "plop",
                                        ChildFolders =
                                        {
                                            new SpiderFolder
                                            {
                                                Name = "output"
                                            }
                                        },
                                        Files =
                                        {
                                            new SpiderFile { Name = "spider-controller-cs-template.hbs", Data = GetSpiderControllerCsTemplateHbsData(appName) },
                                            new SpiderFile { Name = "spider-details-html-template.hbs", Data = GetSpiderDetailsHtmlTemplateHbsData() },
                                            new SpiderFile { Name = "spider-details-ts-template.hbs", Data = GetSpiderDetailsTsTemplateHbsData() },
                                            new SpiderFile { Name = "spider-table-html-template.hbs", Data = GetSpiderTableHtmlTemplateHbsData() },
                                            new SpiderFile { Name = "spider-table-ts-template.hbs", Data = GetSpiderTableTsTemplateHbsData() },
                                        }
                                    },
                                    new SpiderFolder
                                    {
                                        Name = "src",
                                        ChildFolders =
                                        {
                                            new SpiderFolder
                                            {
                                                Name = "app",
                                                ChildFolders =
                                                {
                                                    new SpiderFolder
                                                    {
                                                        Name = "business",
                                                        ChildFolders =
                                                        {
                                                            new SpiderFolder
                                                            {
                                                                Name = "components",
                                                                ChildFolders =
                                                                {
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "base-details",
                                                                    },
                                                                }
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "entities",
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "enums",
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "guards",
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "interceptors",
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "layout",
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "layout.component.html", Data = GetLayoutComponentHtmlCode() },
                                                                    new SpiderFile { Name = "layout.component.ts", Data = GetLayoutComponentTsCode() },
                                                                }
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "services",
                                                                ChildFolders =
                                                                {
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "api",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "api.service.ts", Data = GetAPIServiceTsCode() },
                                                                        }
                                                                    },
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "auth",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "auth.service.ts", Data = GetAuthServiceTsCode() },
                                                                        }
                                                                    },
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "layout",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "layout.service.ts", Data = GetLayoutServiceTsCode() },
                                                                        }
                                                                    },
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "translates",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "merge-class-names.ts", Data = GetMergeClassNamesTsCode() },
                                                                            new SpiderFile { Name = "merge-labels.ts", Data = GetMergeLabelsCode() },
                                                                        }
                                                                    },
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "validators",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "validators.ts", Data = GetValidatorsTsCode() },
                                                                        }
                                                                    },
                                                                },
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "config.service.ts", Data = GetConfigServiceTsCode() },
                                                                }
                                                            },
                                                        },
                                                        Files =
                                                        {
                                                            new SpiderFile { Name = "business.module.ts", Data = GetBusinessModuleTsData() }
                                                        }
                                                    },
                                                    new SpiderFolder
                                                    {
                                                        Name = "features",
                                                        ChildFolders =
                                                        {
                                                            new SpiderFolder
                                                            {
                                                                Name = "administration",
                                                                ChildFolders =
                                                                {
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "pages",
                                                                        ChildFolders =
                                                                        {
                                                                            new SpiderFolder
                                                                            {
                                                                                Name = "notification",
                                                                                Files =
                                                                                {
                                                                                    new SpiderFile { Name = "notification-details.component.html", Data = GetNotificationDetailsComponentHtmlData() },
                                                                                    new SpiderFile { Name = "notification-details.component.ts", Data = GetNotificationDetailsComponentTsData() },
                                                                                    new SpiderFile { Name = "notification-table.component.html", Data = GetNotificationTableComponentHtmlData() },
                                                                                    new SpiderFile { Name = "notification-table.component.ts", Data = GetNotificationTableComponentTsData() },
                                                                                }
                                                                            },
                                                                            new SpiderFolder
                                                                            {
                                                                                Name = "user",
                                                                                Files =
                                                                                {
                                                                                    new SpiderFile { Name = "user-details.component.html", Data = GetUserDetailsComponentHtmlData() },
                                                                                    new SpiderFile { Name = "user-details.component.ts", Data = GetUserDetailsComponentTsData() },
                                                                                    new SpiderFile { Name = "user-table.component.html", Data = GetUserTableComponentHtmlData() },
                                                                                    new SpiderFile { Name = "user-table.component.ts", Data = GetUserTableComponentTsData() },
                                                                                }
                                                                            },
                                                                            new SpiderFolder
                                                                            {
                                                                                Name = "role",
                                                                                Files =
                                                                                {
                                                                                    new SpiderFile { Name = "role-details.component.html", Data = GetRoleDetailsComponentHtmlData() },
                                                                                    new SpiderFile { Name = "role-details.component.ts", Data = GetRoleDetailsComponentTsData() },
                                                                                    new SpiderFile { Name = "role-table.component.html", Data = GetRoleTableComponentHtmlData() },
                                                                                    new SpiderFile { Name = "role-table.component.ts", Data = GetRoleTableComponentTsData() },
                                                                                }
                                                                            },
                                                                        },
                                                                    },
                                                                },
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "administration.module.ts", Data = GetAdministrationModuleTsData() }
                                                                }
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "dashboard",
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "dashboard.component.html", Data = GetDashboardComponentHtmlData() },
                                                                    new SpiderFile { Name = "dashboard.component.ts", Data = GetDashboardComponentTsData() },
                                                                    new SpiderFile { Name = "dashboard.module.ts", Data = GetDashboardModuleTsData() },
                                                                }
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "legal",
                                                                ChildFolders =
                                                                {
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "privacy-policy",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "privacy-policy.component.html", Data = GetPrivacyPolicyComponentHtmlData() },
                                                                            new SpiderFile { Name = "privacy-policy.component.ts", Data = GetPrivacyPolicyComponentTsData() },
                                                                        },
                                                                    },
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "user-agreement",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "user-agreement.component.html", Data = GetUserAgreementComponentHtmlData() },
                                                                            new SpiderFile { Name = "user-agreement.component.ts", Data = GetUserAgreementComponentTsData() },
                                                                        },
                                                                    },
                                                                },
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "legal.module.ts", Data = GetLegalModuleTsData() },
                                                                }
                                                            },
                                                            new SpiderFolder
                                                            {
                                                                Name = "notification",
                                                                ChildFolders =
                                                                {
                                                                    new SpiderFolder
                                                                    {
                                                                        Name = "pages",
                                                                        Files =
                                                                        {
                                                                            new SpiderFile { Name = "notification.component.html", Data = GetClientNotificationComponentHtmlData() },
                                                                            new SpiderFile { Name = "notification.component.ts", Data = GetClientNotificationComponentTsData() },
                                                                        },
                                                                    },
                                                                },
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "notification.module.ts", Data = GetClientNotificationModuleTsData() },
                                                                }
                                                            },
                                                        }
                                                    },
                                                },
                                                Files =
                                                {
                                                    new SpiderFile { Name = "app-routing.module.ts", Data = GetAppRoutingModuleTsData() },
                                                    new SpiderFile { Name = "app.component.html", Data = GetAppComponentHtmlData() },
                                                    new SpiderFile { Name = "app.component.ts", Data = GetAppComponentTsData() },
                                                    new SpiderFile { Name = "app.module.ts", Data = GetAppModuleTsData() },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "assets",
                                                ChildFolders =
                                                {
                                                    new SpiderFolder
                                                    {
                                                        Name = "i18n",
                                                        Files =
                                                        {
                                                            new SpiderFile { Name = "en.generated.json", Data = "" },
                                                            new SpiderFile { Name = "en.json", Data = GetTranslocoEnJsonCode() },
                                                            new SpiderFile { Name = "sr-Latn-RS.generated.json", Data = "" },
                                                            new SpiderFile { Name = "sr-Latn-RS.json", Data = GetTranslocoSrLatnRSJsonCode() },
                                                        }
                                                    },
                                                    new SpiderFolder
                                                    {
                                                        Name = "images",
                                                        ChildFolders =
                                                        {
                                                            new SpiderFolder
                                                            {
                                                                Name = "logo",
                                                                Files =
                                                                {
                                                                    new SpiderFile { Name = "favicon.ico", Data = GetFaviconIcoData() },
                                                                    new SpiderFile { Name = "logo.svg", Data = GetLogoSvgData() },
                                                                }
                                                            }
                                                        }
                                                    },
                                                },
                                                Files =
                                                {
                                                    new SpiderFile { Name = "shared.scss", Data = "" },
                                                    new SpiderFile { Name = "styles.scss", Data = GetStylesScssCode() },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "environments",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "environment.prod.ts", Data = "" },
                                                    new SpiderFile { Name = "environment.ts", Data = GetEnvironmentTsCode(appName, primaryColor) },
                                                }
                                            }
                                        },
                                        Files =
                                        {
                                            new SpiderFile { Name = "index.html", Data = GetIndexHtmlData(appName) },
                                            new SpiderFile { Name = "main.ts", Data = GetMainTsData() },
                                        }
                                    }
                                },
                                Files =
                                {
                                    new SpiderFile { Name = ".editorconfig", Data = GetEditOrConfigData() },
                                    new SpiderFile { Name = "angular.json", Data = GetAngularJsonData(appName) },
                                    new SpiderFile { Name = "package.json", Data = GetPackageJsonData(appName) },
                                    new SpiderFile { Name = "plopfile.js", Data = GetPlopFileJsData() },
                                    new SpiderFile { Name = "README.md", Data = "" },
                                    new SpiderFile { Name = "tsconfig.app.json", Data = GetTsConfigAppJsonData() },
                                    new SpiderFile { Name = "tsconfig.json", Data = GetTsConfigJsonData() },
                                    new SpiderFile { Name = "tsconfig.spec.json", Data = GetTsConfigSpecJsonData() },
                                    new SpiderFile { Name = "vercel.json", Data = GetVercelJsonData() },
                                }
                            },
                            new SpiderFolder
                            {
                                Name = "API",
                                ChildFolders =
                                {
                                    new SpiderFolder
                                    {
                                        Name = $"{appName}.Business",
                                        ChildFolders =
                                        {
                                            new SpiderFolder
                                            {
                                                Name = "DataMappers",
                                                Files = new List<SpiderFile>
                                                {
                                                    new SpiderFile { Name = "MapsterMapper.cs", Data = GetMapsterMapperCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "DTO",
                                                ChildFolders =
                                                {
                                                    new SpiderFolder
                                                    {
                                                        Name = "Partials",
                                                        Files = new List<SpiderFile>
                                                        {
                                                            new SpiderFile { Name = "NotificationDTO.cs", Data = GetNotificationDTOCsData(appName) },
                                                            new SpiderFile { Name = "NotificationSaveBodyDTO.cs", Data = GetNotificationSaveBodyDTOCsData(appName) },
                                                        }
                                                    },
                                                    new SpiderFolder
                                                    {
                                                        Name = "Helpers"
                                                    },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "Entities",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "Notification.cs", Data = GetNotificationCsData(appName) },
                                                    new SpiderFile { Name = "UserExtended.cs", Data = GetUserExtendedCsData(appName) },
                                                    new SpiderFile { Name = "UserNotification.cs", Data = GetUserNotificationCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "Enums",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "BusinessPermissionCodes.cs", Data = GetBusinessPermissionCodesCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "Services",
                                                Files =
                                                {
                                                    new SpiderFile { Name = $"AuthorizationBusinessService.cs", Data = GetAuthorizationServiceCsData(appName) },
                                                    new SpiderFile { Name = $"{appName}BusinessService.cs", Data = GetBusinessServiceCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "ValidationRules",
                                            },
                                        },
                                        Files =
                                        {
                                            new SpiderFile { Name = "GeneratorSettings.cs", Data = GetBusinessGeneratorSettingsData(appName) },
                                            new SpiderFile { Name = $"{appName}.Business.csproj", Data = GetBusinessCsProjData(appName) },
                                            new SpiderFile { Name = $"Settings.cs", Data = GetBusinessSettingsCsData(appName) },
                                        }
                                    },
                                    new SpiderFolder
                                    {
                                        Name = $"{appName}.Infrastructure",
                                        Files =
                                        {
                                            new SpiderFile { Name = $"{appName}ApplicationDbContext.cs", Data = GetInfrastructureApplicationDbContextData(appName) },
                                            new SpiderFile { Name = $"{appName}.Infrastructure.csproj", Data = GetInfrastructureCsProjData(appName) },
                                        }
                                    },
                                    new SpiderFolder
                                    {
                                        Name = $"{appName}.Shared",
                                        ChildFolders =
                                        {
                                            new SpiderFolder
                                            {
                                                Name = "FluentValidation",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "TranslatePropertiesConfiguration.cs", Data = GetTranslatePropertiesConfigurationCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "Resources",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "Terms.Designer.cs", Data = GetTermsDesignerCsData(appName) },
                                                    new SpiderFile { Name = "Terms.resx", Data = GetTermsResxData() },
                                                    new SpiderFile { Name = "TermsGenerated.Designer.cs", Data = GetTermsGeneratedDesignerCsData(appName) },
                                                    new SpiderFile { Name = "TermsGenerated.resx", Data = GetTermsGeneratedResxData() },
                                                    new SpiderFile { Name = "TermsGenerated.sr-Latn-RS.resx", Data = GetTermsGeneratedSrLatnRSResxData() },
                                                }
                                            }
                                        },
                                        Files =
                                        {
                                            new SpiderFile { Name = $"{appName}.Shared.csproj", Data = GetSharedCsProjData() },
                                        }
                                    },
                                    new SpiderFolder
                                    {
                                        Name = $"{appName}.WebAPI",
                                        ChildFolders =
                                        {
                                            new SpiderFolder
                                            {
                                                Name = "Properties",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "launchSettings.json", Data = GetLaunchSettingsJsonData() },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "Controllers",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "NotificationController.cs", Data = GetNotificationControllerCsData(appName) },
                                                    new SpiderFile { Name = "SecurityController.cs", Data = GetSecurityControllerCsData(appName) },
                                                    new SpiderFile { Name = "UserExtendedController.cs", Data = GetUserExtendedControllerCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "DI",
                                                Files =
                                                {
                                                    new SpiderFile { Name = "CompositionRoot.cs", Data = GetCompositionRootCsData(appName) },
                                                }
                                            },
                                            new SpiderFolder
                                            {
                                                Name = "Helpers",
                                            },
                                        },
                                        Files =
                                        {
                                            new SpiderFile { Name = "appsettings.json", Data = GetAppSettingsJsonData(appName, null, null, null, null, null, null) }, // TODO FT: Add this to the app
                                            new SpiderFile { Name = "GeneratorSettings.cs", Data = GetWebAPIGeneratorSettingsData(appName) },
                                            new SpiderFile { Name = $"{appName}.WebAPI.csproj", Data = GetWebAPICsProjData(appName) },
                                            new SpiderFile { Name = "Program.cs", Data = GetProgramCsData(appName) },
                                            new SpiderFile { Name = "Settings.cs", Data = GetWebAPISettingsCsData(appName) },
                                            new SpiderFile { Name = "Startup.cs", Data = GetStartupCsData(appName) },
                                        }
                                    },
                                },
                                Files =
                                {
                                    new SpiderFile { Name = $"{appName}.sln", Data = GetNetSolutionData(appName) }
                                }
                            },
                            new SpiderFolder
                            {
                                Name = "Data",
                                ChildFolders =
                                {
                                    new SpiderFolder
                                    {
                                        Name = "test-data"
                                    },
                                    new SpiderFolder
                                    {
                                        Name = "update-scripts"
                                    },
                                },
                                Files =
                                {
                                    new SpiderFile { Name = "initialize-data.xlsx", Data = "" },
                                    new SpiderFile { Name = "initialize-script.sql", Data = GetInitializeScriptSqlData(appName) }
                                }
                            },
                            new SpiderFolder
                            {
                                Name = "Documentation",
                            }
                        },
                        Files =
                        {
                            new SpiderFile { Name = ".gitignore", Data = GetGitIgnoreData() },
                            new SpiderFile { Name = "License", Data = GetMitLicenseData() },
                        }
                    }
                }
            };

            GenerateProjectStructure(appStructure, outputPath);
            Console.WriteLine("App structure created.");
            CreateSqlServerDatabase(appName);
        }

        private static void GenerateProjectStructure(SpiderFolder appStructure, string path)
        {
            string newPath = GenerateFolder(appStructure, path);

            foreach (SpiderFile file in appStructure.Files)
                GenerateFile(appStructure, file, newPath);

            foreach (SpiderFolder folder in appStructure.ChildFolders)
                GenerateProjectStructure(folder, newPath);
        }

        private static string GenerateFolder(SpiderFolder appStructure, string path)
        {
            Helper.MakeFolder(path, appStructure.Name);

            return Path.Combine(path, appStructure.Name);
        }

        private static void GenerateFile(SpiderFolder parentFolder, SpiderFile file, string path)
        {
            string filePath = Path.Combine(path, file.Name);

            Helper.FileOverrideCheck(filePath);

            Helper.WriteToFile(file.Data, filePath);
        }

        private static void CreateSqlServerDatabase(string appName)
        {
            string connectionString = $"Data source=localhost\\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Encrypt=false;MultipleActiveResultSets=True;";

            string createDatabaseQuery = $$"""
IF DB_ID(N'{{appName}}') IS NULL
    CREATE DATABASE [{{appName}}];
""";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(createDatabaseQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                Console.WriteLine("Database created.");
            }
        }

        private static string GetNotificationDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <spider-card [title]="t('Notification')" icon="pi pi-bell">
        <spider-panel [isFirstMultiplePanel]="true" [showPanelHeader]="false">
            <panel-body>
                <div class="grid">
                    <div class="col-12">
                        <spider-checkbox [control]="isMarkedAsRead" [label]="t('NotifyUsers')" [fakeLabel]="false"></spider-checkbox>
                    </div>
                </div>
            </panel-body>
        </spider-panel>

        <notification-base-details
        [formGroup]="formGroup" 
        [notificationFormGroup]="notificationFormGroup" 
        (onSave)="onSave()"
        [isLastMultiplePanel]="true"
        [additionalButtons]="additionalButtons"
        (onIsAuthorizedForSaveChange)="isAuthorizedForSaveChange($event)"
        />

    </spider-card>
</ng-container>
""";
        }

        private static string GetNotificationDetailsComponentTsData()
        {
            return $$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { Notification } from 'src/app/business/entities/business-entities.generated';
import { ApiService } from 'src/app/business/services/api/api.service';
import { BaseFormCopy, SpiderFormGroup, SpiderFormControl, SpiderButton, SpiderMessageService, BaseFormService, IsAuthorizedForSaveEvent } from '@playerty/spider';

@Component({
    selector: 'notification-details',
    templateUrl: './notification-details.component.html',
    styles: [],
})
export class NotificationDetailsComponent extends BaseFormCopy implements OnInit {
    notificationFormGroup = new SpiderFormGroup<Notification>({});

    isMarkedAsRead = new SpiderFormControl<boolean>(true, {updateOn: 'change'});

    additionalButtons: SpiderButton[] = [];
    sendEmailNotificationButton = new SpiderButton({label: this.translocoService.translate('SendEmailNotification'), icon: 'pi pi-send', disabled: true});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderMessageService, 
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
        this.additionalButtons.push(this.sendEmailNotificationButton);
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

    // FT: We must to do it like arrow function
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
    <spider-data-table 
    [tableTitle]="t('NotificationList')" 
    [cols]="cols" 
    [getTableDataObservableMethod]="getNotificationTableDataObservableMethod" 
    [exportTableDataToExcelObservableMethod]="exportNotificationTableDataToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="deleteNotificationObservableMethod"
    >
    </spider-data-table>
</ng-container>
""";
        }

        private static string GetNotificationTableComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Column } from '@playerty/spider';
import { ApiService } from 'src/app/business/services/api/api.service';
import { Notification } from 'src/app/business/entities/business-entities.generated';

@Component({
    selector: 'notification-table',
    templateUrl: './notification-table.component.html',
    styles: []
})
export class NotificationTableComponent implements OnInit {
    cols: Column<Notification>[];

    getNotificationTableDataObservableMethod = this.apiService.getNotificationTableData;
    exportNotificationTableDataToExcelObservableMethod = this.apiService.exportNotificationTableDataToExcel;
    deleteNotificationObservableMethod = this.apiService.deleteNotification;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Actions'), actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name: this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
            {name: this.translocoService.translate('Title'), filterType: 'text', field: 'title'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
        ]
    }

}

""";
        }

        private static string GetRoleDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <spider-card [title]="t('Role')" icon="pi pi-id-card">

        <role-base-details 
        [formGroup]="formGroup" 
        [roleFormGroup]="roleFormGroup" 
        (onSave)="onSave()" 
        ></role-base-details>

    </spider-card>
</ng-container>
""";
        }

        private static string GetRoleDetailsComponentTsData()
        {
            return $$$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { Role, SpiderMessageService, BaseFormCopy, BaseFormService, SpiderFormGroup } from '@playerty/spider';

@Component({
    selector: 'role-details',
    templateUrl: './role-details.component.html',
    styles: [],
})
export class RoleDetailsComponent extends BaseFormCopy implements OnInit {
    roleFormGroup = new SpiderFormGroup<Role>({});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderMessageService, 
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
    <spider-data-table 
    [tableTitle]="t('RoleList')" 
    [cols]="cols" 
    [getTableDataObservableMethod]="getRoleTableDataObservableMethod" 
    [exportTableDataToExcelObservableMethod]="exportRoleTableDataToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="deleteRoleObservableMethod"
    ></spider-data-table>
</ng-container>
""";
        }

        private static string GetRoleTableComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ApiService } from 'src/app/business/services/api/api.service';
import { Column, Role } from '@playerty/spider';

@Component({
    selector: 'role-table',
    templateUrl: './role-table.component.html',
    styles: []
})
export class RoleTableComponent implements OnInit {
    cols: Column<Role>[];

    getRoleTableDataObservableMethod = this.apiService.getRoleTableData;
    exportRoleTableDataToExcelObservableMethod = this.apiService.exportRoleTableDataToExcel;
    deleteRoleObservableMethod = this.apiService.deleteRole;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Actions'), actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name: this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
            {name: this.translocoService.translate('Name'), filterType: 'text', field: 'name'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
        ]
    }
}

""";
        }

        private static string GetUserDetailsComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    <user-extended-base-details
    [panelTitle]="userExtendedFormGroup.getRawValue().email"
    panelIcon="pi pi-user"
    [formGroup]="formGroup" 
    [userExtendedFormGroup]="userExtendedFormGroup" 
    (onSave)="onSave()" 
    [showIsDisabledForUserExtended]="showIsDisabledControl"
    [showHasLoggedInWithExternalProviderForUserExtended]="showHasLoggedInWithExternalProvider"
    [authorizedForSaveObservable]="authorizedForSaveObservable"
    (onIsAuthorizedForSaveChange)="isAuthorizedForSaveChange($event)"
    ></user-extended-base-details>
</ng-container>
""";
        }

        private static string GetUserDetailsComponentTsData()
        {
            return $$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { UserExtended } from 'src/app/business/entities/business-entities.generated';
import { BaseFormCopy, SpiderFormGroup, SpiderMessageService, BaseFormService, IsAuthorizedForSaveEvent } from '@playerty/spider';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { combineLatest, map, Observable } from 'rxjs';
import { BusinessPermissionCodes } from 'src/app/business/enums/business-enums.generated';

@Component({
    selector: 'user-details',
    templateUrl: './user-details.component.html',
    styles: [],
})
export class UserDetailsComponent extends BaseFormCopy implements OnInit {
    userExtendedFormGroup = new SpiderFormGroup<UserExtended>({});

    showIsDisabledControl: boolean = false;
    showHasLoggedInWithExternalProvider: boolean = false;

    isAuthorizedForSave: boolean = false;

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderMessageService, 
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
        return currentUserPermissionCodes.includes(BusinessPermissionCodes.ReadUserExtended) ||
               currentUserPermissionCodes.includes(BusinessPermissionCodes.UpdateUserExtended);
    }

    isCurrentUserPage = (currentUserId: number) => {
        return currentUserId === this.userExtendedFormGroup.getRawValue().id;
    }

    isAuthorizedForSaveChange = (event: IsAuthorizedForSaveEvent) => {
        this.isAuthorizedForSave = event.isAuthorizedForSave;

        this.userExtendedFormGroup.controls.hasLoggedInWithExternalProvider.disable();
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
    <spider-data-table [tableTitle]="t('UserList')" 
    [cols]="cols" 
    [getTableDataObservableMethod]="getUserTableDataObservableMethod" 
    [exportTableDataToExcelObservableMethod]="exportUserTableDataToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="deleteUserObservableMethod"
    ></spider-data-table>
</ng-container>
""";
        }

        private static string GetUserTableComponentTsData()
        {
            return $$"""
import { ApiService } from '../../../../business/services/api/api.service';
import { TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { Column } from '@playerty/spider';

@Component({
    selector: 'user-table',
    templateUrl: './user-table.component.html',
    styles: []
})
export class UserTableComponent implements OnInit {
    cols: Column[];

    getUserTableDataObservableMethod = this.apiService.getUserExtendedTableData;
    exportUserTableDataToExcelObservableMethod = this.apiService.exportUserExtendedTableDataToExcel;
    deleteUserObservableMethod = this.apiService.deleteUserExtended;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Actions'), actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name:  this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
            {name: this.translocoService.translate('Email'), filterType: 'text', field: 'email'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
        ]
    }
}

""";
        }

        private static string GetAdministrationModuleTsData()
        {
            return $$"""
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { NotificationBaseDetailsComponent, UserExtendedBaseDetailsComponent } from 'src/app/business/components/base-details/business-base-details.generated';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { PrimengModule, SpiderDataTableComponent, SpiderControlsModule, CardSkeletonComponent, RoleBaseDetailsComponent } from '@playerty/spider';
import { NotificationDetailsComponent } from './pages/notification/notification-details.component';
import { NotificationTableComponent } from './pages/notification/notification-table.component';
import { RoleDetailsComponent } from './pages/role/role-details.component';
import { RoleTableComponent } from './pages/role/role-table.component';
import { UserDetailsComponent } from './pages/user/user-details.component';
import { UserTableComponent } from './pages/user/user-table.component';

const routes: Routes = [
    {
        path: 'users',
        component: UserTableComponent,
    },
    {
        path: 'users/:id',
        component: UserDetailsComponent,
    },
    {
        path: 'roles',
        component: RoleTableComponent,
    },
    {
        path: 'roles/:id',
        component: RoleDetailsComponent,
    },
    {
        path: 'notifications',
        component: NotificationTableComponent,
    },
    {
        path: 'notifications/:id',
        component: NotificationDetailsComponent,
    },
];

@NgModule({
    imports: [
    RouterModule.forChild(routes),
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    PrimengModule,
    SpiderDataTableComponent,
    SpiderControlsModule,
    CardSkeletonComponent,
    TranslocoDirective,
    NotificationBaseDetailsComponent,
    UserExtendedBaseDetailsComponent,
    RoleBaseDetailsComponent,
],
declarations: [
        UserTableComponent,
        UserDetailsComponent, 
        RoleTableComponent,
        RoleDetailsComponent,
        NotificationTableComponent,
        NotificationDetailsComponent,
    ],
    providers:[]
})
export class AdministrationModule { }

""";
        }

        private static string GetDashboardComponentHtmlData()
        {
            return $$"""
<ng-container *transloco="let t">
    Welcome!
</ng-container>
""";
        }

        private static string GetDashboardComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';

@Component({
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {

  constructor(

  ) {}

  ngOnInit() {

  }

  ngOnDestroy(): void {

  }

}

""";
        }

        private static string GetDashboardModuleTsData()
        {
            return $$"""
import { NgModule } from '@angular/core';
import { DashboardComponent } from './dashboard.component';
import { TranslocoDirective } from '@jsverse/transloco';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
    {
        path: '', 
        component: DashboardComponent
    }
];

@NgModule({
    imports: [
    RouterModule.forChild(routes),
    TranslocoDirective,
],
    declarations: [DashboardComponent],
    providers:[]
})
export class DashboardModule { }

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

    <spider-panel [isFirstMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="1. PRIKUPLJANJE PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>1.1.</strong> Prikupljamo podatke koje nam dobrovoljno dostavite prilikom registracije i korišćenja Platforme.</p>
        <p><strong>1.2.</strong> Automatski prikupljamo tehničke podatke o vašem uređaju, IP adresi i obrascima korišćenja Platforme.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="2. KORIŠĆENJE PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>2.1.</strong> Vaše podatke koristimo za obezbeđivanje, unapređenje i personalizaciju usluga na Platformi.</p>
        <p><strong>2.2.</strong> Podaci se mogu koristiti u analitičke svrhe kako bismo poboljšali korisničko iskustvo.</p>
        <p><strong>2.3.</strong> Nećemo koristiti vaše podatke u svrhe koje nisu navedene u ovoj Politici bez vaše saglasnosti.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
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
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="4. ZAŠTITA PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>4.1.</strong> Preduzimamo odgovarajuće mere zaštite kako bismo osigurali sigurnost vaših podataka.</p>
        <p><strong>4.2.</strong> Iako činimo sve da zaštitimo vaše podatke, ne možemo garantovati apsolutnu bezbednost.</p>
        <p><strong>4.3.</strong> Vi ste odgovorni za čuvanje svojih pristupnih podataka.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="5. PRAVA KORISNIKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>5.1.</strong> Imate pravo da pristupite, izmenite ili izbrišete svoje podatke.</p>
        <p><strong>5.2.</strong> Možete podneti zahtev za prekid obrade vaših podataka kontaktiranjem naše podrške.</p>
        <p><strong>5.3.</strong> Imate pravo da uložite prigovor na obradu podataka u skladu sa važećim zakonima.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="6. KOLAČIĆI I TEHNOLOGIJE PRAĆENJA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>6.1.</strong> Platforma koristi kolačiće za poboljšanje funkcionalnosti i personalizaciju iskustva.</p>
        <p><strong>6.2.</strong> Možete onemogućiti kolačiće u podešavanjima svog pretraživača.</p>
        <p><strong>6.3.</strong> Korišćenjem Platforme pristajete na upotrebu kolačića.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="7. IZMENI I AŽURIRANJE POLITIKE"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>7.1.</strong> Zadržavamo pravo da izmenimo ovu Politiku privatnosti u bilo kom trenutku.</p>
        <p><strong>7.2.</strong> O svim izmenama korisnici će biti blagovremeno obavešteni putem e-maila ili notifikacija na Platformi.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isLastMultiplePanel]="true" [showPanelHeader]="false">
        <panel-body [normalBottomPadding]="true">
          Za sva pitanja ili zahteve vezane za vašu privatnost, kontaktirajte nas na <strong>filiptrivan5&commat;gmail.com</strong>.
          <p>Hvala što koristite <strong>{{companyName}}</strong>!</p>
        </panel-body>
    </spider-panel>

  </div>
</div>


""";
        }

        private static string GetPrivacyPolicyComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { SpiderPanelsModule } from '@playerty/spider';
import { ConfigService } from 'src/app/business/services/config.service';

@Component({
  templateUrl: './privacy-policy.component.html',
  standalone: true,
  imports: [
    SpiderPanelsModule
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

    <spider-panel [isFirstMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="1. OPŠTE ODREDBE"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>1.1.</strong> Ovi uslovi korišćenja regulišu pravila i obaveze korisnika prilikom korišćenja <strong>{{companyName}}</strong> (dalje u tekstu: "Platforma").</p>
        <p><strong>1.2.</strong> Pristupom ili korišćenjem Platforme prihvatate ove uslove u celosti.</p>
        <p><strong>1.3.</strong> Ukoliko se ne slažete sa bilo kojim delom ovih uslova, molimo vas da ne koristite Platformu.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="2. REGISTRACIJA I KORIŠĆENJE PLATFORME"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>2.1.</strong> Da biste koristili Platformu, morate se registrovati i kreirati korisnički nalog.</p>
        <p><strong>2.2.</strong> Odgovorni ste za tačnost podataka koje unosite prilikom registracije.</p>
        <p><strong>2.3.</strong> Vaš nalog je ličan i nije dozvoljeno deljenje pristupnih podataka sa trećim licima.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="3. PRAVA I OBAVEZE KORISNIKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>3.1.</strong> Korisnik se obavezuje da će Platformu koristiti u skladu sa važećim zakonima i propisima.</p>
        <p><strong>3.2.</strong> Zabranjeno je zloupotrebljavati Platformu, uključujući, ali ne ograničavajući se na pokušaje neovlašćenog pristupa, manipulaciju podacima ili korišćenje Platforme u nezakonite svrhe.</p>
      </panel-body>
    </spider-panel>


    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="4. OGRANIČENJE ODGOVORNOSTI"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>4.1.</strong> Platforma se pruža "kao takva", bez ikakvih garancija.</p>
        <p><strong>4.2.</strong> Ne garantujemo neprekidan rad ili potpunu bezbednost podataka, ali preduzimamo sve razumne mere za njihovu zaštitu.</p>
        <p><strong>4.3.</strong> Ne snosimo odgovornost za bilo kakve gubitke ili štetu nastalu usled korišćenja Platforme.</p>
      </panel-body>
    </spider-panel>

    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="5. ZAŠTITA PRIVATNOSTI I PODATAKA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>5.1.</strong> Svi podaci korisnika biće obrađeni u skladu sa našom Politikom privatnosti.</p>
        <p><strong>5.2.</strong> Nećemo deliti vaše podatke sa trećim licima bez vašeg pristanka, osim ako je to zakonski obavezno.</p>
      </panel-body>
    </spider-panel>


    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="6. IZMENI I DOPUNE USLOVA KORIŠĆENJA"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>6.1.</strong> Zadržavamo pravo da u bilo kom trenutku izmenimo ili dopunimo ove uslove korišćenja.</p>
        <p><strong>6.2.</strong> O svim izmenama korisnici će biti blagovremeno obavešteni putem e-maila ili notifikacija na Platformi.</p>
      </panel-body>
    </spider-panel>


    <spider-panel [isMiddleMultiplePanel]="true">
      <panel-header [showBigTitle]="true" title="7. ZAVRŠNE ODREDBE"></panel-header>
      <panel-body [normalBottomPadding]="true">
        <p><strong>7.1.</strong> Ovi uslovi stupaju na snagu danom objavljivanja na Platformi.</p>
        <p><strong>7.2.</strong> Svi sporovi koji proisteknu iz korišćenja Platforme rešavaće se mirnim putem, a ukoliko to nije moguće, nadležan je sud u <strong>Beogradu</strong>.</p>
      </panel-body>
    </spider-panel>


    <spider-panel [isLastMultiplePanel]="true" [showPanelHeader]="false">
        <panel-body [normalBottomPadding]="true">
          Za sva pitanja ili nejasnoće, kontaktirajte nas na <strong>filiptrivan5&commat;gmail.com</strong>.
          <p>Hvala što koristite <strong>{{companyName}}</strong>!</p>
        </panel-body>
    </spider-panel>

  </div>
</div>
""";
        }

        private static string GetUserAgreementComponentTsData()
        {
            return $$"""
import { Component, OnInit } from '@angular/core';
import { SpiderPanelsModule } from '@playerty/spider';
import { ConfigService } from 'src/app/business/services/config.service';

@Component({
  templateUrl: './user-agreement.component.html',
  standalone: true,
  imports: [
    SpiderPanelsModule
  ]
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

        private static string GetLegalModuleTsData()
        {
            return $$"""
import { RouterModule, Routes } from "@angular/router";
import { NgModule } from "@angular/core";
import { PrivacyPolicyComponent } from "./privacy-policy/privacy-policy.component";
import { UserAgreementComponent } from "./user-agreement/user-agreement.component";

const routes: Routes = [
    {
        path: 'privacy-policy',
        component: PrivacyPolicyComponent,
    },
    {
        path: 'user-agreement',
        component: UserAgreementComponent,
    },
];

@NgModule({
    imports: [
        RouterModule.forChild(routes),
    ],
    declarations: [
    ],
    providers:[]
})
export class LegalModule { }

""";
        }

        private static string GetClientNotificationComponentHtmlData()
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
        <div [class]="(notification.isMarkedAsRead ? 'primary-lighter-color-background' : '') + ' transparent-card'" style="margin: 0px;">
          <div class="text-wrapper">
            <div class="header" style="margin-bottom: 10px; display: flex; justify-content: space-between; position: relative;">
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
      [first]="tableFilter.first"
      [rows]="tableFilter.rows" 
      [totalRecords]="currentUserNotifications?.totalRecords">
    </p-paginator>
    <div class="card-overflow-icon">
      <i class="pi pi-bell"></i>
    </div>
  </div>
</ng-container>
""";
        }

        private static string GetClientNotificationComponentTsData()
        {
            return $$"""
import { LayoutService } from './../../../business/services/layout/layout.service';
import { Component, OnInit, ViewChild } from '@angular/core';
import { ApiService } from 'src/app/business/services/api/api.service';
import { MenuItem } from 'primeng/api';
import { PaginatorState } from 'primeng/paginator';
import { TranslocoService } from '@jsverse/transloco';
import { Notification } from 'src/app/business/entities/business-entities.generated';
import { Menu } from 'primeng/menu';
import { TableResponse, TableFilter, TableFilterContext, SpiderMessageService } from '@playerty/spider';

@Component({
  templateUrl: './notification.component.html',
})
export class NotificationComponent implements OnInit {
  currentUserNotifications: TableResponse<Notification>;

  crudMenu: MenuItem[] = [];
  @ViewChild('menu') menu: Menu;
  lastMenuToggledNotification: Notification;

  tableFilter: TableFilter = new TableFilter({
    first: 0,
    rows: 10,
    filters: new Map<string, TableFilterContext[]>()
  });

  constructor(
    private apiService: ApiService,
    private translocoService: TranslocoService,
    private messageService: SpiderMessageService,
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
    this.tableFilter.first = event.first;
    this.tableFilter.rows = event.rows;
    this.getNotificationsForCurrentUser();
  }

  getNotificationsForCurrentUser(){
    this.apiService.getNotificationsForCurrentUser(this.tableFilter).subscribe((res) => {
      this.currentUserNotifications = res;
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
    this.layoutService.setUnreadNotificationsCountForCurrentUser().subscribe(); // FT: Don't need to unsubscribe from the http observable
  }

}

""";
        }

        private static string GetClientNotificationModuleTsData()
        {
            return $$"""
import { RouterModule, Routes } from "@angular/router";
import { NotificationComponent } from "./pages/notification.component";
import { NgModule } from "@angular/core";
import { TranslocoDirective } from "@jsverse/transloco";
import { PrimengModule, SpiderDataTableComponent, SpiderControlsModule, CardSkeletonComponent } from '@playerty/spider';

const routes: Routes = [
    {
        path: 'notifications',
        component: NotificationComponent,
    },
];

@NgModule({
    imports: [
        RouterModule.forChild(routes),
        PrimengModule,
        SpiderDataTableComponent,
        SpiderControlsModule,
        CardSkeletonComponent,
        TranslocoDirective,
    ],
    declarations: [
        NotificationComponent,
    ],
    providers:[]
})
export class NotificationModule { }

""";
        }

        private static string GetAppRoutingModuleTsData()
        {
            return $$"""
import { PreloadAllModules, RouterModule } from '@angular/router';
import { NgModule } from '@angular/core';
import { AuthGuard, NotAuthGuard, NotFoundComponent } from '@playerty/spider';
import { LayoutComponent } from './business/layout/layout.component';

@NgModule({
    imports: [
        RouterModule.forRoot([
            {
                path: '', 
                component: LayoutComponent,
                children: [
                    {
                        path: '',
                        loadChildren: () => import('./features/dashboard/dashboard.module').then(m => m.DashboardModule),
                        canActivate: [AuthGuard]
                    },
                    { 
                        path: 'administration',
                        loadChildren: () => import('./features/administration/administration.module').then(m => m.AdministrationModule),
                        canActivate: [AuthGuard]
                    },
                    { 
                        path: '',
                        loadChildren: () => import('./features/notification/notification.module').then(m => m.NotificationModule),
                        canActivate: [AuthGuard]
                    },
                ],
            },
            {
                path: '',
                children: [
                    { 
                        path: '',
                        loadChildren: () => import('@playerty/spider').then(m => m.AuthModule),
                        canActivate: [NotAuthGuard],
                    },
                ],
            },
            {
                path: '',
                children: [
                    { 
                        path: '',
                        loadChildren: () => import('./features/legal/legal.module').then(m => m.LegalModule),
                    },
                ],
            },
            // { path: 'landing', loadChildren: () => import('./layout/components/landing/landing.module').then(m => m.LandingModule) },
            { path: 'not-found', component: NotFoundComponent },
            { path: '**', redirectTo: 'not-found' },
        ], { scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled', onSameUrlNavigation: 'reload', preloadingStrategy: PreloadAllModules })
    ],
    exports: [RouterModule]
})
export class AppRoutingModule {
}

""";
        }

        private static string GetAppComponentHtmlData()
        {
            return $$"""
<!-- FT HACK: I don't know why, but translations on the layout component work only if we wrap everything with transloco -->
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
import { TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { PrimeNGConfig } from 'primeng/api';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {

    constructor(
        private primengConfig: PrimeNGConfig, 
        private translocoService: TranslocoService
    ) {

    }

    async ngOnInit() {
        this.primengConfig.ripple = true;

        this.translocoService.selectTranslateObject('Primeng').subscribe((primengTranslations) => {
            this.primengConfig.setTranslation(primengTranslations);
        });
    }
}

""";
        }

        private static string GetAppModuleTsData()
        {
            return $$"""
import { NgModule } from '@angular/core';
import { AppComponent } from './app.component';
import { NgxSpinnerModule } from 'ngx-spinner';
import { SocialAuthServiceConfig } from '@abacritt/angularx-social-login';
import { GoogleLoginProvider } from '@abacritt/angularx-social-login';
import { environment } from 'src/environments/environment';
import { BusinessModule } from './business/business.module';
import { TranslateLabelsService } from './business/services/translates/merge-labels';
import { ValidatorService } from './business/services/validators/validators';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { ConfigService } from './business/services/config.service';
import { AppRoutingModule } from './app-routing.module';
import { MessageService } from 'primeng/api';
import { AuthBaseService, ConfigBaseService, CoreModule, LayoutBaseService, SpiderTranslocoModule, TranslateLabelsAbstractService, ValidatorAbstractService } from '@playerty/spider';
import { LayoutService } from './business/services/layout/layout.service';

@NgModule({
  declarations: [
    AppComponent,
  ],
  imports: [
    AppRoutingModule,
    SpiderTranslocoModule.forRoot(),
    NgxSpinnerModule.forRoot({ type: 'ball-clip-rotate-multiple' }),
    BusinessModule,
    CoreModule,
  ],
  providers: [
    MessageService,
    {
      provide: 'SocialAuthServiceConfig',
      useValue: {
        autoLogin: false,
        providers: [
          {
            id: GoogleLoginProvider.PROVIDER_ID,
            provider: new GoogleLoginProvider(
              environment.googleClientId, 
              {
                scopes: 'email',
                oneTapEnabled: false,
                prompt: 'none',
                // plugin_name: 'the name of the Google OAuth project you created'
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
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
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
using Spider.Shared.Extensions;
using Spider.Shared.Resources;

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
using Spider.Shared.Attributes.EF;

namespace {{appName}}.Business.Entities
{
    public class UserNotification 
    {
        [M2MMaintanceEntity(nameof(Notification.Recipients))]
        public virtual Notification Notification { get; set; }

        [M2MEntity(nameof(User.Notifications))]
        public virtual UserExtended User { get; set; }

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

        private static string GetUserExtendedCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spider.Security.Entities;
using Spider.Security.Interfaces;
using Spider.Shared.Attributes;
using Spider.Shared.Attributes.EF;
using Spider.Shared.Attributes.EF.Translation;
using Spider.Shared.Attributes.EF.UI;
using Spider.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [Index(nameof(Email), IsUnique = true)]
    public class UserExtended : BusinessObject<long>, IUser
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
using Spider.Shared.Attributes;
using Spider.Shared.Attributes.EF.UI;
using Spider.Shared.Interfaces;
using Spider.Shared.DTO;
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
            {{appName}}BusinessService {{appName.FirstCharToLower()}}BusinessService, 
            BlobContainerClient blobContainerClient
        )
            : base(context, {{appName.FirstCharToLower()}}BusinessService, blobContainerClient)
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
        public async Task<TableResponseDTO<NotificationDTO>> GetNotificationsForCurrentUser(TableFilterDTO tableFilterDTO)
        {
            return await _{{appName.FirstCharToLower()}}BusinessService.GetNotificationsForCurrentUser(tableFilterDTO);
        }

    }
}

""";
        }

        private static string GetSecurityControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spider.Security.Interfaces;
using Spider.Security.Services;
using Spider.Security.SecurityControllers;
using Spider.Shared.Interfaces;
using Spider.Shared.Attributes;
using Spider.Shared.DTO;
using Microsoft.EntityFrameworkCore;
using Spider.Shared.Resources;
using Spider.Security.DTO;
using Spider.Shared.Extensions;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;

namespace {{appName}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class SecurityController : SecurityBaseController<UserExtended>
    {
        private readonly ILogger<SecurityController> _logger;
        private readonly SecurityBusinessService<UserExtended> _securityBusinessService;
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}BusinessService _{{appName.FirstCharToLower()}}BusinessService;


        public SecurityController(
            ILogger<SecurityController> logger, 
            SecurityBusinessService<UserExtended> securityBusinessService, 
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

        private static string GetUserExtendedControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spider.Shared.Attributes;
using Spider.Shared.Interfaces;
using Azure.Storage.Blobs;
using Spider.Shared.DTO;
using Spider.Shared.Resources;
using Spider.Security.Services;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Entities;

namespace {{appName}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class UserExtendedController : UserExtendedBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}BusinessService _{{appName.FirstCharToLower()}}BusinessService;
        private readonly AuthenticationService _authenticationService;

        public UserExtendedController(
            IApplicationDbContext context, 
            {{appName}}BusinessService {{appName.FirstCharToLower()}}BusinessService, 
            BlobContainerClient blobContainerClient, 
            AuthenticationService authenticationService
        )
            : base(context, {{appName.FirstCharToLower()}}BusinessService, blobContainerClient)
        {
            _context = context;
            _{{appName.FirstCharToLower()}}BusinessService = {{appName.FirstCharToLower()}}BusinessService;
            _authenticationService = authenticationService;
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserExtendedDTO> GetCurrentUserExtended()
        {
            long userId = _authenticationService.GetCurrentUserId();
            return await _{{appName.FirstCharToLower()}}BusinessService.GetUserExtendedDTO(userId, false); // FT: Don't need to authorize because he is current user
        }

    }
}

""";
        }

        private static string GetNotificationCsData(string appName)
        {
            return $$"""
using Spider.Shared.Attributes.EF;
using Spider.Shared.Attributes.EF.UI;
using Spider.Shared.BaseEntities;
using Spider.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using Spider.Shared.Interfaces;
using {{appName}}.Business.DTO;

namespace {{appName}}.Business.Entities
{
    public class Notification : BusinessObject<long>, INotification<UserExtended>
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
        [UITableColumn(nameof(UserExtendedDTO.Email))]
        [UITableColumn(nameof(UserExtendedDTO.CreatedAt))]
        #endregion
        [SimpleManyToManyTableLazyLoad]
        public virtual List<UserExtended> Recipients { get; } = new(); // M2M
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
using Spider.Shared.Attributes.EF.UI;

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
using Spider.Infrastructure;

namespace {{appName}}.Infrastructure
{
    public partial class {{appName}}ApplicationDbContext : ApplicationDbContext<UserExtended> // https://stackoverflow.com/questions/41829229/how-do-i-implement-dbcontext-inheritance-for-multiple-databases-in-ef7-net-co
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
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Nuget", "Nuget", "{D485BCE8-A950-457D-A710-566D559BD585}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.WebAPI", "{{appName}}.WebAPI\{{appName}}.WebAPI.csproj", "{1063DCDA-9291-4FAA-87B2-555E12511EE2}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Spider.Security", "..\..\..\SpiderFramework\spider-framework\Spider.Security\Spider.Security.csproj", "{3B328631-AB3B-4B28-9FA5-4DA790670199}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Spider.Shared", "..\..\..\SpiderFramework\spider-framework\Spider.Shared\Spider.Shared.csproj", "{53565A13-28F1-424F-B5A0-34125EF303CD}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Spider.Infrastructure", "..\..\..\SpiderFramework\spider-framework\Spider.Infrastructure\Spider.Infrastructure.csproj", "{587D08A6-A975-4673-90A4-77CF61B7B526}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Spider.SourceGenerators", "..\..\..\SpiderFramework\spider-framework\Spider.SourceGenerators\Spider.SourceGenerators.csproj", "{A30DFD0D-9EDD-4FD2-8CAF-85492EEEE6F1}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Infrastructure", "{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj", "{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Business", "Business", "{F2AA00F3-29C7-4A82-B4C0-5BD998C67912}"
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
		{3B328631-AB3B-4B28-9FA5-4DA790670199}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{3B328631-AB3B-4B28-9FA5-4DA790670199}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{3B328631-AB3B-4B28-9FA5-4DA790670199}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{3B328631-AB3B-4B28-9FA5-4DA790670199}.Release|Any CPU.Build.0 = Release|Any CPU
		{53565A13-28F1-424F-B5A0-34125EF303CD}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{53565A13-28F1-424F-B5A0-34125EF303CD}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{53565A13-28F1-424F-B5A0-34125EF303CD}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{53565A13-28F1-424F-B5A0-34125EF303CD}.Release|Any CPU.Build.0 = Release|Any CPU
		{587D08A6-A975-4673-90A4-77CF61B7B526}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{587D08A6-A975-4673-90A4-77CF61B7B526}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{587D08A6-A975-4673-90A4-77CF61B7B526}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{587D08A6-A975-4673-90A4-77CF61B7B526}.Release|Any CPU.Build.0 = Release|Any CPU
		{A30DFD0D-9EDD-4FD2-8CAF-85492EEEE6F1}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A30DFD0D-9EDD-4FD2-8CAF-85492EEEE6F1}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A30DFD0D-9EDD-4FD2-8CAF-85492EEEE6F1}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A30DFD0D-9EDD-4FD2-8CAF-85492EEEE6F1}.Release|Any CPU.Build.0 = Release|Any CPU
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
	GlobalSection(NestedProjects) = preSolution
		{3B328631-AB3B-4B28-9FA5-4DA790670199} = {D485BCE8-A950-457D-A710-566D559BD585}
		{53565A13-28F1-424F-B5A0-34125EF303CD} = {D485BCE8-A950-457D-A710-566D559BD585}
		{587D08A6-A975-4673-90A4-77CF61B7B526} = {D485BCE8-A950-457D-A710-566D559BD585}
		{A30DFD0D-9EDD-4FD2-8CAF-85492EEEE6F1} = {D485BCE8-A950-457D-A710-566D559BD585}
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C} = {F2AA00F3-29C7-4A82-B4C0-5BD998C67912}
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE} = {F2AA00F3-29C7-4A82-B4C0-5BD998C67912}
		{2D65E133-33C4-4169-A175-D744800941D6} = {F2AA00F3-29C7-4A82-B4C0-5BD998C67912}
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
-- FT: First you need to register the user in the app

begin transaction;

use {{appName}}

insert into Permission(Name, Description, Code) values(N'Pregled korisnika', null, N'ReadUserExtended');
insert into Permission(Name, Description, Code) values(N'Promena postojećih korisnika', null, N'UpdateUserExtended');
insert into Permission(Name, Description, Code) values(N'Brisanje korisnika', null, N'DeleteUserExtended');
insert into Permission(Name, Description, Code) values(N'Pregled notifikacija', null, N'ReadNotification');
insert into Permission(Name, Description, Code) values(N'Promena postojećih notifikacija', null, N'UpdateNotification');
insert into Permission(Name, Description, Code) values(N'Dodavanje novih notifikacija', null, N'InsertNotification');
insert into Permission(Name, Description, Code) values(N'Brisanje notifikacija', null, N'DeleteNotification');
insert into Permission(Name, Description, Code) values(N'Pregled uloga', null, N'ReadRole');
insert into Permission(Name, Description, Code) values(N'Promena postojećih uloga', null, N'UpdateRole');
insert into Permission(Name, Description, Code) values(N'Dodavanje novih uloga', null, N'InsertRole');
insert into Permission(Name, Description, Code) values(N'Brisanje uloga', null, N'DeleteRole');

INSERT INTO Role (Version, Name, CreatedAt, ModifiedAt) VALUES (1, N'Admin', getdate(), getdate());

DECLARE @AdminRoleId INT;
DECLARE @AdminUserId INT;
SELECT @AdminRoleId = Id FROM Role WHERE Name = N'Admin';
SELECT @AdminUserId = Id FROM [User] WHERE Id = 1;

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
using Spider.Shared.Helpers;
using Spider.Shared.Extensions;
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
        Spider.Infrastructure.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<Spider.Infrastructure.Settings>(_jsonConfigurationFile);
        Spider.Security.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<Spider.Security.Settings>(_jsonConfigurationFile);
        Spider.Shared.SettingsProvider.Current = Helper.ReadAssemblyConfiguration<Spider.Shared.Settings>(_jsonConfigurationFile);
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.SpiderConfigureServices<{{appName}}ApplicationDbContext>();
    }

    public void ConfigureContainer(IServiceContainer container)
    {
        container.RegisterInstance(container);

        container.RegisterFrom<CompositionRoot>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.SpiderConfigure(env);

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
        public string GoogleClientId { get; set; }

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

        private static string GetWebAPICsProjData(string appName)
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
		<PackageReference Include="NucleusFramework.Core" Version="6.1.9" />
        <PackageReference Include="Serilog.Extensions.Hosting" Version="9.0.0" />
        <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
        <PackageReference Include="Serilog.Sinks.ApplicationInsights" Version="4.0.0" />
        <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
		<PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.3.1" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Infrastructure\Spider.Infrastructure.csproj" />
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Security\Spider.Security.csproj" />
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Shared\Spider.Shared.csproj" />
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.SourceGenerators\Spider.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
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
	</ItemGroup>

	<ItemGroup>
	  <Folder Include="Helpers\" />
	</ItemGroup>

</Project>
""";
        }

        private static string GetWebAPIGeneratorSettingsData(string appName)
        {
            return $$"""
using Spider.Shared.Attributes;

namespace {{appName}}.WebAPI.GeneratorSettings
{
    public class GeneratorSettings
    {

    }
}
""";
        }

        private static string GetAppSettingsJsonData(string appName, string emailSender, string smtpUser, string smtpPass, string jwtKey, string blobStorageConnectionString, string blobStorageUrl)
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
      "GoogleClientId": "24372003240-44eprq8dn4s0b5f30i18tqksep60uk5u.apps.googleusercontent.com",
      "ExcelContentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    },
    "{{appName}}.Business": {
    },
    "Spider.Infrastructure": {
      "UseGoogleAsExternalProvider": true,
      "AppHasLatinTranslation": false
    },
    "Spider.Shared": {
      "ApplicationName": "{{appName}}",
      "EmailSender": "{{emailSender}}",
      "UnhandledExceptionRecipients": [
        "{{emailSender}}"
      ],
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": 587,
      "SmtpUser": "{{smtpUser}}",
      "SmtpPass": "{{smtpPass}}",
      "JwtKey": "{{jwtKey}}",
      "JwtIssuer": "https://localhost:7260;",
      "JwtAudience": "https://localhost:7260;",
      "ClockSkewMinutes": 1, // FT: Making it to 1 minute because of the SPA sends request exactly when it expires.
      "FrontendUrl": "http://localhost:4200",

      "BlobStorageConnectionString": "{{blobStorageConnectionString}}",
      "BlobStorageUrl": "{{blobStorageUrl}}",
      "BlobStorageContainerName": "files",

      "ConnectionString": "Data source=localhost\\SQLEXPRESS;Initial Catalog={{appName}};Integrated Security=True;Encrypt=false;MultipleActiveResultSets=True;",

      "RequestsLimitNumber": 70,
      "RequestsLimitWindow": 60
    },
    "Spider.Security": {
      "JwtKey": "{{jwtKey}}",
      "JwtIssuer": "https://localhost:7260;",
      "JwtAudience": "https://localhost:7260;",
      "ClockSkewMinutes": 1, // FT: Making it to 1 minute because of the SPA sends request exactly when it expires. 
      "AccessTokenExpiration": 20,
      "RefreshTokenExpiration": 1440, // 24 hours
      "VerificationTokenExpiration": 5,
      "NumberOfFailedLoginAttemptsInARowToDisableUser": 40, // FT: I think we don't need this check, maybe delete in the future
      "AllowTheUseOfAppWithDifferentIpAddresses": true,
      "AllowedBrowsersForTheSingleUser": 5,
      "GoogleClientId": "24372003240-44eprq8dn4s0b5f30i18tqksep60uk5u.apps.googleusercontent.com",
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
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:5173",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:7068;http://localhost:5173",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
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
using Spider.Security.Interfaces;
using Spider.Shared.Excel;
using Spider.Security.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Spider.Shared.Emailing;
using {{appName}}.Business.Services;
using {{appName}}.Business.Entities;
using {{appName}}.Shared.FluentValidation;

namespace {{appName}}.WebAPI.DI
{
    public class CompositionRoot : ICompositionRoot
    {
        public virtual void Compose(IServiceRegistry registry)
        {
            // Framework
            registry.Register<AuthenticationService>();
            registry.Register<AuthorizationService>();
            registry.Register<SecurityBusinessService<UserExtended>>();
            registry.Register<Spider.Security.Services.BusinessServiceGenerated<UserExtended>>();
            registry.Register<Spider.Security.Services.AuthorizationBusinessService<UserExtended>>();
            registry.Register<Spider.Security.Services.AuthorizationBusinessServiceGenerated<UserExtended>>();
            registry.Register<ExcelService>();
            registry.Register<EmailingService>();
            registry.RegisterSingleton<IConfigureOptions<MvcOptions>, TranslatePropertiesConfiguration>();
            registry.RegisterSingleton<IJwtAuthManager, JwtAuthManagerService>();

            // Business
            registry.Register<{{appName}}.Business.Services.{{appName}}BusinessService>();
            registry.Register<{{appName}}.Business.Services.BusinessServiceGenerated>();
            registry.Register<{{appName}}.Business.Services.AuthorizationBusinessService>();
            registry.Register<{{appName}}.Business.Services.AuthorizationBusinessServiceGenerated>();
        }
    }
}
""";
        }

        private static string GetSharedCsProjData()
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Shared\Spider.Shared.csproj" />
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
	</ItemGroup>

</Project>

""";
        }

        private static string GetInfrastructureCsProjData(string appName)
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
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Infrastructure\Spider.Infrastructure.csproj" />
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Security\Spider.Security.csproj" />
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Shared\Spider.Shared.csproj" />
		<ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.SourceGenerators\Spider.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
		<ProjectReference Include="..\{{appName}}.Business\{{appName}}.Business.csproj" />
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

        private static string GetBusinessCsProjData(string appName)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.Security\Spider.Security.csproj" />
    <ProjectReference Include="..\..\..\..\SpiderFramework\spider-framework\Spider.SourceGenerators\Spider.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="DataMappers\" />
    <Folder Include="DTO\Helpers" />
    <Folder Include="DTO\Partials" />
    <Folder Include="Entities\" />
    <Folder Include="Enums\" />
    <Folder Include="Services\" />
    <Folder Include="ValidationRules\" />
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
	</ItemGroup>

    <ItemGroup>
      <EmbeddedResource Include="Terms.resx">
        <Generator>PublicResXFileCodeGenerator</Generator>
        <LastGenOutput>Terms.Designer.cs</LastGenOutput>
      </EmbeddedResource>
      <EmbeddedResource Include="TermsGenerated.resx">
        <Generator>PublicResXFileCodeGenerator</Generator>
        <LastGenOutput>TermsGenerated.Designer.cs</LastGenOutput>
      </EmbeddedResource>
    </ItemGroup>

</Project>
""";
        }

        private static string GetBusinessGeneratorSettingsData(string appName)
        {
            return $$"""
using Spider.Shared.Attributes;

namespace {{appName}}.Business.GeneratorSettings
{
    public class GeneratorSettings
    {
        
    }
}
""";
        }

        private static string GetAuthorizationServiceCsData(string appName)
        {
            return $$"""
using Azure.Storage.Blobs;
using Spider.Security.Services;
using Spider.Shared.Interfaces;
using Spider.Shared.Extensions;
using Spider.Shared.Exceptions;
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
            AuthenticationService authenticationService, 
            BlobContainerClient blobContainerClient
        )
            : base(context, authenticationService, blobContainerClient)
        {
            _context = context;
            _authenticationService = authenticationService;
        }

        #region UserExtended

        public override async Task AuthorizeUserExtendedReadAndThrow(long? userExtendedId)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminReadPermission = await IsAuthorizedAsync<UserExtended>(BusinessPermissionCodes.ReadUserExtended);
                bool isCurrentUser = _authenticationService.GetCurrentUserId() == userExtendedId;

                if (isCurrentUser == false && hasAdminReadPermission == false)
                    throw new UnauthorizedException();
            });
        }

        public override async Task AuthorizeUserExtendedUpdateAndThrow(UserExtendedDTO userExtendedDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminUpdatePermission = await IsAuthorizedAsync<UserExtended>(BusinessPermissionCodes.UpdateUserExtended);
                if (hasAdminUpdatePermission)
                    return;

                long currentUserId = _authenticationService.GetCurrentUserId();
                if (currentUserId != userExtendedDTO.Id)
                    throw new UnauthorizedException();

                UserExtended userExtended = await GetInstanceAsync<UserExtended, long>(userExtendedDTO.Id, null);

                if (
                    userExtendedDTO.IsDisabled != userExtended.IsDisabled ||
                    userExtendedDTO.HasLoggedInWithExternalProvider != userExtended.HasLoggedInWithExternalProvider
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
using Spider.Shared.DTO;
using Spider.Shared.Excel;
using Spider.Shared.Interfaces;
using Spider.Shared.Extensions;
using Spider.Shared.Helpers;
using Spider.Security.DTO;
using Spider.Security.Services;
using Spider.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Mapster;
using FluentValidation;
using Spider.Shared.Emailing;
using Azure.Storage.Blobs;

namespace {{appName}}.Business.Services
{
    public class {{appName}}BusinessService : {{appName}}.Business.Services.BusinessServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly {{appName}}.Business.Services.AuthorizationBusinessService _authorizationService;
        private readonly AuthenticationService _authenticationService;
        private readonly SecurityBusinessService<UserExtended> _securityBusinessService;
        private readonly EmailingService _emailingService;

        public {{appName}}BusinessService(
            IApplicationDbContext context, 
            ExcelService excelService, 
            {{appName}}.Business.Services.AuthorizationBusinessService authorizationService, 
            SecurityBusinessService<UserExtended> securityBusinessService, 
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
        /// FT: IsDisabled is handled inside authorization service
        /// </summary>
        protected override async Task OnBeforeSaveUserExtendedAndReturnSaveBodyDTO(UserExtendedSaveBodyDTO userExtendedSaveBodyDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                if (userExtendedSaveBodyDTO.UserExtendedDTO.Id <= 0)
                    throw new HackerException("You can't add new user.");

                UserExtended userExtended = await GetInstanceAsync<UserExtended, long>(userExtendedSaveBodyDTO.UserExtendedDTO.Id, userExtendedSaveBodyDTO.UserExtendedDTO.Version);

                if (userExtendedSaveBodyDTO.UserExtendedDTO.Email != userExtended.Email ||
                    userExtendedSaveBodyDTO.UserExtendedDTO.HasLoggedInWithExternalProvider != userExtended.HasLoggedInWithExternalProvider
                //userExtendedSaveBodyDTO.UserExtendedDTO.AccessedTheSystem != userExtended.AccessedTheSystem
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
                await _authorizationService.AuthorizeAndThrowAsync<UserExtended>(BusinessPermissionCodes.UpdateNotification);

                // FT: Checking version because if the user didn't save and some other user changed the version, he will send emails to wrong users
                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                List<string> recipients = notification.Recipients.Select(x => x.Email).ToList();

                await _emailingService.SendEmailAsync(recipients, notification.Title, notification.EmailBody);
            });
        }

        /// <summary>
        /// FT: Don't need authorization because user can do whatever he wants with his notifications
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
        /// FT: Don't need authorization because user can do whatever he wants with his notifications
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
        /// FT: Don't need authorization because user can do whatever he wants with his notifications
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

        public async Task<TableResponseDTO<NotificationDTO>> GetNotificationsForCurrentUser(TableFilterDTO tableFilterDTO)
        {
            TableResponseDTO<NotificationDTO> result = new();
            long currentUserId = _authenticationService.GetCurrentUserId(); // FT: Not doing user.Notifications, because he could have a lot of them.

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
                    .Skip(tableFilterDTO.First)
                    .Take(tableFilterDTO.Rows)
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
using Spider.Shared.Attributes;

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

        private static string GetTsConfigJsonData()
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "compileOnSave": false,
  "compilerOptions": {
    "baseUrl": "./",
    "paths": {
      "@playerty/spider": ["../../../SpiderFramework/spider-framework/Angular/projects/spider/src/public-api"]
    },
    "outDir": "./dist/out-tsc",
    "forceConsistentCasingInFileNames": true,
    "strict": false,
    "noImplicitOverride": true,
    "noPropertyAccessFromIndexSignature": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true,
    "sourceMap": true,
    "declaration": false,
    "downlevelIteration": true,
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

        private static string GetPackageJsonData(string appName)
        {
            return $$"""
{
    "name": "{{appName.ToLower()}}.spa",
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
        "@abacritt/angularx-social-login": "2.2.0",
        "@angular/animations": "17.3.12",
        "@angular/cdk": "17.3.10",
        "@angular/common": "17.3.12",
        "@angular/compiler": "17.3.12",
        "@angular/core": "17.3.12",
        "@angular/forms": "17.3.12",
        "@angular/platform-browser": "17.3.12",
        "@angular/platform-browser-dynamic": "17.3.12",
        "@angular/router": "17.3.12",
        "@jsverse/transloco": "7.5.0",
        "@jsverse/transloco-preload-langs": "7.0.1",
        "@playerty/spider": "latest",
        "file-saver": "2.0.5",
        "json-parser": "3.1.2",
        "ngx-spinner": "17.0.0",
        "primeflex": "3.3.1",
        "primeicons": "7.0.0",
        "primeng": "17.18.9",
        "quill": "2.0.2",
        "rxjs": "7.8.1",
        "tslib": "2.3.0",
        "webpack-dev-server": "4.15.1",
        "zone.js": "0.14.10"
    },
    "devDependencies": {
        "@angular-devkit/build-angular": "17.3.11",
        "@angular/cli": "17.3.11",
        "@angular/compiler-cli": "17.3.12",
        "@jsverse/transloco-keys-manager": "5.1.0",
        "@types/jasmine": "5.1.0",
        "jasmine-core": "5.1.0",
        "karma": "6.4.0",
        "karma-chrome-launcher": "3.2.0",
        "karma-coverage": "2.2.0",
        "karma-jasmine": "5.1.0",
        "karma-jasmine-html-reporter": "2.1.0",
        "typescript": "5.2.2"
    }
}
""";
        }

        private static string GetPlopFileJsData()
        {
            return $$$"""
module.exports = function (plop) {
  plop.setHelper('toKebab', function (text) {
      return text
        .replace(/([a-z])([A-Z])/g, '$1-$2')
        .replace(/\s+/g, '-')
        .toLowerCase();
  });

  plop.setHelper('firstCharToLower', function (text) {
      return text.charAt(0).toLowerCase() + text.slice(1);
  });

  plop.setGenerator('generate-complete', {
    description: 'Generate complete',
    prompts: [
      {
        type: 'input',
        name: 'filenames',
        message: 'Write entity names (comma-separated): ',
      }
    ],
    actions: function (data) {
      const filenames = data.filenames.split(',').map(name => name.trim());
      let actions = [];

      filenames.forEach(filename => {
        actions.push(
          {
            type: 'add',
            path: 'plop/output/{{filename}}/{{toKebab filename}}-details.component.html',
            templateFile: 'plop/spider-details-html-template.hbs',
            data: {filename}
          },
          {
            type: 'add',
            path: 'plop/output/{{filename}}/{{toKebab filename}}-details.component.ts',
            templateFile: 'plop/spider-details-ts-template.hbs',
            data: {filename}
          },
          {
            type: 'add',
            path: 'plop/output/{{filename}}/{{toKebab filename}}-table.component.html',
            templateFile: 'plop/spider-table-html-template.hbs',
            data: {filename}
          },
          {
            type: 'add',
            path: 'plop/output/{{filename}}/{{toKebab filename}}-table.component.ts',
            templateFile: 'plop/spider-table-ts-template.hbs',
            data: {filename}
          },
          {
            type: 'add',
            path: 'plop/output/{{filename}}/{{filename}}Controller.cs',
            templateFile: 'plop/spider-controller-cs-template.hbs',
            data: {filename}
          },
        );
      });

      return actions;
    } 
  });
};
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
    "{{appName}}.SPA": {
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
            "outputPath": "dist/{{appName}}.SPA",
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
              "src/assets/styles.scss",
              "node_modules/ngx-spinner/animations/ball-clip-rotate-multiple.css"
            ],
            "scripts": []
          },
          "configurations": {
            "production": {
              "budgets": [
                {
                  "type": "initial",
                  "maximumWarning": "1mb",
                  "maximumError": "2mb"
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
              "buildTarget": "{{appName}}.SPA:build:production"
            },
            "development": {
              "buildTarget": "{{appName}}.SPA:build:development"
            }
          },
          "defaultConfiguration": "development"
        },
        "extract-i18n": {
          "builder": "@angular-devkit/build-angular:extract-i18n",
          "options": {
            "buildTarget": "{{appName}}.SPA:build"
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
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';


platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.error(err));
""";
        }

        private static string GetIndexHtmlData(string appName)
        {
            return $$"""
<!doctype html>
<html lang="sr-Latn-RS">
<head>
  <meta charset="utf-8">
  <title>{{appName.ToTitleCase()}}</title>
  <meta name="description" content="{{appName.ToTitleCase()}}">
  <meta name="author" content="{{appName.ToTitleCase()}}">
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <link rel="icon" type="image/x-icon" href="./assets/images/logo/favicon.ico">
</head>
<body>
  <app-root></app-root>
</body>
</html>
""";
        }

        private static string GetEnvironmentTsCode(string appName, string primaryColor)
        {
            return $$"""
// FT: In environment putting only the variables which are different in dev and prod, and which the client would change ocasionaly so we don't need to redeploy the app
export const environment = {
  production: false,
  apiUrl: 'https://localhost:44388/api',
  frontendUrl: 'http://localhost:4200',
  googleClientId: '24372003240-44eprq8dn4s0b5f30i18tqksep60uk5u.apps.googleusercontent.com',
  companyName: '{{appName.ToTitleCase()}}',
  primaryColor: '{{primaryColor}}',
};
""";
        }

        private static string GetStylesScssCode()
        {
            return $$"""
//#region PrimeNG

@import "../../node_modules/primeng/resources/primeng.min.css";
$gutter: 1rem; // FT: For primeflex grid system, it needs to be rigth above primeflex import!
@import "../../node_modules/primeflex/primeflex.scss";
@import "../../node_modules/primeicons/primeicons.css";
/* PrimeNG editor */
@import "../../node_modules/quill/dist/quill.core.css";
@import "../../node_modules/quill/dist/quill.snow.css";

//#endregion

//#region Spider

@import "../../../../../SpiderFramework/spider-framework/Angular/projects/spider/src/lib/styles/styles.scss";
// @import "../../node_modules/@playerty/spider/styles/styles/styles.scss";

//#endregion

@import "shared.scss";
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
    "SelectAColor": "Odaberite boju",
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
    "UserExtendedDTO": "/",
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
    "UserExtended": "Korisnik",
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
    "YouDoNotHaveAnyNotifications": "Nemate nijednu notifikaciju.",
    "More than": "Više od",
    "BadRequestDetails": "Sistem ne može da obradi zahtev. Molimo vas da proverite zahtev i pokušate ponovo."
}
""";
        }

        private static string GetTranslocoEnJsonCode()
        {
            return $$"""
{

}
""";
        }

        private static string GetBusinessModuleTsData()
        {
            return $$"""
import { CommonModule } from '@angular/common';
import { NgModule, Optional, SkipSelf } from '@angular/core';

@NgModule({
  declarations: [],
  imports: [CommonModule],
  providers: [
  ],
})
export class BusinessModule {
  constructor(@Optional() @SkipSelf() Business: BusinessModule) {
    if (Business) {
      throw new Error('Business Module can only be imported to AppModule.');
    }
  }
}
""";
        }

        private static string GetValidatorsTsCode()
        {
            return $$"""
import { TranslocoService } from '@jsverse/transloco';
import { Injectable } from '@angular/core';
import { ValidatorServiceGenerated } from "./validators.generated";
import { ValidatorAbstractService, SpiderFormControl, SpiderValidatorFn } from '@playerty/spider';

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

    override setValidator = (formControl: SpiderFormControl, className: string): SpiderValidatorFn => {
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
import { ConfigBaseService } from '@playerty/spider';

@Injectable({
  providedIn: 'root',
})
export class ConfigService extends ConfigBaseService
{
    override production: boolean = environment.production;
    override apiUrl: string = environment.apiUrl;
    override frontendUrl: string = environment.frontendUrl;
    override googleClientId: string = environment.googleClientId;
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
import { TranslateLabelsAbstractService } from '@playerty/spider';

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
import { Injectable, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ApiService } from 'src/app/business/services/api/api.service';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { ConfigService } from '../config.service';
import { AuthBaseService } from '@playerty/spider';

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
  ) {
    super(router, http, externalAuthService, apiService, config);
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
import { LayoutBaseService } from '@playerty/spider';
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

        private static string GetSpiderControllerCsTemplateHbsData(string appName)
        {
            return $$$"""
using Microsoft.AspNetCore.Mvc;
using Spider.Shared.Attributes;
using Spider.Shared.Interfaces;
using Azure.Storage.Blobs;
using Spider.Security.Services;
using {{{appName}}}.Business.Services;
using {{{appName}}}.Business.DTO;

namespace {{{appName}}}.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class {{filename}}Controller : {{filename}}BaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly {{{appName}}}BusinessService _{{{appName.FirstCharToLower()}}}BusinessService;
        private readonly AuthenticationService _authenticationService;

        public {{filename}}Controller(
            IApplicationDbContext context, 
            {{{appName}}}BusinessService {{{appName.FirstCharToLower()}}}BusinessService, 
            BlobContainerClient blobContainerClient, 
            AuthenticationService authenticationService
        )
            : base(context, {{{appName.FirstCharToLower()}}}BusinessService, blobContainerClient)
        {
            _context = context;
            _{{{appName.FirstCharToLower()}}}BusinessService = {{{appName.FirstCharToLower()}}}BusinessService;
            _authenticationService = authenticationService;
        }

    }
}

""";
        }

        private static string GetSpiderDetailsHtmlTemplateHbsData()
        {
            return $$$"""
<ng-container *transloco="let t">
    <spider-card [title]="t('{{filename}}')" icon="pi pi-file-edit">

        <{{toKebab filename}}-base-details
        [formGroup]="formGroup" 
        [{{firstCharToLower filename}}FormGroup]="{{firstCharToLower filename}}FormGroup" 
        (onSave)="onSave()"
        [getCrudMenuForOrderedData]="getCrudMenuForOrderedData"
        />

    </spider-card>
</ng-container>
""";
        }

        private static string GetSpiderDetailsTsTemplateHbsData()
        {
            return $$$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { ApiService } from 'src/app/business/services/api/api.service';
import { {{filename}} } from 'src/app/business/entities/business-entities.generated';
import { BaseFormCopy, SpiderFormGroup, SpiderMessageService, BaseFormService } from '@playerty/spider';

@Component({
    selector: '{{toKebab filename}}-details',
    templateUrl: './{{toKebab filename}}-details.component.html',
    styles: [],
})
export class {{filename}}DetailsComponent extends BaseFormCopy implements OnInit {
    {{firstCharToLower filename}}FormGroup = new SpiderFormGroup<{{filename}}>({});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderMessageService, 
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

        private static string GetSpiderTableHtmlTemplateHbsData()
        {
            return $$$"""
<ng-container *transloco="let t">

    <spider-data-table [tableTitle]="t('{{filename}}List')" 
    [cols]="cols" 
    [getTableDataObservableMethod]="get{{filename}}TableDataObservableMethod" 
    [exportTableDataToExcelObservableMethod]="export{{filename}}TableDataToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="delete{{filename}}ObservableMethod"
    [showAddButton]="true"
    ></spider-data-table>

</ng-container>
""";
        }

        private static string GetSpiderTableTsTemplateHbsData()
        {
            return $$$"""
import { ApiService } from 'src/app/business/services/api/api.service';
import { TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { Column } from '@playerty/spider';
import { {{filename}} } from 'src/app/business/entities/business-entities.generated';

@Component({
    selector: '{{toKebab filename}}-table',
    templateUrl: './{{toKebab filename}}-table.component.html',
    styles: []
})
export class {{filename}}TableComponent implements OnInit {
    cols: Column<{{filename}}>[];

    get{{filename}}TableDataObservableMethod = this.apiService.get{{filename}}TableData;
    export{{filename}}TableDataToExcelObservableMethod = this.apiService.export{{filename}}TableDataToExcel;
    delete{{filename}}ObservableMethod = this.apiService.delete{{filename}};

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Actions'), actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name:  this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
            {name: this.translocoService.translate('Id'), filterType: 'numeric', field: 'id'},
        ]
    }
}
""";
        }

        private static string GetLayoutComponentHtmlCode()
        {
            return $"""
<div class="layout-wrapper" [ngClass]="containerClass">
    <topbar></topbar>
    <div class="layout-sidebar">
        <sidebar [menu]="menu"></sidebar>
    </div>
    <div class="layout-main-container">
        <div class="layout-main">
            <router-outlet></router-outlet>
        </div>
        <footer></footer>
    </div>
    <div class="layout-mask"></div>
</div>
""";
        }

        private static string GetLayoutComponentTsCode()
        {
            return $$"""
import { TranslocoService } from '@jsverse/transloco';
import { Component, OnDestroy, OnInit, Renderer2 } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { ConfigService } from 'src/app/business/services/config.service';
import { Subscription } from 'rxjs';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { FooterComponent, LayoutBaseComponent, AppSidebarComponent, AppTopBarComponent, LayoutBaseService, PrimengModule, SpiderMenuItem} from '@playerty/spider';
import { CommonModule } from '@angular/common';
import { BusinessPermissionCodes } from '../enums/business-enums.generated';
import { SecurityPermissionCodes } from '@playerty/spider';

@Component({
    selector: 'layout',
    templateUrl: './layout.component.html',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        HttpClientModule,
        RouterModule,
        PrimengModule,
        FooterComponent,
        AppSidebarComponent,
        AppTopBarComponent,
    ]
})
export class LayoutComponent extends LayoutBaseComponent implements OnInit, OnDestroy {
    menu: SpiderMenuItem[];

    constructor(
        protected override layoutService: LayoutBaseService, 
        protected override renderer: Renderer2, 
        protected override router: Router,
        private authService: AuthService,
        private config: ConfigService,
        private translocoService: TranslocoService
    ) {
        super(layoutService, renderer, router);
    }

    ngOnInit(): void {
        this.menu = [
            {
                visible: true,
                items: [
                    { 
                        label: this.translocoService.translate('Home'), 
                        icon: 'pi pi-fw pi-home', 
                        routerLink: [''],
                        visible: true,
                    },
                    {
                        label: this.translocoService.translate('Administration'),
                        icon: 'pi pi-fw pi-cog',
                        visible: true,
                        hasPermission: (permissionCodes: string[]): boolean => { 
                            return (
                                permissionCodes?.includes(BusinessPermissionCodes.ReadUserExtended) ||
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
                                        permissionCodes?.includes(BusinessPermissionCodes.ReadUserExtended)
                                    )
                                },
                                visible: true,
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
                                visible: true,
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
                                visible: true,
                            },
                        ]
                    },
                ]
            },
        ];
    }

    override onAfterNgDestroy = () => {
        
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

        private static string GetMitLicenseData()
        {
            return $$"""
MIT License

Copyright (c) 2024 Filip Trivan

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
""";
        }

        private static string GetFaviconIcoData()
        {
            return """
    00     ¨%  6          ¨  Þ%       h  †6  (   0   `           $                                                                                                          Ið'J
ó²J
ó²Ið'                                                                                                                                                                        SòEôˆF	ôþH
öÿL	õÿMòÿPó™Sò                                                                                                                                                        Ì ÏKïcDòéF	ôþH
öÿL	õÿL	õÿQôÿQôÿTòîZðwŽ÷                                                                                                                                            bîFE	ïÒF	ôþF	ôþF	ôþH
öÿL	õÿL	õÿQôÿQôÿVòÿ\îÿcêØbîF                                                                                                                                Ið'J
ó²MòÿMòÿF	ôþF	ôþH
öÿH
öÿL	õÿQôÿQôÿVòÿYðÿaíÿféÿmæÿsâ»{ä*                                                                                                                SòSí‘QïùMòÿMòÿMòÿL	õÿL	õÿH
öÿL	õÿL	õÿQôÿQôÿVòÿ\îÿféÿi	éÿqãÿvàÿ{Þÿ…×™žÕ                                                                                                    KïcZîïVñÿVñÿVñÿVñÿP
õÿP
õÿP
õÿP
õÿP
õÿQôÿQôÿVòÿYðÿaíÿféÿmæÿqãÿ{Þÿ‚ÙÿŒ
Óÿ”Îí®ØX                                                                                                Zî…\ñÿ\ñÿ\ñÿVñÿVñÿVöÿVöÿVöÿP
õÿP
õÿQôÿQôÿVòÿ\îÿaíÿi	éÿqãÿvàÿ{Þÿ‚ÙÿŒ
Óÿ–ÌÿŸÇ                                                                                                ð_ðù\ñÿ\ñÿ\ñÿ\öÿ\öÿVöÿVöÿVöÿVöÿVòÿVòÿYðÿaíÿféÿmæÿqãÿ{Þÿ‚ÙÿŒ
Óÿ–ÌÿÈüžÕ                                                                                                    bñ±dñÿdñÿböÿ\öÿ\öÿ\öÿ\öÿ[
õÿ[
õÿ[
õÿ[
õÿaíÿaíÿi	éÿqãÿvàÿ{Þÿ‚ÙÿŒ
Óÿ–Ìÿ¡Ä¥                                                                                                        bîFgõÿgõÿe÷ÿböÿböÿböÿböÿaôÿaôÿ_
òÿ_
òÿaíÿi	éÿmæÿqãÿvàÿ‚ÙÿŒ
Óÿ–ÌÿÈü³Ì:                                                                                                            ióØgõÿgõÿgõÿe÷ÿe÷ÿböÿböÿaôÿaôÿdñÿi	éÿmæÿqãÿvàÿ{Þÿ‚ÙÿŒ
Óÿ–Ìÿ 
ÆÕÌ Ï                                                                                                            hòQh÷—h÷—h÷—h÷—h÷—h÷—hò‘hò‘hò‘hò‘uæŒuæŒuæŒuæŒ‰	Õˆ‰	Õˆ‰	ÕˆŸÇ¦ÀE                                                                                                                                                                                                                                                                    ó7                                                                                                                                                                íŽ&                    Sòƒ ð¿                                                                                                                                                                ð‰™ð
ˆ                {ä*–%ôýqî-                                                                                                                                                        é•ï€ùé•                bîF›'õÿƒó¯                                                                                                                                                        òyŒð	~ÿóy*                {ñY›'õÿ–%ôýð                                                                                                                                                ð€ñ	|ðð{ÿñ‚6                ‹ öj¡)öÿ›'õÿ‚ñ¡                                                                                                                                                ð†|ð	~ÿð{ÿñwK                „ó€¡)öÿ¡)öÿ›&ôó}õ                                        }õ… ÷ê‰ôô‰ôô‰ôôíóíó•çò•çò›ßò¥Øñ¥Øñ®Íæ×¸                                        ð
ˆí€éð{ÿòwÿï[                †ó“§+øÿ§+øÿ¡)öÿ†ó“                                            ƒó¯!õÿ!õÿ“ ðÿ“ ðÿ—éÿ—éÿŸäÿ¥Þÿ¥Þÿ®Õÿº Ðž                                            ñ
ƒlð	~ÿð{ÿòwÿñ
ƒl                —!ô¦¬,ùÿ§+øÿ§+øÿ›&ôóŽ÷                                        ó7!õÿ“ ðÿ“ ðÿ™!îÿ›éÿ›éÿŸäÿ¥Þÿ®Õÿ®Õÿ³Ì:                                        Ì Ïì
Ûð	~ÿòwÿòwÿór{                ‘!óº¬,ùÿ¬,ùÿ¬,ùÿ§+øÿ„ó€                                            ‘ôÕ–%ôý™!îÿ™!îÿ›éÿŸäÿ¥Þÿ­!Þÿ®Õÿ½$ØÊ                                            ï[ñ
ƒÿð{ÿòwÿôrÿòyŒ                ›%øÈ±-úÿ±-úÿ¬,ùÿ¬,ùÿŸ&øäÌ Ï                                        “ôoŸ#ïÿŸ#ïÿŸ#ïÿ¤!èÿ¤!èÿ­!Þÿ­!Þÿµ#Úÿ¶Ël                                            ë‰Ïñ
ƒÿð	~ÿòwÿôrÿõn›                %öØ´.ûÿ´.ûÿ±-úÿ¬,ùÿ¬,ùÿ‹ öj                                        }õ›&ôóŸ#ïÿ¦%íÿ¤!èÿ«$èÿ­!Þÿµ#Úÿ¸"Õñ×¸                                        ïIð‰ÿñ
ƒÿð	~ÿòwÿôrÿõ k«                Ÿ&øä¸.üÿ´.ûÿ´.ûÿ±-úÿ±-úÿŸ&øä                                            —!ô¦¦%íÿ¦%íÿ«$èÿ±%äÿµ#Úÿµ#Úÿº Ðž                                            îÁðŒÿñ†ÿð	~ÿòwÿôrÿöoÅ                ³)ûë»/üÿ¸.üÿ¸.üÿ´.ûÿ´.ûÿ±-úÿ’ øS                                        ™ð)¦%íÿ«$èÿ«$èÿ±%äÿµ#Úÿ½&ÜÿÀ!Õ*                                        ñ‚6í“ÿðŒÿñ†ÿòÿó	|ÿôrÿöoÅ                ´,ùö¿/ýÿ»/üÿ»/üÿ¸.üÿµ.ýÿ´.ûÿ'úÒ                                            ¨#óË±'êÿ±%äÿ¹'ãÿ½&Üÿ½$ØÊ                                            íš­í“ÿðŒÿð‰ÿòÿó	|ÿòwÿòtÞ                ¼-üûÂ0ýÿ¿/ýÿ¿/ýÿ»/üÿ¸.üÿ¸.üÿµ.ýÿ”ù=                                        §ìe±'êÿ¹'ãÿ¹'ãÿ½&ÜÿÂ!Õ]                                        íŽ&ì›þí“ÿïÿð‰ÿñ
ƒÿó	|ÿó	|ÿñ	|ð            Ì ÏÆ0þÿÆ0þÿÂ0ýÿÂ0ýÿ¿/ýÿ¿/ýÿ»/üÿ¸.üÿ›%øÈ                                        Ì Ï»'ìâ¹'ãÿ¿*äþÂ'ÛíŽ÷                                        íš­ì›þî˜ÿïÿð‰ÿñ
ƒÿòÿó	|ÿó	|ÿÌ Ï        ðÌ0þÿÊ0ýÿÆ0þÿÆ0þÿÂ0ýÿ¿/ýÿ¿/ýÿ»/üÿµ.ýÿŒù*                                        ²#ã™¿*äþÃ*áþº Ðž                                        é•ê¡øì›þî˜ÿïÿðŒÿñ†ÿòÿòÿó	|ÿð€        ó7Ï0þÿÌ0þÿÊ0ýÿÆ0þÿÆ0þÿÂ0ýÿÂ0ýÿ¿/ýÿ¿/ýÿ¢#úº                                        ¸ Ø ¿*äþÃ*áþ¸ Ø                                         ë¢—ì£ÿíŸÿî˜ÿð“ÿðŒÿð‰ÿñ†ÿñ
ƒÿòÿíŽ&        ’ øSÒ0þÿÏ0þÿÌ0þÿÌ0þÿÈ0þÿÆ0þÿÂ0ýÿÂ0ýÿ¿/ýÿ¼-üûð                                        ½$ØÊ½$ØÊ                                        ×¸é!¬ðì£ÿíŸÿî›ÿð“ÿïÿðŒÿð‰ÿñ†ÿñ
ƒÿñ‚6        “ôoÖ1ÿÿÒ0þÿÏ0þÿÏ0þÿÌ0þÿÈ0þÿÆ0þÿÆ0þÿÂ0ýÿÂ0ýÿ—!ô¦                                        ®ØXÂ!Õ]                                        é ¨‰ë ¬ÿì§ÿíŸÿî›ÿï–ÿïÿïÿðŒÿð‰ÿñ†ÿïI        œøƒÙ1ÿÿÖ1ÿÿÒ0þÿÒ0þÿÏ0þÿÌ0þÿÊ0ýÿÆ0þÿÆ0þÿÂ0ýÿ¼+ûö}õ                                                                                ð
ˆé!¬ðë ¬ÿì§ÿì£ÿíŸÿî˜ÿð“ÿð“ÿïÿðŒÿð‰ÿï[        ¡ ÷–Ù1ÿÿÙ1ÿÿÖ1ÿÿÔ0þÿÒ0þÿÏ0þÿÌ0þÿÊ0ýÿÊ0ýÿÆ0þÿÆ0þÿ¡ ÷–                                                                                è!«zê"²ÿê!¯ÿëªÿì£ÿíŸÿî›ÿî˜ÿï–ÿð“ÿïÿðŒÿñ
ƒl        ¬"ö®Ù1ÿÿÙ1ÿÿÙ1ÿÿÖ1ÿÿÖ1ÿÿÒ0þÿÏ0þÿÌ0þÿÌ0þÿÊ0ýÿÈ0þÿ¼+ûöŽ÷                                                                        Ì Ïæ%µäé#¶ÿê!¯ÿë ¬ÿì§ÿíŸÿíŸÿî›ÿî˜ÿï–ÿð“ÿïÿð†|        ¢#úºÙ1ÿÿÙ1ÿÿÙ1ÿÿÙ1ÿÿÖ1ÿÿÔ0þÿÒ0þÿÏ0þÿÏ0þÿÌ0þÿÊ0ýÿÊ0ýÿ³!ï€                                                                        å ºmè%ºÿé#¶ÿê"²ÿë ¬ÿì§ÿì£ÿíŸÿíŸÿî›ÿî˜ÿï–ÿð“ÿð‰™        ¨#óËÙ1ÿÿÙ1ÿÿÙ1ÿÿÙ1ÿÿÙ1ÿÿÖ1ÿÿÖ1ÿÿÒ0þÿÒ0þÿÏ0þÿÏ0þÿÌ0þÿ¼*öí                                                                        å&¼Öç'½ÿè%ºÿê"²ÿê!¯ÿëªÿì§ÿì£ÿíŸÿíŸÿî›ÿî˜ÿï–ÿð‰™        Ž÷œøƒ¼+ûöÙ1ÿÿÙ1ÿÿÙ1ÿÿÙ1ÿÿÙ1ÿÿÖ1ÿÿÒ0þÿÒ0þÿÏ0þÿÐ0üþÐ0üþ§ìe                                                                å$²Oæ'Áþç'½ÿè%ºÿé#¶ÿê"²ÿë ¬ÿëªÿì§ÿì£ÿì£ÿíŸÿîÙñ
ƒlð
ˆ                    “ôo³)ûëÙ1ÿÿÙ1ÿÿÙ1ÿÿÙ1ÿÿÖ1ÿÿÔ0þÿÓ0üÿÒ0þÿÐ0üþ¾(ôã                                                                å&¼Öå)Æÿæ'Áþç'½ÿé#¶ÿê"²ÿê!¯ÿë ¬ÿëªÿì§ÿîÙí–aÌ Ï                                    ’ øSµ&üÝÖ1ÿÿÙ1ÿÿÙ1ÿÿ×1þÿÔ0þÿÔ0þÿÓ0üÿÐ0üþ®ØX                                                        ìª:ä+Ìÿå)Æÿæ'Áþç'½ÿè%ºÿé#¶ÿê"²ÿê!¯ÿë¨ÆïI                                                        ”ù=³%ùÐÔ0þÿÙ1ÿÿ×1þÿ×1þÿÔ0þÿÓ0üÿ»'ìâ                                                        æ'ÁÅä+Ìÿå)Æÿå)Æÿæ'Áþè%ºÿé#¶ÿë¨Æìª:                                                                        ™ð)µ$öÀÓ0üÿ×1þÿ×1þÿ×1þÿÓ0üÿ³Ì:                                                ã"µ-ã,Òÿä+Ìÿä+Ìÿå)Æÿæ'Áþê®´íŽ&                                                                                        žÕ¬"ö®Ð0üþ×1þÿÖ1ûÿÆ&ìÊ                                                å'Ä¹ã,Òÿã,Òÿå)Èõç ¸ é•                                                                                                        Ž÷³!ï€Î+õóÖ1ûÿÀ!Õ*                                        í$¿â-Öûã+Îðç ¸ ×¸                                                                                                                        Ì ÏÆ#â}Ê$ßx                                        å ºmå ºmÌ Ï                                                                ÿÿþÿÿ  ÿÿøÿÿ  ÿÿðÿÿ  ÿÿÀÿÿ  ÿÿ  ÿÿ  ÿü  ?ÿ  ÿø  ÿ  ÿð  ÿ  ÿø  ÿ  ÿø  ÿ  ÿü  ?ÿ  ÿü  ?ÿ  ÿþ  ÿ  ÿÿÿÿÿÿ  ÿÿÿÿÿÿ  ïÿÿÿÿ÷  ïÿÿÿÿ÷  çÿÿÿÿç  çÿÿÿÿç  ãÿÿÿÿç  ÃÿÀÿÇ  ÁÿÀÿÇ  Áÿàÿ‡  Àÿàÿƒ  Àÿðÿ  Àÿðÿ  Àðþ  Àøþ  À?øü  À?ü?ü  Àü?ø  Àü?ø  Àþð  Àþð  Àÿÿà  €ÿÿà  €ÿÿà  €ÿÿÀ  €ÿÿÀ  €ÿÿ€  Àÿÿ€  ø ÿÿ   þ ÿÿ   ÿ€þÿ  ÿàþÿ  ÿø?üÿ  ÿþ?üÿ  ÿÿÿÿÿÿ  (       @                                                                                     Jò'I	õµI	õµJò'                                                                                                        \ðDòˆF	ôøH
öÿL	õÿQóûUò™vó                                                                                        l ÿF
îjF	ðêDóÿH
öÿL	õÿOôÿSóÿXðÿbëîk	åw€ë                                                                            RîKPîÖLñÿLñÿL	õÿH
öÿL	õÿOôÿSóÿ]îÿféÿpãÿzÞÙ‡	ÕU                                                                    Xì¢YîÿUñÿUñÿP
õÿP
õÿP
õÿOôÿSóÿXðÿbëÿkæÿtáÿ~ÛÿŽ
ÒÿœÈ˜                                                                \í^ñÿ^ñÿZõÿZõÿV÷ÿV÷ÿVóÿVòÿ]îÿféÿpãÿyÞÿ†	×ÿ”Íÿ¢Âq                                                                vófòöaôÿaôÿ]÷ÿ]÷ÿZõÿ]óÿ]óÿbëÿkæÿtáÿ~ÛÿŽ
Òÿ›ÈöÍ¡                                                                    hó¬gõÿgõÿcøÿcøÿbôÿbôÿf
ïÿlêÿq
åÿyÞÿ†	×ÿ”Íÿ¢
Å                                                                        gñ%h÷ch÷ch÷ceô`g÷^kñ[kñ[xíY~
ßW~
ßW	ÒU™ÉU±Ä                                            Œñ                                                                                                        Í¡            \ðñŸ                                                                                                        ï‚~ìŠ        yò(“%ôö\ð                                                                                                ìŠï
€êõ
z        zïAœ(õÿ€ï                                                                                                î‚lñ	}ÿóv)        xíY£*÷ÿ™%öí€ë                        l ÿøª„ó®†ï­ŽéªŽéª™â§ ×¦©Ðžã «                        ã «ï€Þðzÿòr:        †ñp¨+øÿ£*÷ÿƒõ}                            † ÷¾‘ ñÿ‘ ñÿ•ëÿšçÿ¡ßÿª×ÿ±Í´                            ñyZñ	}ÿñwÿõsI        ‰ò†¯,ùÿ¨+øÿ™%öíÝ Á                        Š úT—"ñÿ—"ñÿŸ!êÿ¡åÿ©Ýÿ±Õÿ½ ÍG                            ð
ƒÑðzÿótÿñyZ        ó›³.úÿ¯,ùÿ«-ùÿˆ"øi                            ›$õáŸ#îÿŸ!êÿ¨!äÿ°"Üÿ·!ÔàÝ Á                        ë‹Kñ
ƒÿñ	}ÿótÿõ lj        • õ®·.ûÿ³.úÿ¯,ùÿ›$õá                            —î‰¥$îÿ©$ëü¯$âÿ·%ßÿÀ#Ó€                            ëŽÄð‡ÿñ	}ÿótÿõ mz        ›#ö¿¼/ýÿ¸/üÿ·.ûÿ³.úÿŠ úT                        vó©$ëü¯%èÿ·%ßÿ»%Ùù±Ä                        ê›;ï‘ÿð‡ÿñ	}ÿótÿôqŒ        ¡%ùÌÂ0ýÿ¿/ýÿ¸/üÿ¸/üÿž)úÕ                            ©!é·´(çÿ·%ßÿ½%Ô³                            í™·ï‘ÿðŒÿñ
ƒÿózÿôtž        ª&øÝÇ0ýÿÂ0ýÿ¿/ýÿ¼/ýÿ¸/üÿ— ù>                        ª#äBº*æÿÀ)àÿ½ ÍG                        í¡*ëœýï”ÿðŒÿñ
ƒÿñ	}ÿòz°        ±'úéË0þÿÇ0ýÿÂ0ýÿ¿/ýÿ¿/ýÿ¡%ùÌ                            »(âÞÁ(ÛÞ                            ì¡«íŸÿï–ÿðŒÿð‡ÿñ
ƒÿóÁ        º*ûóÐ/þÿË0þÿÈ1þÿÅ0þÿÂ0ýÿ¼/ýÿ ô+                        »$ÝÀ#Ó€                        æªë ¨ùíŸÿî™ÿï‘ÿðŒÿð‡ÿð
ƒÑ        Ä-ýûÔ1ÿÿÐ/þÿÎ1þÿË0þÿÇ0ýÿÂ0ýÿ¤%ø¼                        ÝÅ¤ê                        ê ªŸë«ÿí¢ÿîšÿï”ÿï‘ÿðŒÿð‡â        Ð/þÿÙ1ÿÿÔ1ÿÿÒ1þÿÎ1þÿË0þÿÇ0ýÿÄ-ýûŒñ                                                á¶è#³õê!¯ÿì§ÿíŸÿî™ÿï”ÿï‘ÿðï        Ö1ÿÿÙ1ÿÿÙ1ÿÿÖ1ÿÿÒ1þÿÎ1þÿË0þÿÊ0üÿ­$õ«                                                è#±“è#·ÿê!¯ÿì§ÿí¢ÿíŸÿîšÿï–ÿï‘ÿÝ Á¤êÃ+ûùÙ1ÿÿÙ1ÿÿÙ1ÿÿ×0ÿÿÒ1þÿÐ/þÿÎ1þÿÃ+ûù¤ê                                        ÝÅæ&½ñè%ºÿé"´ÿë«ÿì§ÿí¢ÿíŸÿîšÿï”ÝìŠ        š"új¸(ýèÙ1ÿÿÙ1ÿÿ×0ÿÿÔ1ÿÿÒ1þÿÎ1þÿ²!ð™                                        è$¶…å)Åÿç'¾ÿè#·ÿê!¯ÿë«ÿì§ÿížÕì—`Ý Á                    › ùYµ%üÝ×0ÿÿ×0ÿÿÔ1ÿÿÔ0ýÿÉ-øöl ÿ                                Ý Áå(Åêå)Åÿç'¾ÿè%ºÿé"´ÿë¨ÌîžL                                        — ù>·%øÐÔ0ýÿÔ1ÿÿÔ0ýÿ¾#è…                                å#»tä,Ïýå*Éÿæ&Âüë °Âê›;                                                         ô+¼%ò¿Ò/ûýÎ+óñ                                ã)Ìää,Ïýè!¹¹í¡*                                                                        ÂàÈ$é°Ê'ßH                        â%¹>ä$À¢á¶                                        ÿþÿÿøÿÿðÿÿÀÿÿ  ÿÿ€ÿÿ€ÿÿ€ÿÿÿÿÿÿÿÿÿßÿÿÿßÿÿûÏÿÿûÏðóÏðó‡øã‡øãƒøÃƒü?Áü?þ€þ€ÿ€þ€þ€?ü€?üàøøøþøÿñÿÿï÷ÿ(                                                            @ì(FõµNô¶Wó)                                        Q
äJ
íŽIóøJ
öÿQóÿ_ìûsã™†Î                                [ïÊXñÿTõÿRôÿVòÿiçÿ|Ýÿ˜ÊÂ                                còydõÿa÷ÿ_ôÿeîÿrâÿ‰
Ôÿ¥Ài                    €+ÿ        ]èg÷>f÷<gò9vè8‚Ý5–
Í3¿ ¿        ÿ €    wîyï|                                                î…\ÿ €€ò(“$ôé  ÿ          ÿyõg…ëfåb Ô^ÿ ÿ        ÿ ÿï
~Òõ pƒóB¨,÷ÿ„õj            Œ ôÎ– íÿ âÿ°ÐÅ            î…Kñyÿó p)ˆñ^³.ùÿ›'øá            ñj£$ìÿ¯"ßÿ¿#Í`            î‰Åòwÿö k9ôw¼/ûÿ¶/üÿˆ#ùX        f ÿ­&éíº%ÜïÛ$¶        ëš?ïŒÿó	{ÿñtK• öÇ0ýÿ¿0ýÿ¤(ûØ            ³#â À$Õ            êž¾î‘ÿó
‚ÿòz`  ù§Ð0þÿÈ1þÿÂ0ýÿ—÷B        º"Ý%Ä'Ë'        ç"¨5ê¦þî–ÿðŒÿò
u¥"øºÙ1ÿÿÒ0þÿË1þÿ¯&øÎ                        é#±·ê «ÿížÿï–ÿîŠ‰–÷a½)þðÙ1ÿÿÓ1þÿÍ0üþª ê0                ë!·'è(¾ûê"±ÿì§ÿîšÒðD        ŸüZº%ùÝÕ0ýÿÀ&ïÃ                å%¿²æ(Äþé®ÏïœP                        °îJÅ&íËÉ(×        á-´å$Áºê«=                þ  ø  ð  ø  ÿÿ  ÿÿ  ¿ý  ¼=  žy  žy  q  ñ  à  ‡á  ãÇ  ûß  
""";
        }

        private static string GetLogoSvgData()
        {
            return $$"""
<svg width="85" height="63" viewBox="0 0 85 63" fill="none" xmlns="http://www.w3.org/2000/svg">
<path fill-rule="evenodd" clip-rule="evenodd" d="M27.017 30.3135C27.0057 30.5602 27 30.8085 27 31.0581C27 39.9267 34.1894 47.1161 43.0581 47.1161C51.9267 47.1161 59.1161 39.9267 59.1161 31.0581C59.1161 30.8026 59.1102 30.5485 59.0984 30.2959C60.699 30.0511 62.2954 29.7696 63.8864 29.4515L64.0532 29.4181C64.0949 29.9593 64.1161 30.5062 64.1161 31.0581C64.1161 42.6881 54.6881 52.1161 43.0581 52.1161C31.428 52.1161 22 42.6881 22 31.0581C22 30.514 22.0206 29.9747 22.0612 29.441L22.1136 29.4515C23.7428 29.7773 25.3777 30.0646 27.017 30.3135ZM52.4613 18.0397C49.8183 16.1273 46.5698 15 43.0581 15C39.54 15 36.2862 16.1313 33.6406 18.05C31.4938 17.834 29.3526 17.5435 27.221 17.1786C31.0806 12.7781 36.7449 10 43.0581 10C49.3629 10 55.0207 12.7708 58.8799 17.1612C56.7487 17.5285 54.6078 17.8214 52.4613 18.0397ZM68.9854 28.4316C69.0719 29.2954 69.1161 30.1716 69.1161 31.0581C69.1161 45.4495 57.4495 57.1161 43.0581 57.1161C28.6666 57.1161 17 45.4495 17 31.0581C17 30.1793 17.0435 29.3108 17.1284 28.4544L12.2051 27.4697C12.0696 28.6471 12 29.8444 12 31.0581C12 48.211 25.9052 62.1161 43.0581 62.1161C60.211 62.1161 74.1161 48.211 74.1161 31.0581C74.1161 29.8366 74.0456 28.6317 73.9085 27.447L68.9854 28.4316ZM69.6705 15.0372L64.3929 16.0927C59.6785 9.38418 51.8803 5 43.0581 5C34.2269 5 26.4218 9.39306 21.7089 16.1131L16.4331 15.0579C21.867 6.03506 31.7578 0 43.0581 0C54.3497 0 64.234 6.02581 69.6705 15.0372Z" fill="black"/>
<mask id="path-2-inside-1" fill="white">
<path d="M42.5 28.9252C16.5458 30.2312 0 14 0 14C0 14 26 22.9738 42.5 22.9738C59 22.9738 85 14 85 14C85 14 68.4542 27.6193 42.5 28.9252Z"/>
</mask>
<path d="M0 14L5.87269 -3.01504L-12.6052 26.8495L0 14ZM42.5 28.9252L41.5954 10.948L42.5 28.9252ZM85 14L96.4394 27.8975L79.1273 -3.01504L85 14ZM0 14C-12.6052 26.8495 -12.5999 26.8546 -12.5946 26.8598C-12.5928 26.8617 -12.5874 26.8669 -12.5837 26.8706C-12.5762 26.8779 -12.5685 26.8854 -12.5605 26.8932C-12.5445 26.9088 -12.5274 26.9254 -12.5092 26.943C-12.4729 26.9782 -12.4321 27.0174 -12.387 27.0605C-12.2969 27.1467 -12.1892 27.2484 -12.0642 27.3646C-11.8144 27.5968 -11.4949 27.8874 -11.1073 28.2273C-10.3332 28.9063 -9.28165 29.7873 -7.96614 30.7967C-5.34553 32.8073 -1.61454 35.3754 3.11693 37.872C12.5592 42.8544 26.4009 47.7581 43.4046 46.9025L41.5954 10.948C32.6449 11.3983 25.2366 8.83942 19.9174 6.03267C17.2682 4.63475 15.2406 3.22667 13.9478 2.23478C13.3066 1.74283 12.8627 1.366 12.6306 1.16243C12.5151 1.06107 12.4538 1.00422 12.4485 0.999363C12.446 0.996981 12.4576 1.00773 12.4836 1.03256C12.4966 1.04498 12.5132 1.06094 12.5334 1.08055C12.5436 1.09035 12.5546 1.10108 12.5665 1.11273C12.5725 1.11855 12.5787 1.12461 12.5852 1.13091C12.5884 1.13405 12.5934 1.13895 12.595 1.14052C12.6 1.14548 12.6052 1.15049 0 14ZM43.4046 46.9025C59.3275 46.1013 72.3155 41.5302 81.3171 37.1785C85.8337 34.9951 89.4176 32.8333 91.9552 31.151C93.2269 30.3079 94.2446 29.5794 94.9945 29.0205C95.3698 28.7409 95.6788 28.503 95.92 28.3138C96.0406 28.2192 96.1443 28.1366 96.2309 28.067C96.2742 28.0321 96.3133 28.0005 96.348 27.9723C96.3654 27.9581 96.3817 27.9448 96.3969 27.9323C96.4045 27.9261 96.4119 27.9201 96.419 27.9143C96.4225 27.9114 96.4276 27.9072 96.4294 27.9057C96.4344 27.9016 96.4394 27.8975 85 14C73.5606 0.102497 73.5655 0.0985097 73.5703 0.0945756C73.5718 0.0933319 73.5765 0.0894438 73.5795 0.0869551C73.5856 0.0819751 73.5914 0.077195 73.597 0.0726136C73.6082 0.0634509 73.6185 0.055082 73.6278 0.0474955C73.6465 0.0323231 73.6614 0.0202757 73.6726 0.0112606C73.695 -0.00676378 73.7026 -0.0126931 73.6957 -0.00726687C73.6818 0.00363418 73.6101 0.0596753 73.4822 0.154983C73.2258 0.346025 72.7482 0.691717 72.0631 1.14588C70.6873 2.05798 68.5127 3.38259 65.6485 4.7672C59.8887 7.55166 51.6267 10.4432 41.5954 10.948L43.4046 46.9025ZM85 14C79.1273 -3.01504 79.1288 -3.01557 79.1303 -3.01606C79.1306 -3.01618 79.1319 -3.01664 79.1326 -3.01688C79.134 -3.01736 79.135 -3.0177 79.1356 -3.01791C79.1369 -3.01834 79.1366 -3.01823 79.1347 -3.01759C79.131 -3.01633 79.1212 -3.01297 79.1055 -3.00758C79.0739 -2.99681 79.0185 -2.97794 78.9404 -2.95151C78.7839 -2.89864 78.5366 -2.81564 78.207 -2.7068C77.5472 -2.48895 76.561 -2.16874 75.3165 -1.78027C72.8181 -1.00046 69.3266 0.039393 65.3753 1.07466C57.0052 3.26771 48.2826 4.97383 42.5 4.97383V40.9738C53.2174 40.9738 65.7448 38.193 74.4997 35.8992C79.1109 34.691 83.1506 33.4874 86.0429 32.5846C87.4937 32.1318 88.6676 31.7509 89.4942 31.478C89.9077 31.3414 90.2351 31.2317 90.4676 31.1531C90.5839 31.1138 90.6765 31.0823 90.7443 31.0591C90.7783 31.0475 90.806 31.038 90.8275 31.0306C90.8382 31.0269 90.8473 31.0238 90.8549 31.0212C90.8586 31.0199 90.862 31.0187 90.865 31.0177C90.8665 31.0172 90.8684 31.0165 90.8691 31.0163C90.871 31.0156 90.8727 31.015 85 14ZM42.5 4.97383C36.7174 4.97383 27.9948 3.26771 19.6247 1.07466C15.6734 0.039393 12.1819 -1.00046 9.68352 -1.78027C8.43897 -2.16874 7.4528 -2.48895 6.79299 -2.7068C6.46337 -2.81564 6.21607 -2.89864 6.05965 -2.95151C5.98146 -2.97794 5.92606 -2.99681 5.89453 -3.00758C5.87876 -3.01297 5.86897 -3.01633 5.86528 -3.01759C5.86344 -3.01823 5.86312 -3.01834 5.86435 -3.01791C5.86497 -3.0177 5.86597 -3.01736 5.86736 -3.01688C5.86805 -3.01664 5.86939 -3.01618 5.86973 -3.01606C5.87116 -3.01557 5.87269 -3.01504 0 14C-5.87269 31.015 -5.87096 31.0156 -5.86914 31.0163C-5.8684 31.0165 -5.86647 31.0172 -5.86498 31.0177C-5.86201 31.0187 -5.85864 31.0199 -5.85486 31.0212C-5.84732 31.0238 -5.83818 31.0269 -5.82747 31.0306C-5.80603 31.038 -5.77828 31.0475 -5.74435 31.0591C-5.67649 31.0823 -5.58388 31.1138 -5.46761 31.1531C-5.23512 31.2317 -4.9077 31.3414 -4.49416 31.478C-3.66764 31.7509 -2.49366 32.1318 -1.04289 32.5846C1.84938 33.4874 5.88908 34.691 10.5003 35.8992C19.2552 38.193 31.7826 40.9738 42.5 40.9738V4.97383Z" fill="black" mask="url(#path-2-inside-1)"/>
</svg>
""";
        }

        #endregion

    }
}
