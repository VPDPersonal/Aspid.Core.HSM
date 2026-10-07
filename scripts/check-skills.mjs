// Check the SKILL.md frontmatter of the repo skills in .claude/skills/ with a strict YAML parser,
// as Claude Code and other agents read it. Needs js-yaml:
//   npm install --no-save js-yaml@4 && node scripts/check-skills.mjs
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import yaml from 'js-yaml';

process.chdir(fileURLToPath(new URL('..', import.meta.url)));

const ROOT = '.claude/skills';

let errors = 0;
const fail = (file, message) => {
  console.log(`::error file=${file}::${message}`);
  errors++;
};

const names = readdirSync(ROOT, { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name);
for (const name of names) {
  const file = join(ROOT, name, 'SKILL.md');
  if (!existsSync(file)) {
    fail(join(ROOT, name), 'missing SKILL.md');
    continue;
  }
  const match = readFileSync(file, 'utf8').match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n/);
  if (!match) {
    fail(file, 'no YAML frontmatter');
    continue;
  }
  let data;
  try {
    data = yaml.load(match[1]);
  } catch (e) {
    fail(file, `invalid YAML frontmatter: ${e.reason ?? e.message}`);
    continue;
  }
  // Limits of the Agent Skills spec (agentskills.io); Codex counts the description in bytes and skips a longer skill.
  if (data?.name !== name) fail(file, `name "${data?.name}" must match the folder name "${name}"`);
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(name) || name.length > 64) fail(file, 'name must be kebab-case, at most 64 characters');
  if (typeof data?.description !== 'string' || !data.description.trim()) fail(file, 'description is missing');
  else if (Buffer.byteLength(data.description) > 1024) fail(file, 'description exceeds 1024 bytes');
}

if (errors) process.exit(1);
console.log(`Skill frontmatter OK (${names.length} skills)`);
