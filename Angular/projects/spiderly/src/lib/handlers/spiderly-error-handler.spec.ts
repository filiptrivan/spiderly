import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoService } from '@jsverse/transloco';

import { SpiderlyMessageService } from '../services/spiderly-message.service';
import { SpiderlyErrorHandler } from './spiderly-error-handler';

// Constructed directly: both of the handler's collaborators are injected, so a TestBed
// would only add ceremony around the decisions under test.
function createHandler() {
  const messageService = {
    errorMessage: jasmine.createSpy('errorMessage'),
  } as unknown as SpiderlyMessageService;

  const translocoService = {
    translate: (key: string) => key,
  } as unknown as TranslocoService;

  return {
    handler: new SpiderlyErrorHandler(messageService, translocoService),
    messageService,
  };
}

describe('SpiderlyErrorHandler', () => {
  beforeEach(() => {
    spyOn(console, 'error');
  });

  // Logging used to be gated on `config.production == false`. The toast says the team was
  // notified, and unless the app wires an error tracker this line is the ONLY place the error
  // exists — so a deployed app gave a red toast and nothing to open: not the console, not a
  // log, not a tracker. The console is a developer surface (a user never opens it), so silence
  // there protected nothing. There is no production flag on this class any more, by design.
  it('logs every error, with no environment condition', () => {
    const { handler } = createHandler();
    const error = new TypeError('Cannot read properties of undefined');

    handler.handleError(error);

    expect(console.error).toHaveBeenCalledWith(error);
  });

  it('shows the generic toast for a non-HTTP error', () => {
    const { handler, messageService } = createHandler();

    handler.handleError(new TypeError('boom'));

    expect(messageService.errorMessage).toHaveBeenCalledWith(
      'UnexpectedErrorDetails',
      'UnexpectedErrorTitle',
    );
  });

  // HTTP-error UX belongs to unauthorizedInterceptor, which has already shown the message
  // matched to the status by the time an unhandled HttpErrorResponse reaches here. Toasting
  // again would double up, and with the wrong (generic) copy — but it still gets logged.
  it('leaves HTTP errors to the interceptor, but still logs them', () => {
    const { handler, messageService } = createHandler();
    const error = new HttpErrorResponse({ status: 500 });

    handler.handleError(error);

    expect(messageService.errorMessage).not.toHaveBeenCalled();
    expect(console.error).toHaveBeenCalledWith(error);
  });
});
