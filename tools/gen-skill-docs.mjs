#!/usr/bin/env node
// Generates reference docs from framework-metadata.json — the single source of truth
// produced by Spiderly.MetadataExporter. Zero dependencies.
//
//   Regenerate the whole chain:
//     dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json
//     node tools/gen-skill-docs.mjs
//
// DO NOT hand-edit the *.generated.md files this writes — change the C# source + re-run.
// CI runs this and fails on any git diff, so stale docs can never merge.
//
// Generated reference tables are pure reference, so they're always hosted under a doc
// (claude-plugins/docs/<name>/references/), never under a workflow skill.

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const metadataPath = join(repoRoot, 'framework-metadata.json');
const docsRoot = join(repoRoot, 'claude-plugins', 'docs');

// Which contract is documented in which doc, plus the generated file's name + heading.
// Add an entry when the exporter emits a new contract, or the renderer fails loud.
const ENUM_PLACEMENT = {
  ApiErrorCodes:      { doc: 'authorization',         file: 'api-error-codes.generated.md',  title: 'API error codes' },
  MatchModeCodes:     { doc: 'filtering-patterns',    file: 'match-mode-codes.generated.md', title: 'Filter match modes' },
  UIControlTypeCodes: { doc: 'angular-customization', file: 'ui-control-types.generated.md', title: 'UI control types' },
};
const CONTROLLER_PLACEMENT = {
  SecurityBaseController: { doc: 'authorization', file: 'security-endpoints.generated.md', title: 'SecurityBaseController endpoints' },
};
const ATTRIBUTES_PLACEMENT = { doc: 'entity-design', file: 'attributes.generated.md', title: 'Spiderly attributes' };
const HELPERS_PLACEMENT = { doc: 'angular-customization', file: 'helper-functions.generated.md', title: 'Shared helper functions' };
const VALIDATORS_PLACEMENT = { doc: 'angular-customization', file: 'validators.generated.md', title: 'Built-in validators' };
const CONTROLS_PLACEMENT = { doc: 'angular-customization', file: 'controls.generated.md', title: 'Form control components' };

function fail(msg) {
  console.error(`ERROR: gen-skill-docs: ${msg}`);
  process.exit(1);
}

if (!existsSync(metadataPath))
  fail(`framework-metadata.json not found at ${metadataPath}. Run Spiderly.MetadataExporter first.`);

const metadata = JSON.parse(readFileSync(metadataPath, 'utf8'));
const cell = (s) => (s ?? '').replaceAll('|', '\\|'); // escape table-breaking pipes
const header =
  `<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.\n` +
  `     Regenerate: \`dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs\` -->`;

let written = 0;

function writeRef(place, contextName, bodyLines) {
  const docDir = join(docsRoot, place.doc);
  if (!existsSync(docDir)) fail(`doc directory not found: ${docDir} (for '${contextName}').`);
  const refDir = join(docDir, 'references');
  mkdirSync(refDir, { recursive: true });
  writeFileSync(join(refDir, place.file), [header, '', `# ${place.title}`, '', ...bodyLines, ''].join('\n'), 'utf8');
  written++;
}

// One reference table's markdown body: optional intro paragraph, a header row, a divider sized to the
// header, then the pre-built data rows.
function tableBody(headers, rows, intro) {
  return [
    ...(intro ? [intro, ''] : []),
    `| ${headers.join(' | ')} |`,
    `| ${headers.map(() => '---').join(' | ')} |`,
    ...rows,
  ];
}

// Enums + const-string classes.
for (const model of metadata.enums ?? []) {
  const place = ENUM_PLACEMENT[model.name];
  if (!place) fail(`no ENUM_PLACEMENT entry for '${model.name}'. Add one (which doc hosts its reference table?).`);

  const hasValue = model.kind === 'constStringClass';
  const headers = hasValue ? ['Name', 'Value', 'Description'] : ['Name', 'Description'];
  const rows = model.members.map((m) =>
    hasValue ? `| \`${m.name}\` | \`${m.value}\` | ${cell(m.summary)} |` : `| \`${m.name}\` | ${cell(m.summary)} |`,
  );

  writeRef(place, model.name, tableBody(headers, rows, model.summary));
  console.log(`  ${model.name} -> ${place.doc}/references/${place.file} (${model.members.length} members)`);
}

// Controller endpoints.
for (const ctrl of metadata.controllers ?? []) {
  const place = CONTROLLER_PLACEMENT[ctrl.name];
  if (!place) fail(`no CONTROLLER_PLACEMENT entry for '${ctrl.name}'. Add one (which doc hosts its reference table?).`);

  const rows = ctrl.endpoints.map((e) => `| \`${e.name}\` | ${e.verb} | ${e.auth ? 'Yes' : 'No'} | ${cell(e.summary)} |`);
  writeRef(place, ctrl.name, tableBody(['Endpoint', 'Method', 'Auth', 'Description'], rows, ctrl.summary));
  console.log(`  ${ctrl.name} -> ${place.doc}/references/${place.file} (${ctrl.endpoints.length} endpoints)`);
}

// Attributes (one combined reference for the whole set).
if ((metadata.attributes ?? []).length > 0) {
  const rows = metadata.attributes.map((a) => `| \`[${a.name}]\` | ${a.target} | ${cell(a.summary)} |`);
  writeRef(ATTRIBUTES_PLACEMENT, 'attributes', tableBody(['Attribute', 'Target', 'Description'], rows));
  console.log(`  attributes -> ${ATTRIBUTES_PLACEMENT.doc}/references/${ATTRIBUTES_PLACEMENT.file} (${metadata.attributes.length} attributes)`);
}

// Helpers + validators — both signature-first (description only where JSDoc exists).
const SIGNATURE_REFS = [
  {
    items: metadata.helpers,
    place: HELPERS_PLACEMENT,
    label: 'helpers',
    header: 'Signature',
    intro: 'Reusable helpers exported from `helper-functions.ts`. Import the one you need instead of re-implementing it.',
  },
  {
    items: metadata.validators,
    place: VALIDATORS_PLACEMENT,
    label: 'validators',
    header: 'Validator',
    intro: 'Built-in validators on `ValidatorAbstractService` (call from your `setValidator` / `setFormArrayValidator` override).',
  },
];
for (const { items, place, label, header: col, intro } of SIGNATURE_REFS) {
  if (!(items ?? []).length) continue;
  const rows = items.map((x) => `| \`${cell(x.signature)}\` | ${cell(x.description ?? '')} |`);
  writeRef(place, label, tableBody([col, 'Description'], rows, intro));
  console.log(`  ${label} -> ${place.doc}/references/${place.file} (${items.length} ${label})`);
}

// Form control components.
if ((metadata.controls?.components ?? []).length > 0) {
  const intro = metadata.controls.baseInputs?.length
    ? `Every control also accepts the shared \`BaseControl\` inputs: ${metadata.controls.baseInputs.map((i) => `\`${i}\``).join(', ')}.`
    : undefined;
  const rows = metadata.controls.components.map(
    (c) => `| \`${c.selector}\` | \`${c.component}\` | ${c.inputs.map((i) => `\`${i}\``).join(', ') || '—'} |`,
  );
  writeRef(CONTROLS_PLACEMENT, 'controls', tableBody(['Selector', 'Component', 'Control-specific inputs'], rows, intro));
  console.log(`  controls -> ${CONTROLS_PLACEMENT.doc}/references/${CONTROLS_PLACEMENT.file} (${metadata.controls.components.length} controls)`);
}

console.log(`gen-skill-docs: wrote ${written} reference file(s) from framework-metadata.json.`);
