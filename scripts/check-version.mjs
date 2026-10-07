// Check that the files scripts/set-version.sh writes carry the package.json version: CHANGELOG.md must have the
// version's section, its release link and the [Unreleased] compare link from that tag. release.yml runs it:
//   node scripts/check-version.mjs
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

process.chdir(fileURLToPath(new URL('..', import.meta.url)));

const PKG = 'Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM';
const CHANGELOG = 'CHANGELOG.md';
const REPOSITORY = 'https://github.com/VPDPersonal/Aspid.Core.HSM';

const version = JSON.parse(readFileSync(`${PKG}/package.json`, 'utf8')).version ?? '';
const fix = `run scripts/set-version.sh ${version}`;
const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

let errors = 0;
const fail = (file, message) => {
  console.log(`::error file=${file}::${message}`);
  errors++;
};

if (!/^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$/.test(version)) fail(`${PKG}/package.json`, `"${version}" is not a SemVer version`);

const text = readFileSync(CHANGELOG, 'utf8');
if (!new RegExp(`^## \\[${escape(version)}\\] `, 'm').test(text)) fail(CHANGELOG, `has no "## [${version}]" section; ${fix}`);
if (!text.includes(`\n[${version}]: ${REPOSITORY}/releases/tag/v${version}\n`)) fail(CHANGELOG, `has no [${version}] release link; ${fix}`);
if (!text.includes(`\n[Unreleased]: ${REPOSITORY}/compare/v${version}...HEAD\n`)) fail(CHANGELOG, `the [Unreleased] link must compare from v${version}; ${fix}`);

if (errors) process.exit(1);
console.log(`Version files carry ${version} (#${version.includes('-') ? 'upm-preview' : 'upm'})`);
