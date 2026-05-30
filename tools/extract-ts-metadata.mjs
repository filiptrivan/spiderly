#!/usr/bin/env node
// Extracts TypeScript facts from the Spiderly Angular library and MERGES them into the
// framework-metadata.json produced by Spiderly.MetadataExporter (C#). Run AFTER the C# exporter:
//
//   dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json
//   node tools/extract-ts-metadata.mjs        <-- this script (adds "helpers")
//   node tools/gen-skill-docs.mjs
//
// Uses ts-morph (the TypeScript compiler API) so overloads, multi-line signatures, and (later)
// multi-file class/decorator scans are parsed robustly rather than by fragile regex.
// Signature-first: helper-functions.ts is largely undocumented, and a signature like
// `kebabToTitleCase(input: string): string` is enough to stop someone re-implementing it.

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Project, SyntaxKind } from 'ts-morph';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const metadataPath = join(repoRoot, 'framework-metadata.json');
const helpersFile = join(repoRoot, 'Angular', 'projects', 'spiderly', 'src', 'lib', 'services', 'helper-functions.ts');

function fail(msg) {
  console.error(`ERROR: extract-ts-metadata: ${msg}`);
  process.exit(1);
}

if (!existsSync(metadataPath))
  fail(`framework-metadata.json not found at ${metadataPath}. Run Spiderly.MetadataExporter (C#) first.`);
if (!existsSync(helpersFile))
  fail(`helper-functions.ts not found at ${helpersFile}.`);

const oneLine = (s) => s.replace(/\s+/g, ' ').trim();
const jsDocOf = (node) => oneLine((node.getJsDocs?.() ?? []).map((d) => d.getDescription().trim()).filter(Boolean).join(' '));
const byKey = (key) => (a, b) => (a[key] < b[key] ? -1 : a[key] > b[key] ? 1 : 0); // ordinal — matches the C# StringComparer.Ordinal side

// Syntactic only (no type checker / tsconfig needed): read parameter + return-type text as written.
const project = new Project({ skipAddingFilesFromTsConfig: true, skipFileDependencyResolution: true });
const sf = project.addSourceFileAtPath(helpersFile);

const helpers = [];

// Exported function declarations. For overloaded functions keep only the implementation
// (the overload signatures have no body), so each helper appears once.
for (const fn of sf.getFunctions()) {
  if (!fn.isExported() || !fn.isImplementation()) continue;
  const name = fn.getName();
  if (!name) continue;
  helpers.push(makeHelper(name, fn.getTypeParameters(), fn.getParameters(), fn.getReturnTypeNode(), jsDocOf(fn)));
}

// Exported arrow-function consts (callable helpers like isNullOrEmpty / selectedTab).
// Plain value consts (Symbol, arrays) are intentionally skipped — they aren't re-implementation risks.
for (const v of sf.getVariableDeclarations()) {
  if (!v.isExported()) continue;
  const init = v.getInitializer();
  if (!init || init.getKind() !== SyntaxKind.ArrowFunction) continue;
  helpers.push(makeHelper(v.getName(), init.getTypeParameters(), init.getParameters(), init.getReturnTypeNode(), jsDocOf(v.getVariableStatement())));
}

helpers.sort(byKey('name'));

// --- Built-in validators on ValidatorAbstractService ----------------------------------------------
// The abstract setValidator / setFormArrayValidator hooks are the override API (documented separately
// in the skill) — skip them; keep the concrete built-in validators (one method + arrow-fn properties).
const validatorsFile = join(repoRoot, 'Angular', 'projects', 'spiderly', 'src', 'lib', 'services', 'validator-abstract.service.ts');
if (!existsSync(validatorsFile)) fail(`validator-abstract.service.ts not found at ${validatorsFile}.`);
const validatorClass = project.addSourceFileAtPath(validatorsFile).getClassOrThrow('ValidatorAbstractService');
const validators = [];
for (const m of validatorClass.getMethods()) {
  if (m.isAbstract() || m.getScope() !== 'public') continue;
  validators.push(makeHelper(m.getName(), m.getTypeParameters(), m.getParameters(), m.getReturnTypeNode(), jsDocOf(m)));
}
for (const p of validatorClass.getProperties()) {
  const init = p.getInitializer();
  if (!init || init.getKind() !== SyntaxKind.ArrowFunction) continue;
  validators.push(makeHelper(p.getName(), init.getTypeParameters(), init.getParameters(), init.getReturnTypeNode(), jsDocOf(p)));
}
validators.sort(byKey('name'));

// --- spiderly-* form controls ---------------------------------------------------------------------
// Selector + component class + control-specific @Input()s, plus the shared BaseControl inputs once.
const inputNames = (cls) => cls.getProperties().filter((p) => p.getDecorator('Input')).map((p) => p.getName());
const controlsGlob = `${repoRoot.replace(/\\/g, '/')}/Angular/projects/spiderly/src/lib/controls/**/*.component.ts`;
const controlComponents = [];
for (const csf of project.addSourceFilesAtPaths(controlsGlob)) {
  for (const cls of csf.getClasses()) {
    const objArg = cls.getDecorator('Component')?.getArguments()[0];
    if (!objArg || objArg.getKind() !== SyntaxKind.ObjectLiteralExpression) continue;
    const selector = objArg.getProperty('selector')?.getInitializerIfKind(SyntaxKind.StringLiteral)?.getLiteralText();
    if (!selector || !selector.startsWith('spiderly-')) continue;
    controlComponents.push({ selector, component: cls.getName(), inputs: inputNames(cls) });
  }
}
controlComponents.sort(byKey('selector'));

const baseControlFile = join(repoRoot, 'Angular', 'projects', 'spiderly', 'src', 'lib', 'controls', 'base-control.ts');
const baseControlClass = existsSync(baseControlFile) ? project.addSourceFileAtPath(baseControlFile).getClass('BaseControl') : undefined;
const controls = { baseInputs: baseControlClass ? inputNames(baseControlClass).sort() : [], components: controlComponents };

function makeHelper(name, typeParams, params, returnTypeNode, description) {
  const generics = typeParams.length ? `<${typeParams.map((t) => oneLine(t.getText())).join(', ')}>` : '';
  const paramText = params.map((p) => oneLine(p.getText())).join(', ');
  const ret = returnTypeNode ? `: ${returnTypeNode.getText()}` : '';
  const helper = { name, signature: oneLine(`${name}${generics}(${paramText})${ret}`) };
  if (description) helper.description = description; // omit when absent (mirrors the C# null-omit)
  return helper;
}

const metadata = JSON.parse(readFileSync(metadataPath, 'utf8'));
metadata.helpers = helpers;
metadata.validators = validators;
metadata.controls = controls;
// LF + trailing newline so the committed artifact is byte-identical across OSes (the git-diff guard needs it).
writeFileSync(metadataPath, JSON.stringify(metadata, null, 2).replace(/\r\n/g, '\n') + '\n', 'utf8');

console.log(
  `extract-ts-metadata: merged ${helpers.length} helpers, ${validators.length} validators, ` +
  `${controls.components.length} controls into framework-metadata.json.`,
);
