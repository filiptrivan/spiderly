import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoService } from '@jsverse/transloco';

import { ConfigServiceBase } from '../services/config.service.base';
import { SpiderlyMessageService } from '../services/spiderly-message.service';
import { SpiderlyErrorHandler } from './spiderly-error-handler';

// Constructed directly: the handler's three collaborators are all injected, so a TestBed
// would only add ceremony around the one decision under test.
function createHandler(production: boolean) {
  const messageService = {
    errorMessage: jasmine.createSpy('errorMessage'),
  } as unknown as SpiderlyMessageService;

  const translocoService = {
    translate: (key: string) => key,
  } as unknown as TranslocoService;

  const config = { production } as unknown as ConfigServiceBase;

  return {
    handler: new SpiderlyErrorHandler(messageService, translocoService, config),
    messageService,
  };
}

describe('SpiderlyErrorHandler', () => {
  beforeEach(() => {
    spyOn(console, 'error');
  });

  // The toast says the team was notified, and unless the consumer wired an error tracker
  // this console line is the ONLY place the error exists. Gating it on !production made a
  // deployed app's client errors unreachable: nothing in the console, nothing in a tracker,
  // nothing in a log — a red toast and no way to find out what it was. The console is a
  // developer surface (a user never opens it), so there is nothing to protect by staying
  // silent there.
  it('logs the error in production too', () => {
    const { handler } = createHandler(true);
    const error = new TypeError('Cannot read properties of undefined');

    handler.handleError(error);

    expect(console.error).toHaveBeenCalledWith(error);
  });

  it('logs the error in development', () => {
    const { handler } = createHandler(false);
    const error = new TypeError('boom');

    handler.handleError(error);

    expect(console.error).toHaveBeenCalledWith(error);
  });

  it('shows the generic toast for a non-HTTP error', () => {
    const { handler, messageService } = createHandler(true);

    handler.handleError(new TypeError('boom'));

    expect(messageService.errorMessage).toHaveBeenCalledWith(
      'UnexpectedErrorDetails',
      'UnexpectedErrorTitle',
    );
  });

  // HTTP-error UX belongs to unauthorizedInterceptor, which has already shown the message
  // matched to the status by the time an unhandled HttpErrorResponse reaches here. Toasting
  // again would double up, and with the wrong (generic) copy.
  it('leaves HTTP errors to the interceptor', () => {
    const { handler, messageService } = createHandler(true);

    handler.handleError(new HttpErrorResponse({ status: 500 }));

    expect(messageService.errorMessage).not.toHaveBeenCalled();
    expect(console.error).toHaveBeenCalled();
  });
});
