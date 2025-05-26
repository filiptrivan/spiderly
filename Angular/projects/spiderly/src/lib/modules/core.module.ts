import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { NgModule, APP_INITIALIZER, Optional, SkipSelf, ErrorHandler } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInitializer } from '../services/app-initializer';
import { jwtInterceptor } from '../interceptors/jwt.interceptor';
import { AuthBaseService } from '../services/auth-base.service';
import { SpiderlyErrorHandler } from '../handlers/spiderly-error-handler';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CommonModule } from '@angular/common';
import { BrowserModule } from '@angular/platform-browser';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { httpLoadingInterceptor } from '../interceptors/http-loading.interceptor';
import { jsonHttpInterceptor } from '../interceptors/json-parser.interceptor';
import { unauthorizedInterceptor } from '../interceptors/unauthorized.interceptor';

@NgModule({ declarations: [],
    exports: [
        BrowserModule,
        CommonModule,
        BrowserAnimationsModule,
        ToastModule,
        ConfirmDialogModule,
    ], imports: [BrowserModule,
        CommonModule,
        BrowserAnimationsModule,
        ToastModule,
        ConfirmDialogModule], providers: [
        MessageService,
        ConfirmationService,
        {
            provide: APP_INITIALIZER,
            useFactory: authInitializer,
            multi: true,
            deps: [AuthBaseService],
        },
        {
            provide: ErrorHandler,
            useClass: SpiderlyErrorHandler,
        },
        provideHttpClient(withInterceptors([
            httpLoadingInterceptor,
            jsonHttpInterceptor,
            jwtInterceptor,
            unauthorizedInterceptor
        ])),
    ] })
export class CoreModule {
  constructor(@Optional() @SkipSelf() core: CoreModule) {
    if (core) {
      throw new Error('Core Module can only be imported to AppModule.');
    }
  }
}