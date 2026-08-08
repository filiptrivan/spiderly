import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiErrorCodes } from '../errors/api-error-codes';
import { AuthServiceBase } from '../services/auth.service.base';
import { SpiderlyMessageService } from '../services/spiderly-message.service';

/**
 * Owns cross-cutting HTTP-error UX: shows the right message and, on an expired session, clears auth — then
 * RETHROWS. Errors stay errors: callers run only their success path, and an unhandled HttpErrorResponse that
 * reaches the global ErrorHandler is intentionally ignored there (HTTP-error UX lives here). This interceptor
 * must never convert an error into a value — doing so makes callers treat failures as data.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(SpiderlyMessageService);
  const translocoService = inject(TranslocoService);
  const authService = inject(AuthServiceBase);

  const reactToError = (err: HttpErrorResponse, request: HttpRequest<any>): void => {
    // Unconditional, production included: the toast the user gets is deliberately vague, so
    // this is where a developer reads which request failed and how. See SpiderlyErrorHandler
    // for why silence in production cost more than the console noise saves.
    console.error(err);

    // TODO: type this as an ApiError interface (TS mirror of ApiErrorDTO, next to errors/api-error-codes.ts)
    // so message/errorCode/traceId reads of the cross-language contract are compile-checked, not conventional.
    let errorResponse = err.error;
    if (request.responseType !== 'json' && typeof err.error === 'string') {
      try {
        errorResponse = JSON.parse(err.error);
      } catch {
        errorResponse = null;
      }
    }

    // ApiErrorDTO.traceId is present only on reportable errors, so this is a no-op everywhere else —
    // the server decides which responses carry a support reference, never this status-code chain.
    const withReference = (detail: string): string =>
      errorResponse?.traceId
        ? `${detail} ${translocoService.translate('ErrorReference', { traceId: errorResponse.traceId })}`
        : detail;

    if (err.status === 0) {
      // Server unreachable; defer so the message isn't lost during a shutdown/refresh race.
      setTimeout(() => {
        messageService.warningMessage(
          withReference(translocoService.translate('ServerLostConnectionDetails')),
          translocoService.translate('ServerLostConnectionTitle'),
        );
      }, 100);
    } else if (err.status === 400) {
      messageService.warningMessage(
        withReference(errorResponse?.message ?? translocoService.translate('BadRequestDetails')),
        translocoService.translate('Warning'),
      );
    } else if (err.status === 401) {
      if (errorResponse?.errorCode === ApiErrorCodes.InvalidToken) {
        authService.clearSession(); // expired/invalid session — drop it; guards send the user to login
      } else {
        messageService.warningMessage(
          withReference(errorResponse?.message ?? translocoService.translate('LoginRequired')),
          translocoService.translate('Warning'),
        );
      }
    } else if (err.status === 403) {
      messageService.warningMessage(
        withReference(translocoService.translate('PermissionErrorDetails')),
        translocoService.translate('PermissionErrorTitle'),
      );
    } else if (err.status === 404) {
      messageService.warningMessage(
        withReference(translocoService.translate('NotFoundDetails')),
        translocoService.translate('NotFoundTitle'),
      );
    } else {
      messageService.errorMessage(
        withReference(translocoService.translate('UnexpectedErrorDetails')),
        translocoService.translate('UnexpectedErrorTitle'),
      );
    }
  };

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      reactToError(err, req);
      return throwError(() => err);
    }),
  );
};
