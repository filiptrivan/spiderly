import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Observable, from, of, throwError } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
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

  const reactToError = (err: HttpErrorResponse, request: HttpRequest<any>, errorResponse: any): void => {
    // Unconditional, production included, and the only log for a failed request (the global
    // ErrorHandler skips HTTP errors): the toast the user gets is deliberately vague, so this is
    // where a developer reads which request failed and how. See SpiderlyErrorHandler for why
    // silence in production cost more than the console noise saves. The summary leads because an
    // HttpErrorResponse on its own stringifies to "[object HttpErrorResponse]" in an error
    // tracker's console breadcrumb; the object still follows, to expand in devtools.
    console.error(`HTTP ${err.status} ${request.method} ${request.url}`, err);

    // TODO: type errorResponse as an ApiError interface (TS mirror of ApiErrorDTO, next to
    // errors/api-error-codes.ts) so message/errorCode/traceId reads of the cross-language contract
    // are compile-checked, not conventional.

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
    catchError((err: HttpErrorResponse) =>
      readErrorBody(err, req).pipe(
        switchMap((errorResponse) => {
          reactToError(err, req, errorResponse);
          return throwError(() => err);
        }),
      ),
    ),
  );
};

/**
 * The server's ApiErrorDTO, whatever body type the request asked for. Angular hands the ERROR body
 * back in the requested `responseType` too: a `'text'` request gets the JSON as a string, and a
 * `'blob'` request (every PDF / Excel download) gets it as a Blob, whose text is only readable
 * asynchronously — which is why this is an Observable and the toast waits for it. Before the Blob
 * branch existed, a BusinessException on a download endpoint reached the operator as the generic
 * "bad request" toast: the sentence the server had spelled out was sitting unread in the Blob.
 * A body that is not JSON resolves to null, so the status-code fallbacks below still apply.
 */
const readErrorBody = (err: HttpErrorResponse, request: HttpRequest<any>): Observable<any> => {
  if (err.error instanceof Blob) {
    return from(err.error.text()).pipe(
      map(parseJsonOrNull),
      catchError(() => of(null)),
    );
  }
  if (request.responseType !== 'json' && typeof err.error === 'string') {
    return of(parseJsonOrNull(err.error));
  }
  return of(err.error);
};

const parseJsonOrNull = (text: string): any => {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
};
