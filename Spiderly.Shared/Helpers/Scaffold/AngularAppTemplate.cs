using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>src/app/</c> folder: the root component, its providers and its routes, with the business folder
    /// and the pages they name.
    /// </summary>
    internal static class AngularAppTemplate
    {
        internal static SpiderlyFolder Folder(string appName) => new()
        {
            Name = "app",
            ChildFolders =
            {
                AngularBusinessTemplate.Folder(),
                PagesTemplate.Folder(appName),
            },
            Files =
            {
                new SpiderlyFile { Name = "app.routes.ts", Data = GetAppRoutesTsData() },
                new SpiderlyFile { Name = "app.component.html", Data = GetAppComponentHtmlData() },
                new SpiderlyFile { Name = "app.component.ts", Data = GetAppComponentTsData() },
                new SpiderlyFile { Name = "app.config.ts", Data = GetAppConfigTsData() },
            }
        };

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
import { AuthServiceBase, authInitializer, ConfigServiceBase, httpLoadingInterceptor, jsonHttpInterceptor, jwtInterceptor, LayoutServiceBase, SpiderlyErrorHandler, unauthorizedInterceptor, ValidatorAbstractService } from 'spiderly';
import { environment } from 'src/environments/environment';
import { ValidatorService } from './business/services/validators/validators';
import { AuthService } from 'src/app/business/services/auth/auth.service';
import { ConfigService } from './business/services/config.service';
import { LayoutService } from './business/services/layout/layout.service';
import { provideTranslations } from './business/services/translations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { provideMarkdown } from 'ngx-markdown';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideAnimationsAsync(),
    provideHttpClient(withFetch()),
    provideMarkdown(),
    provideTranslations(),
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
    }
}
