// Stylelint on the library's SCSS, run by .githooks/pre-commit and CI. Each rule holds a library rule and
// names its fix; lint/stylelint.test.mjs proves each on a fixture.
import viewportInsetFallback from './lint/viewport-inset-fallback.mjs';

export default {
  customSyntax: 'postcss-scss',
  plugins: [viewportInsetFallback],
  rules: {
    'property-disallowed-list': [
      ['scroll-behavior'],
      {
        message:
          "The library sets no scroll-behavior: a global one applies to every programmatic scroll in every consuming app and ignores prefers-reduced-motion. A call site that wants an animated scroll passes behavior: 'smooth' itself.",
      },
    ],
    'spiderly/viewport-inset-fallback': true,
  },
  overrides: [
    {
      // The layout's own files: layout.scss declares the inset beside them, so it is never missing there.
      files: ['src/lib/styles/layout/_topbar.scss', 'src/lib/styles/layout/_content.scss', 'src/lib/styles/layout/_menu.scss'],
      rules: { 'spiderly/viewport-inset-fallback': null },
    },
  ],
};
