import { EnvironmentProviders, makeEnvironmentProviders, APP_INITIALIZER, ErrorHandler } from "@angular/core";
import { MessageService, ConfirmationService } from "primeng/api";
import { authInitializer } from "../services/app-initializer";
import { AuthBaseService } from "../services/auth-base.service";
import { SpiderlyErrorHandler } from "../handlers/spiderly-error-handler";

export function provideSpiderlyCore(): EnvironmentProviders {
  return makeEnvironmentProviders([
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
  ]);
}