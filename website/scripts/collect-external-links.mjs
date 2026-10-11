import { readFile, readdir, stat } from 'node:fs/promises';
import { resolve, sep } from 'node:path';

const dist = resolve(import.meta.dirname, '../dist');
const repositoryRoot = resolve(import.meta.dirname, '../..');
const siteUrl = 'https://rodri-oliveira-dev.github.io/Dapper-FluentMap/';
const editUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap/edit/main/';
const blobUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap/blob/main/';
const files = [];

async function walk(directory) {
  for (const name of await readdir(directory)) {
    const path = resolve(directory, name);
    (await stat(path)).isDirectory() ? await walk(path) : name.endsWith('.html') && files.push(path);
  }
}

await walk(dist);

async function validateRepositoryLink(value, prefix) {
  const target = resolve(repositoryRoot, decodeURIComponent(value.slice(prefix.length)));
  if (!target.startsWith(`${repositoryRoot}${sep}`)) {
    throw new Error(`Repository link resolves outside the repository: ${value}`);
  }
  await stat(target);
}

const links = new Set();
for (const file of files) {
  const html = await readFile(file, 'utf8');
  for (const match of html.matchAll(/(?:href|src)="([^"]+)"/g)) {
    const value = match[1].replaceAll('&amp;', '&');
    if (!value.startsWith('https://') || value.startsWith(siteUrl)) continue;

    if (value.startsWith(editUrl) || value.startsWith(blobUrl)) {
      await validateRepositoryLink(value, value.startsWith(editUrl) ? editUrl : blobUrl);
      continue;
    }

    links.add(value);
  }
}

if (links.size === 0) throw new Error('No external HTTPS links found in the generated portal.');

process.stdout.write(`${[...links].sort().join('\n')}\n`);
