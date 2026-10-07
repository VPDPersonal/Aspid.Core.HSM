// Check the Unity package for mistakes that the .NET tests cannot see: a sample path that does not exist,
// a file or folder without its .meta, an orphan .meta, build output inside the package, and a README badge
// that promises another Unity version. Only tracked files count, so local Library/ or obj/ output is ignored.
//   node scripts/check-package.mjs
import { existsSync, readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

process.chdir(fileURLToPath(new URL('..', import.meta.url)));

const PKG = 'Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM';

let errors = 0;
const fail = (file, message) => {
  console.log(`::error file=${file}::${message}`);
  errors++;
};

const pkg = JSON.parse(readFileSync(`${PKG}/package.json`, 'utf8'));

// Package Manager shows a sample whose folder is missing, and the import then fails.
for (const sample of pkg.samples ?? []) {
  if (!existsSync(`${PKG}/${sample.path}`)) fail(`${PKG}/package.json`, `sample "${sample.displayName}": ${sample.path} does not exist`);
}

// Unity ignores folders that end with "~" (Samples~, Documentation~): they and their contents need no .meta.
const files = execFileSync('git', ['ls-files', '--', PKG], { encoding: 'utf8' }).split('\n').filter(Boolean);
const tracked = new Set(files);
const assets = new Set();
for (const file of files) {
  const rel = file.slice(PKG.length + 1);
  if (rel.split('/').some((part) => part.endsWith('~'))) continue;
  if (/(^|\/)(bin|obj|Library|Temp)\//.test(rel) || rel.endsWith('.DS_Store')) fail(file, 'build output or OS file inside the package');
  if (file.endsWith('.meta')) continue;
  const parts = rel.split('/');
  for (let i = 1; i <= parts.length; i++) assets.add(`${PKG}/${parts.slice(0, i).join('/')}`);
}
for (const asset of assets) {
  if (!tracked.has(`${asset}.meta`)) fail(asset, 'has no .meta; let Unity create it, then commit it');
}
for (const file of files) {
  if (!file.endsWith('.meta') || file.split('/').some((part) => part.endsWith('~'))) continue;
  if (!assets.has(file.slice(0, -'.meta'.length)) && file !== `${PKG}.meta`) fail(file, 'orphan .meta: its file or folder is gone');
}

// The README badge must promise the same minimum Unity as package.json.
const unity = pkg.unity ?? '';
const badge = /badge\/Unity_([0-9.]+)%2B/.exec(readFileSync('README.md', 'utf8'))?.[1];
if (badge !== unity) fail('README.md', `the Unity badge says ${badge ?? 'nothing'}, package.json says ${unity}`);

if (errors) process.exit(1);
console.log(`Package OK: ${assets.size} assets with .meta, ${pkg.samples?.length ?? 0} sample(s), Unity ${unity}${pkg.unityRelease ? `.${pkg.unityRelease}` : ''}`);
