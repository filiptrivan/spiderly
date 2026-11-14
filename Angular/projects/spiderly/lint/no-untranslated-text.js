// A library template shows no static English: a consumer's admin is localized, so a hardcoded word is
// the one untranslated thing on its screen. Static text with a letter is refused: a text node, the
// value of an attribute a person reads, and a string literal a binding shows. Symbols, digits and
// punctuation hold no letter, so they pass.
//
// The selectors and `shownText` name the bundled Angular compiler's node classes (`Text$3`), as
// angular-eslint's own template rules do; they hold for the pinned angular-eslint.

const LETTER = /\p{L}/u;

// The attributes and inputs whose value a person reads. The others (`class`, `icon`, `optionLabel`…)
// hold names a person never sees.
const READ_ATTRIBUTES = new Set([
  'label',
  'placeholder',
  'title',
  'header',
  'alt',
  'aria-label',
  'ariaLabel',
  'emptyMessage',
  'emptyFilterMessage',
  'acceptLabel',
  'rejectLabel',
  'pTooltip',
]);

// `{{binding}}` is filled from `data`: ESLint reads a literal `{{ … }}` in a message as a placeholder.
const FIX =
  'Translate it with Transloco: add TranslocoDirective to the component\'s imports, wrap the template in <ng-container *transloco="let t">, and write {{binding}}, as spiderly-markdown and return-button do.';
const BINDING = "{{ t('Key') }}";

// The static text a bound expression shows: the parts around an interpolation's bindings, and a string
// literal the expression evaluates to (`'Save'`, a conditional's branch, an operand of `+` or `??`). A
// literal it only passes on (`t('Key')`, `'Key' | transloco`) or compares (`status === 'active'`) is not shown.
function shownText(ast) {
  switch (ast.constructor.name) {
    case 'Interpolation$1':
      return [...ast.strings, ...ast.expressions.flatMap(shownText)];
    case 'LiteralPrimitive':
      return typeof ast.value === 'string' ? [ast.value] : [];
    case 'Conditional':
      return [...shownText(ast.trueExp), ...shownText(ast.falseExp)];
    case 'Binary':
      return ['+', '??', '||', '&&'].includes(ast.operation) ? [...shownText(ast.left), ...shownText(ast.right)] : [];
    default:
      return [];
  }
}

module.exports = {
  meta: {
    type: 'problem',
    docs: { description: 'Refuses static text with a letter in a library template.' },
    // `allow`: a symbol written with letters (a unit, say), matched whole after trimming.
    schema: [
      {
        type: 'object',
        properties: { allow: { type: 'array', items: { type: 'string' } } },
        additionalProperties: false,
      },
    ],
    messages: {
      untranslatedText: `Static text "{{text}}" in a library template. ${FIX}`,
    },
  },
  create(context) {
    const { parserServices } = context.sourceCode;
    const allowed = new Set(context.options[0]?.allow ?? []);
    const check = (value, node) => {
      const text = value.trim();
      if (!LETTER.test(text) || allowed.has(text)) return;
      context.report({
        messageId: 'untranslatedText',
        data: { text: text.replace(/\s+/g, ' '), binding: BINDING },
        loc: parserServices.convertNodeSourceSpanToLoc(node.sourceSpan),
      });
    };
    return {
      'Text$3'(node) {
        check(node.value, node);
      },
      BoundText(node) {
        for (const text of shownText(node.value.ast)) check(text, node);
      },
      // `Element$1 >`, since a structural directive copies its element's attributes onto its template.
      'Element$1 > TextAttribute'(node) {
        if (READ_ATTRIBUTES.has(node.name)) check(node.value, node);
      },
      'Element$1 > BoundAttribute'(node) {
        if (!READ_ATTRIBUTES.has(node.name)) return;
        for (const text of shownText(node.value.ast)) check(text, node);
      },
    };
  },
};
