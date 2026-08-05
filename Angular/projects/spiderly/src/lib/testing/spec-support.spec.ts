import { TranslocoTestingOptions } from '@jsverse/transloco';

// Shared spec bootstrap. Named *.spec.ts so tsconfig.lib's `**/*.spec.ts`
// exclude keeps it out of the published library build while tsconfig.spec
// picks it up; it intentionally contains no tests.

// Every control in this library routes static text through Transloco
// (Angular/CLAUDE.md), so component specs boot it from this one empty-map
// config instead of each carrying its own copy.
export function translocoTesting(): TranslocoTestingOptions {
  return {
    langs: { en: {} },
    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
    preloadLangs: true,
  };
}
