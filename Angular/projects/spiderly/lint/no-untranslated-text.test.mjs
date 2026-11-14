// The template rule of eslint.config.js, through ESLint's RuleTester on the angular-eslint template parser.
//
//   cd Angular && npm run test:lint-rules

import { describe, it } from 'node:test';
import { RuleTester } from 'eslint';
import angular from 'angular-eslint';
import rule from './no-untranslated-text.js';

RuleTester.describe = describe;
RuleTester.it = it;

const ruleTester = new RuleTester({ languageOptions: { parser: angular.templateParser } });

// The refusal names the text and the fix.
const refused = (text) => ({
  message: new RegExp(
    `^Static text "${text}" in a library template\\. Translate it with Transloco, .*TranslocoDirective.*provideSpiderlyScope\\(\\).*\\*transloco="let t".*\\{\\{ t\\('spiderly\\.Key'\\) \\}\\}.*i18n/en\\.json`,
  ),
});

ruleTester.run('no-untranslated-text', rule, {
  valid: [
    { code: `<ng-container *transloco="let t"><button>{{ t('Save') }}</button></ng-container>` },
    // Symbols, digits and punctuation hold no letter.
    { code: `<h1>404</h1><span>× → ( {{ count }} ) · —</span>` },
    // An attribute no person reads.
    { code: `<p-button class="w-full {{ size }}" icon="pi pi-plus" severity="secondary" optionLabel="displayName"></p-button>` },
    // A literal the binding does not show: a translation key, a pipe's input, a compared value.
    { code: `<p-button [label]="t('DeleteSelected') + ' (' + count + ')'" [ngClass]="'wide'"></p-button>` },
    { code: `<span>{{ 'Save' | transloco }} {{ status === 'active' ? t('Active') : t('Inactive') }}</span>` },
    // A symbol written with letters, on the allow list.
    { code: `<span>{{ width }} px</span>`, options: [{ allow: ['px'] }] },
  ],
  invalid: [
    { code: `<button>Save</button>`, errors: [refused('Save')] },
    { code: `<span>Total: {{ count }} items</span>`, errors: [refused('Total:'), refused('items')] },
    { code: `<p-button label="Go to Homepage"></p-button>`, errors: [refused('Go to Homepage')] },
    { code: `<input placeholder="Search" />`, errors: [refused('Search')] },
    // Reported once, though `*ngIf` copies the element's attributes onto its template.
    { code: `<img *ngIf="logo" [src]="logo" alt="{{ companyName }} Logo" />`, errors: [refused('Logo')] },
    // A literal the binding shows is static text in quotes.
    { code: `<span>{{ 'Save' }}</span>`, errors: [refused('Save')] },
    { code: `<p-button [label]="'Save'"></p-button>`, errors: [refused('Save')] },
    { code: `<span>{{ done ? 'Yes' : t('No') }} {{ name ?? 'Unknown' }}</span>`, errors: [refused('Yes'), refused('Unknown')] },
    { code: `<span>{{ width }} px wide</span>`, options: [{ allow: ['px'] }], errors: [refused('px wide')] },
  ],
});
