import { provideTransloco, TranslocoModule } from '@jsverse/transloco';
import { EnvironmentProviders, importProvidersFrom, ModuleWithProviders, NgModule } from '@angular/core';

import { SpiderlyTranslocoLoader } from '../services/spiderly-transloco-loader';
import { provideTranslocoPreloadLangs } from '@jsverse/transloco-preload-langs';

export function provideSpiderlyTransloco(config?: SpiderlyTranslocoConfig): EnvironmentProviders {
  return importProvidersFrom(
    SpiderlyTranslocoModule.forRoot(config)
  );
}

@NgModule({
  imports: [TranslocoModule],
  exports: [TranslocoModule],
})
export class SpiderlyTranslocoModule {

  static forRoot(config?: SpiderlyTranslocoConfig): ModuleWithProviders<SpiderlyTranslocoModule> {
    return {
      ngModule: SpiderlyTranslocoModule,
      providers: [
        provideTranslocoPreloadLangs(config.preloadLangs ?? ['en']),
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
  preloadLangs: string[];
  defaultLang: string;
  fallbackLang: string;
}