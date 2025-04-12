import { provideTransloco, provideTranslocoLoader, TranslocoModule } from '@jsverse/transloco';
import { ModuleWithProviders, NgModule } from '@angular/core';

import { SpiderlyTranslocoLoader } from '../services/spiderly-transloco-loader';
import { provideTranslocoPreloadLangs } from '@jsverse/transloco-preload-langs';

@NgModule({
  imports: [TranslocoModule],
  exports: [TranslocoModule],
})
export class SpiderlyTranslocoModule {

  static forRoot(): ModuleWithProviders<SpiderlyTranslocoModule> {
    return {
      ngModule: SpiderlyTranslocoModule,
      providers: [
        provideTranslocoPreloadLangs(['sr-Latn-RS']),
        provideTransloco({
          config: {
            availableLangs: [
              'sr-Latn-RS', 'sr-Latn-RS.generated', 
              'en', 'en.generated',
            ],
            defaultLang: 'sr-Latn-RS',
            fallbackLang: [
              'sr-Latn-RS.generated',
            ],
            missingHandler: {
              useFallbackTranslation: true,
              logMissingKey: false,
            },
            reRenderOnLangChange: true,
          },
          loader: SpiderlyTranslocoLoader
        }),
      ],
    };
  }

}