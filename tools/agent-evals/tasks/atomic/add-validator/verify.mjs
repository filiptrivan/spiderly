import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// Recursively collect *.cs files under dir, skipping build output and deps.
function csFiles(dir) {
  const out = [];
  let entries = [];
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of entries) {
    if (e.isDirectory()) {
      if (e.name === 'bin' || e.name === 'obj' || e.name === 'node_modules') continue;
      out.push(...csFiles(join(dir, e.name)));
    } else if (e.name.endsWith('.cs')) {
      out.push(join(dir, e.name));
    }
  }
  return out;
}

// Attributes attached to the Product `Name` property (from the previous member boundary up to it),
// so a sibling property's attributes don't leak in.
function nameAttributeRegion(txt) {
  const idx = txt.search(/public\s+[^\n;{}]*\bstring\s+Name\b/);
  if (idx < 0) return '';
  const before = txt.slice(0, idx);
  const cut = Math.max(before.lastIndexOf(';'), before.lastIndexOf('{'), before.lastIndexOf('}'));
  return before.slice(cut + 1);
}

// A FluentValidation chain targeting .Name — `RuleFor(x => x.Name) ... ;`.
function nameFluentChain(txt) {
  const m = txt.match(/RuleFor\(\s*\w+\s*=>\s*\w+\.Name\s*\)[\s\S]*?;/);
  return m ? m[0] : '';
}

// `compiles` can't grade this task — the pre-task fixture already builds with a plain Name, so it
// stays green for a do-nothing agent; `name-required` / `name-max-100` are the discriminating
// checks. Accept EITHER Spiderly idiom so a correct-but-different solution isn't failed:
//   (a) DataAnnotations on the entity:        [Required] + [MaxLength(100)] / [StringLength(100)]
//   (b) a FluentValidation rule in a partial: RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
export default async function verify({ workspaceDir, run }) {
  const backend = join(workspaceDir, 'Backend');
  const build = run('dotnet', ['build'], { cwd: backend, shell: true, timeoutMs: 10 * 60 * 1000 });
  const compiles = build.code === 0;

  let requiredFound = false;
  let maxFound = false;
  for (const f of csFiles(backend)) {
    const txt = readFileSync(f, 'utf8');

    // (a) attributes on the Product entity's Name property
    if (/class\s+Product\b/.test(txt) && /\bstring\s+Name\b/.test(txt)) {
      const region = nameAttributeRegion(txt);
      if (/\[\s*Required\b/.test(region)) requiredFound = true;
      if (/\[\s*(MaxLength|StringLength)\s*\(\s*100\b/.test(region)) maxFound = true;
    }

    // (b) a hand-written FluentValidation rule for Name (in a *ValidationRules partial)
    const fluent = nameFluentChain(txt);
    if (/\.(NotEmpty|NotNull)\s*\(/.test(fluent)) requiredFound = true;
    if (/\.MaximumLength\s*\(\s*100\b/.test(fluent) || /\.Length\s*\([^)]*\b100\b/.test(fluent)) maxFound = true;

    if (requiredFound && maxFound) break; // both satisfied — skip the rest of the scan
  }

  return [
    { name: 'compiles', pass: compiles, detail: compiles ? 'dotnet build OK' : build.stderr.slice(-800) },
    { name: 'name-required', pass: requiredFound, detail: requiredFound ? 'found a required rule for Product.Name' : 'no required rule for Product.Name ([Required] or NotEmpty/NotNull)' },
    { name: 'name-max-100', pass: maxFound, detail: maxFound ? 'found a max-100 rule for Product.Name' : 'no max-length-100 rule for Product.Name ([MaxLength/StringLength(100)] or MaximumLength(100))' },
  ];
}
