import { TranslocoService } from '@jsverse/transloco';
import { ErrorHandler, Injectable } from '@angular/core';
import { SpiderlyMessageService } from '../services/spiderly-message.service';
import { HttpErrorResponse } from '@angular/common/http';

/**
 * The app's global {@link ErrorHandler}: logs every error and shows the generic error toast for
 * the ones no other layer owns. HTTP errors are excluded on purpose — `unauthorizedInterceptor`
 * has already shown the message matched to the status by the time one reaches here.
 *
 * Logging is unconditional, production included. The toast tells the user the team was notified,
 * and unless the app wires an error tracker this line is the only place the error exists — so
 * staying quiet in production left a deployed app with a red toast and nothing to open. The
 * console is a developer surface; a user never sees it.
 *
 * An error tracker plugs in by providing an `ErrorHandler` that reports and then delegates here,
 * which keeps this class's toast copy true (see the PACMS admin's `AdminErrorHandler`).
 */
@Injectable({
  providedIn: 'root',
})
export class SpiderlyErrorHandler implements ErrorHandler {
  constructor(
    private messageService: SpiderlyMessageService,
    private translocoService: TranslocoService,
  ) {}

  handleError(error: any): void {
    console.error(error);

    if (error instanceof HttpErrorResponse == false) {
      this.messageService.errorMessage(
        this.translocoService.translate('UnexpectedErrorDetails'),
        this.translocoService.translate('UnexpectedErrorTitle'),
      );
    }
  }
}
