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
  // exists — so a deployed app gave a red toast and nothing to open. There is no production flag
  // on this class any more, by design; see its docstring.
  it('logs and toasts a non-HTTP error', () => {
    const { handler, messageService } = createHandler();
    const error = new TypeError('Cannot read properties of undefined');

    handler.handleError(error);

    expect(console.error).toHaveBeenCalledWith(error);
    expect(messageService.errorMessage).toHaveBeenCalledWith(
      'UnexpectedErrorDetails',
      'UnexpectedErrorTitle',
    );
  });

  // unauthorizedInterceptor owns HTTP errors end to end — it has already logged the failed
  // request and toasted the message matched to its status.
  it('ignores HTTP errors entirely, logging included', () => {
    const { handler, messageService } = createHandler();

    handler.handleError(new HttpErrorResponse({ status: 500 }));

    expect(messageService.errorMessage).not.toHaveBeenCalled();
    expect(console.error).not.toHaveBeenCalled();
  });
});
