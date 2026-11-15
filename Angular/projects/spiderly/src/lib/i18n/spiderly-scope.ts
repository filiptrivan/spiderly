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
  provideTransloco,
  Translation,
  TRANSLOCO_CONFIG,
  TRANSLOCO_LOADER,
  TRANSLOCO_SCOPE,
  TranslocoOptions,
  TranslocoService,
} from '@jsverse/transloco';
import { firstValueFrom, from, switchMap } from 'rxjs';

import en from './en.json';

/** The Transloco scope that holds the library's own strings: a template reads `t('spiderly.Save')`. */
export const SPIDERLY_SCOPE = 'spiderly';

/**
 * Provides Transloco with `options`, as `provideTransloco` does, and loads the library's strings.
 * They load before the app's first page, and again on each language switch. So a class that
 * translates a `spiderly.` key as it is built (a form's toast text) reads the active language.
 */
export function provideSpiderlyTransloco(options: TranslocoOptions): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideTransloco(options),
    provideAppInitializer(() => {
      const transloco = inject(TranslocoService);
      const loader = spiderlyLoader();
      // `load` takes a full path, and its inline loader is keyed by that path.
      const load = (lang: string) => {
        const path = `${SPIDERLY_SCOPE}/${lang}`;
        return transloco.load(path, { inlineLoader: { [path]: loader[lang] } });
      };

      transloco.langChanges$
        .pipe(switchMap(load), takeUntilDestroyed(inject(DestroyRef)))
        .subscribe();
      return firstValueFrom(load(transloco.getActiveLang()));
    }),
  ]);
}

/**
 * Makes a library component's `*transloco` wait for the library's strings before it renders. It
 * waits on the first render and on each language switch. Each library component whose template
 * reads a `spiderly.` key provides it.
 *
 * Each component provides the scope, never the app's environment. Transloco gives an environment
 * scope's loader to every inline `scope:` in the app. And an app component that provides its own
 * scope hides an environment scope from all the components below it.
 */
export function provideSpiderlyScope(): Provider {
  return {
    provide: TRANSLOCO_SCOPE,
    useFactory: () => ({ scope: SPIDERLY_SCOPE, loader: spiderlyLoader() }),
    multi: true,
  };
}

// English ships inline. Any other language loads through the app's own loader as `spiderly/<lang>`.
// Transloco gives that path to each scope of the app's own. So the app adds its language for the
// library's strings with one more file, `i18n/spiderly/<lang>.json`. The file lies over the
// English: a key it lacks, such as one a new release adds, shows in English, never as a raw key.
// Without the file, all the library's strings show in English.
function spiderlyLoader(): InlineLoader {
  const appLoader = inject(TRANSLOCO_LOADER);
  const loader: InlineLoader = {};

  for (const lang of inject(TRANSLOCO_CONFIG).availableLangs) {
    const id = typeof lang === 'string' ? lang : lang.id;
    const path = `${SPIDERLY_SCOPE}/${id}`;
    loader[id] =
      id === 'en'
        ? () => Promise.resolve(en)
        : () =>
            firstValueFrom(from(appLoader.getTranslation(path, { scope: SPIDERLY_SCOPE }))).then(
              (translation): Translation => ({ ...en, ...translation }),
              (): Translation => {
                console.warn(`The app's loader has no ${path}, so the library's strings show in English.`);
                return en;
              },
            );
  }

  return loader;
}
