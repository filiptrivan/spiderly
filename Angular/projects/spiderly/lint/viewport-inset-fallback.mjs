// A component reads the viewport inset as `var(--spiderly-viewport-top-inset, 0px)`: a library component
// works without Spiderly's layout, which declares the variable, and without the fallback a consumer with
// its own shell gets a phantom gap. stylelint.config.mjs exempts the layout files beside the declaration.

import stylelint from 'stylelint';
import valueParser from 'postcss-value-parser';

const {
  createPlugin,
  utils: { report, ruleMessages, validateOptions },
} = stylelint;

const ruleName = 'spiderly/viewport-inset-fallback';
const INSET = '--spiderly-viewport-top-inset';

const messages = ruleMessages(ruleName, {
  rejected: (read) =>
    `${read} has no 0px fallback. Write var(${INSET}, 0px): a library component works without Spiderly's layout, and without the fallback a consumer with its own shell gets a phantom gap.`,
});

function hasZeroFallback(varNode) {
  const [, comma, ...fallback] = varNode.nodes.filter((n) => n.type !== 'space' && n.type !== 'comment');
  return comma?.type === 'div' && comma.value === ',' && fallback.length === 1 && fallback[0].value === '0px';
}

const ruleFunction = (primary) => (root, result) => {
  if (!validateOptions(result, ruleName, { actual: primary })) return;
  root.walkDecls((decl) => {
    if (!decl.value.includes(INSET)) return;
    valueParser(decl.value).walk((node) => {
      if (node.type !== 'function' || node.value.toLowerCase() !== 'var') return;
      if (node.nodes[0]?.value !== INSET || hasZeroFallback(node)) return;
      const read = valueParser.stringify(node);
      report({ message: messages.rejected(read), node: decl, word: read, result, ruleName });
    });
  });
};

ruleFunction.ruleName = ruleName;
ruleFunction.messages = messages;

export default createPlugin(ruleName, ruleFunction);
