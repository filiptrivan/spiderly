import {
  DestroyRef,
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
  Provider,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  InlineLoader,
  Translation,
  TRANSLOCO_CONFIG,
  TRANSLOCO_LOADER,
  TRANSLOCO_SCOPE,
  TranslocoService,
} from '@jsverse/transloco';
import { firstValueFrom, from, switchMap } from 'rxjs';

import en from './en.json';

/** The Transloco scope that holds the library's own strings: a template reads `t('spiderly.Save')`. */
export const SPIDERLY_SCOPE = 'spiderly';

/**
 * Loads the library's strings before the app's first page, and again on each language switch, so a
 * class that translates a `spiderly.` key as it is built (a form's toast text, a menu's labels) reads
 * the active language. The app provides it once, beside its `provideTransloco`.
 */
export function provideSpiderlyTranslations(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() => {
      const transloco = inject(TranslocoService);
      const inlineLoader = spiderlyLoader(`${SPIDERLY_SCOPE}/`);
      const load = (lang: string) =>
        transloco.load(`${SPIDERLY_SCOPE}/${lang}`, { inlineLoader });

      transloco.langChanges$
        .pipe(switchMap(load), takeUntilDestroyed(inject(DestroyRef)))
        .subscribe();
      return firstValueFrom(load(transloco.getActiveLang()));
    }),
  ]);
}

/**
 * Makes a library component's `*transloco` wait for the library's strings before it renders, on the
 * first render and on each language switch. Every library component whose template or class reads a
 * `spiderly.` key provides it.
 */
export function provideSpiderlyScope(): Provider {
  return {
    provide: TRANSLOCO_SCOPE,
    useFactory: () => ({ scope: SPIDERLY_SCOPE, loader: spiderlyLoader('') }),
    multi: true,
  };
}

// English ships inline. Any other language loads through the app's own loader as `spiderly/<lang>`,
// the path Transloco gives each scope of the app's own, so an app adds its language for the
// library's strings with one more file beside its own (`i18n/spiderly/<lang>.json`). Without that
// file the library's strings show in English, never as a blank component.
function spiderlyLoader(keyPrefix: string): InlineLoader {
  const appLoader = inject(TRANSLOCO_LOADER);
  const loader: InlineLoader = {};

  for (const lang of inject(TRANSLOCO_CONFIG).availableLangs) {
    const id = typeof lang === 'string' ? lang : lang.id;
    const path = `${SPIDERLY_SCOPE}/${id}`;
    loader[`${keyPrefix}${id}`] =
      id === 'en'
        ? () => Promise.resolve(en)
        : () =>
            firstValueFrom(from(appLoader.getTranslation(path, { scope: SPIDERLY_SCOPE }))).catch(
              (): Translation => {
                console.warn(`The app's loader has no ${path}, so the library's strings show in English.`);
                return en;
              },
            );
  }

  return loader;
}
