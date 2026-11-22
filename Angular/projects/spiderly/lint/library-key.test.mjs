// The library-key rule of eslint.config.js, through ESLint's RuleTester on the angular-eslint template
// parser and on the TypeScript parser.
//
//   cd Angular && npm run test:lint-rules

import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { RuleTester } from 'eslint';
import angular from 'angular-eslint';
import tseslint from 'typescript-eslint';
import libraryKey from './library-key.js';

RuleTester.describe = describe;
RuleTester.it = it;

const rule = libraryKey.libraryKeyRule({
  Apply: 'Apply',
  Equals: 'Equals',
  FilterChipEquals: 'is',
  Remove: 'Remove',
  ResultCount: '{{count}} results',
  Return: 'Return',
  Yes: 'Yes',
});

const unscoped = (key) => ({
  message: new RegExp(`^"${key}" is read from the app's translations\\. Write 't\\('spiderly\\.${key}'\\)'.*i18n/en\\.json`),
});
const unknown = (key) => ({
  message: new RegExp(`^"spiderly\\.${key}" has no English: add "${key}" to the library's i18n/en\\.json`),
});
const unprovided = { message: /^This component's template reads the library's strings\. Add provideSpiderlyScope\(\) to its providers/ };

new RuleTester({ languageOptions: { parser: angular.templateParser } }).run('library-key (templates)', rule, {
  valid: [
    { code: `<p-button [label]="t('spiderly.Return')"></p-button>` },
    // A key built at run time, and a literal that is not a key.
    { code: `<span [ngClass]="'wide'">{{ t('spiderly.ResultCount', { count: n }) }} {{ t(chip.operatorPhraseKey) }}</span>` },
  ],
  invalid: [
    { code: `<p-button [label]="t('Save')"></p-button>`, errors: [unscoped('Save')] },
    { code: `<span>{{ ok ? t('spiderly.Yes') : t('Nope') }}</span>`, errors: [unscoped('Nope')] },
    { code: `<span>{{ t('spiderly.NoSuchKey') }}</span>`, errors: [unknown('NoSuchKey')] },
  ],
});

// A templateUrl resolves from the linted file, so these cases sit beside a real library template.
const returnButton = fileURLToPath(
  new URL('../src/lib/components/spiderly-buttons/return-button/return-button.component.ts', import.meta.url),
);

new RuleTester({ languageOptions: { parser: tseslint.parser } }).run('library-key (classes)', rule, {
  valid: [
    { code: `this.translocoService.translate('spiderly.Remove');` },
    // A key the app owns, built at run time: a field's label.
    { code: `this.translocoService.translate(firstCharToUpper(field));` },
    { code: `const words = { pickerKey: 'spiderly.Equals', phraseKey: 'spiderly.FilterChipEquals' };` },
    {
      code: `@Component({ selector: 'x', templateUrl: './return-button.component.html', providers: [provideSpiderlyScope()] }) class X {}`,
      filename: returnButton,
    },
    { code: `@Component({ selector: 'x', template: '<span>{{ count }}</span>' }) class X {}` },
  ],
  invalid: [
    { code: `this.translocoService.translate('Remove');`, errors: [unscoped('Remove')] },
    { code: `transloco.translate(\n  'AddAbove',\n  {});`, errors: [unscoped('AddAbove')] },
    { code: `const key = ok ? 'spiderly.Yes' : 'spiderly.Maybe';`, errors: [unknown('Maybe')] },
    {
      code: `@Component({ selector: 'x', templateUrl: './return-button.component.html' }) class X {}`,
      filename: returnButton,
      errors: [unprovided],
    },
    {
      code: `@Component({ selector: 'x', template: \`<b>{{ t('spiderly.Apply') }}</b>\`, providers: [ConfirmationService] }) class X {}`,
      errors: [unprovided],
    },
  ],
});
