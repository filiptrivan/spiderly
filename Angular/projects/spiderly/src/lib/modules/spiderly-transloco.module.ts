import { provideTransloco, provideTranslocoLoader, TranslocoModule } from '@jsverse/transloco';
import { ModuleWithProviders, NgModule } from '@angular/core';

import { SpiderlyTranslocoLoader } from '../services/spiderly-transloco-loader';
import { provideTranslocoPreloadLangs } from '@jsverse/transloco-preload-langs';

@NgModule({
  imports: [TranslocoModule],
  exports: [TranslocoModule],
})
export class SpiderlyTranslocoModule {

  static forRoot(config?: SpiderlyTranslocoConfig): ModuleWithProviders<SpiderlyTranslocoModule> {
    return {
      ngModule: SpiderlyTranslocoModule,
      providers: [
        provideTranslocoPreloadLangs(['sr-Latn-RS']),
        provideTransloco({
          config: {
            availableLangs: config?.availableLangs ?? [
              'en', 'en.generated',
              'sr-Latn-RS', 'sr-Latn-RS.generated', 
            ],
            defaultLang: config?.defaultLang ?? 'en',
            fallbackLang: config?.fallbackLang ?? 'en.generated',
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

export interface SpiderlyTranslocoConfig {
  availableLangs: string[];
  defaultLang: string;
  fallbackLang: string;
}