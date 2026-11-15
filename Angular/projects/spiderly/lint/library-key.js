// The library owns its strings. They live in its `spiderly` Transloco scope, with the English in
// src/lib/i18n/en.json, and an app adds its own language for them (i18n/spiderly-scope.ts). A key
// that the library reads from the app's translations shows raw in an app that never added it.
// So the rule refuses three things:
// - a literal key in `t(…)` or `translate(…)` that is not a `spiderly.` key;
// - a `spiderly.` key that en.json does not hold;
// - a component whose template reads a `spiderly.` key without the scope. Its `*transloco` would not
//   wait for the strings on a language switch.
//
// One rule for both parsers: the template nodes (`Call`, `LiteralPrimitive`) and the TypeScript nodes
// (`CallExpression`, `Literal`, `Decorator`) never meet in one file.

const { readFileSync } = require('node:fs');
const { dirname, resolve } = require('node:path');

const SCOPED = /^spiderly\.(.+)$/;
const READS_SCOPE = /\bt\(\s*['"]spiderly\./;

// `en` is the scope's English. The rule's tests pass their own, so a key en.json drops fails the
// library's lint, never the rule's tests.
const libraryKeyRule = (en) => ({
  meta: {
    type: 'problem',
    docs: { description: "Holds the library's keys in its own Transloco scope." },
    schema: [],
    messages: {
      unscoped:
        "\"{{key}}\" is read from the app's translations. Write 't('spiderly.{{key}}')' and add \"{{key}}\" with its English to the library's i18n/en.json, so the library ships it.",
      unknown: '"spiderly.{{key}}" has no English: add "{{key}}" to the library\'s i18n/en.json.',
      unprovided:
        "This component's template reads the library's strings. Add provideSpiderlyScope() to its providers, so its *transloco waits for them on a language switch.",
    },
  },
  create(context) {
    // `at` is where the report points: `{ node }` in a class, `{ loc }` in a template.
    const refuseUnscoped = (key, at) => {
      if (typeof key === 'string' && !SCOPED.test(key)) context.report({ ...at, messageId: 'unscoped', data: { key } });
    };
    const refuseUnknown = (value, at) => {
      const match = typeof value === 'string' && SCOPED.exec(value);
      if (match && !Object.hasOwn(en, match[1])) context.report({ ...at, messageId: 'unknown', data: { key: match[1] } });
    };
    // An expression node's span holds offsets into the template, not lines.
    const templateLoc = ({ sourceSpan }) => ({
      loc: {
        start: context.sourceCode.getLocFromIndex(sourceSpan.start),
        end: context.sourceCode.getLocFromIndex(sourceSpan.end),
      },
    });

    return {
      // Templates: `t('Key')`, and any `'spiderly.Key'` literal.
      Call(node) {
        const [arg] = node.args;
        if (node.receiver?.name === 't' && arg?.constructor.name === 'LiteralPrimitive') refuseUnscoped(arg.value, templateLoc(node));
      },
      LiteralPrimitive(node) {
        refuseUnknown(node.value, templateLoc(node));
      },

      // Classes: `translate('Key')`, any `'spiderly.Key'` literal, and a component's providers.
      CallExpression(node) {
        const [arg] = node.arguments;
        if (node.callee.type === 'MemberExpression' && node.callee.property.name === 'translate' && arg?.type === 'Literal') {
          refuseUnscoped(arg.value, { node: arg });
        }
      },
      Literal(node) {
        refuseUnknown(node.value, { node });
      },
      Decorator(node) {
        const call = node.expression;
        if (call.type !== 'CallExpression' || call.callee.name !== 'Component') return;
        const options = call.arguments[0];
        if (options?.type !== 'ObjectExpression') return;
        const property = (name) => options.properties.find((p) => p.key?.name === name)?.value;

        const inline = property('template');
        const url = property('templateUrl');
        const template =
          inline?.type === 'TemplateLiteral'
            ? inline.quasis.map((q) => q.value.cooked).join('')
            : inline?.type === 'Literal'
              ? String(inline.value)
              : url?.type === 'Literal'
                ? readFileSync(resolve(dirname(context.filename), url.value), 'utf8')
                : '';
        if (!READS_SCOPE.test(template)) return;

        const providers = property('providers');
        const provided = providers?.type === 'ArrayExpression' && providers.elements.some(
          (e) => e?.type === 'CallExpression' && e.callee.name === 'provideSpiderlyScope',
        );
        if (!provided) context.report({ node, messageId: 'unprovided' });
      },
    };
  },
});

module.exports = libraryKeyRule(require('../src/lib/i18n/en.json'));
module.exports.libraryKeyRule = libraryKeyRule;
