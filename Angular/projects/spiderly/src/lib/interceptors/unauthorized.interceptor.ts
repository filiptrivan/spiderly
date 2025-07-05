import { inject } from '@angular/core';
import { HttpRequest, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { SpiderlyMessageService } from '../services/spiderly-message.service';
import { TranslocoService } from '@jsverse/transloco';
import { ConfigBaseService } from '../services/config-base.service';

export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(SpiderlyMessageService);
  const translocoService = inject(TranslocoService);
  const config = inject(ConfigBaseService);

  const handleAuthError = (err: HttpErrorResponse, request: HttpRequest<any>): Observable<any> => {
    if (!config.production) {
      console.error(err);
    }
    
    let errorResponse = err.error;

    if (request.responseType != 'json')
      errorResponse = JSON.parse(err.error);

    if (err.status == 0) {
      setTimeout(() => { // Had problem when the server is shut down, and try to refresh token, warning message didn't appear
        messageService.warningMessage( 
          translocoService.translate('ServerLostConnectionDetails'),
          translocoService.translate('ServerLostConnectionTitle'),
        );
      }, 100);
      return of(err.message);
    } 
    else if (err.status == 400) {
      messageService.warningMessage(
        errorResponse.message ?? translocoService.translate('BadRequestDetails'),
        translocoService.translate('Warning'),
      );

      return of(err.message);
    } 
    else if (err.status == 401) {
      messageService.warningMessage(
        errorResponse.message ?? translocoService.translate('LoginRequired'),
        translocoService.translate('Warning'),
      );

      return of(err.message);
    } 
    else if (err.status == 403) {
      messageService.warningMessage(
        translocoService.translate('PermissionErrorDetails'),
        translocoService.translate('PermissionErrorTitle'),
      );
      return of(err.message);
    } 
    else if (err.status == 404) {
      messageService.warningMessage(
        translocoService.translate('NotFoundDetails'),
        translocoService.translate('NotFoundTitle'),
      );
      return of(err.message);
    } 
    else if (err.status == 419) { // Access token expired
      return of(err.message);
    } 
    else {
      messageService.errorMessage(
        errorResponse.message,
        translocoService.translate('UnexpectedErrorTitle'),
      );

      return of(err.message);
    }

    return throwError(err);
  }
  
  return next(req).pipe(
    catchError((err) => {
      return handleAuthError(err, req);
    })
  );
}
