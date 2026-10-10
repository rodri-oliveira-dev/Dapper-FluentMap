import { readdir, readFile, stat } from 'node:fs/promises';
import { relative, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '../src/content/docs');
const ptRoot = resolve(root, 'pt-br');
const failures = [];
const english = [];
async function walk(directory) {
  for (const name of await readdir(directory)) {
    const path = resolve(directory, name);
    if ((await stat(path)).isDirectory()) await walk(path);
    else if (/\.mdx?$/.test(name) && !path.startsWith(ptRoot)) english.push(path);
  }
}
await walk(root);
for (const source of english) {
  const rel = relative(root, source);
  const translated = resolve(ptRoot, rel);
  try {
    const [en, pt] = await Promise.all([readFile(source, 'utf8'), readFile(translated, 'utf8')]);
    if (!/^---[\s\S]*?title:\s*.+/m.test(pt)) failures.push(`${rel}: translated page has no title`);
    if (!/^---[\s\S]*?description:\s*.+/m.test(pt)) failures.push(`${rel}: translated page has no description`);
    if (en === pt) failures.push(`${rel}: English and Portuguese files are identical`);
  } catch { failures.push(`${rel}: missing pt-br counterpart`); }
}
if (failures.length) throw new Error(`Translation parity failed:\n${failures.join('\n')}`);
console.log(`Verified translation parity for ${english.length} English pages.`);
