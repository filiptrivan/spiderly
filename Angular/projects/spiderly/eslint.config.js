// @ts-check
// `ng lint` (angular.json names this file) and .githooks/pre-commit. It holds the library's own
// rules, each with its RuleTester test under lint/; specs and their test hosts are not the library.
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const noUntranslatedText = require('./lint/no-untranslated-text');
const libraryKey = require('./lint/library-key');

const spiderly = { rules: { 'no-untranslated-text': noUntranslatedText, 'library-key': libraryKey } };

module.exports = tseslint.config(
  { ignores: ['**/*.spec.ts'] },
  {
    files: ['**/*.ts'],
    languageOptions: { parser: tseslint.parser },
    processor: angular.processInlineTemplates,
    plugins: { spiderly },
    rules: { 'spiderly/library-key': 'error' },
  },
  {
    files: ['**/*.html'],
    languageOptions: { parser: angular.templateParser },
    plugins: { spiderly },
    rules: { 'spiderly/no-untranslated-text': 'error', 'spiderly/library-key': 'error' },
  },
);
