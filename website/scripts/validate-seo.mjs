import { readFile, readdir, stat } from 'node:fs/promises';
import { relative, resolve, sep } from 'node:path';

const dist = resolve(import.meta.dirname, '../dist');
const site = 'https://rodri-oliveira-dev.github.io';
const basePath = '/Dapper-FluentMap/';
const failures = [];
const pages = [];

async function walk(directory) {
  for (const name of await readdir(directory)) {
    const path = resolve(directory, name);
    if ((await stat(path)).isDirectory()) await walk(path);
    else if (name.endsWith('.html')) pages.push(path);
  }
}
const attributes = (source) => Object.fromEntries([...source.matchAll(/([:\w-]+)="([^"]*)"/g)].map((match) => [match[1], match[2]]));
const tags = (html, name) => [...html.matchAll(new RegExp(`<${name}\\b([^>]*)>`, 'gi'))].map((match) => attributes(match[1]));
const meta = (html, key, value) => tags(html, 'meta').filter((entry) => entry[key] === value);
const links = (html, rel) => tags(html, 'link').filter((entry) => entry.rel === rel);
const fail = (file, message) => failures.push(`${relative(dist, file).split(sep).join('/')}: ${message}`);
function routeFor(file) {
  const path = relative(dist, file).split(sep).join('/');
  if (path === 'index.html') return basePath;
  if (path === '404.html') return `${basePath}404/`;
  if (path.endsWith('/index.html')) return `${basePath}${path.slice(0, -'index.html'.length)}`;
  return `${basePath}${path}`;
}

await walk(dist);
const indexable = new Map();
const titles = new Map();
const descriptions = new Map();
const linked = new Set();
for (const file of pages) {
  const html = await readFile(file, 'utf8');
  const route = routeFor(file);
  const expected = `${site}${route}`;
  const pt = route.startsWith(`${basePath}pt-br/`);
  const notFound = /\/404(?:\.html|\/)$/.test(route);
  const lang = html.match(/<html\b[^>]*\blang="([^"]+)"/i)?.[1];
  const title = html.match(/<title>([\s\S]*?)<\/title>/i)?.[1]?.trim();
  const description = meta(html, 'name', 'description');
  const canonical = links(html, 'canonical');
  const robots = meta(html, 'name', 'robots');
  const alternates = links(html, 'alternate').filter((entry) => entry.hreflang);

  if (lang !== (pt ? 'pt-BR' : 'en')) fail(file, `expected lang ${pt ? 'pt-BR' : 'en'}, found ${lang ?? 'none'}`);
  if (!title) fail(file, 'missing title');
  if (description.length !== 1 || !description[0].content) fail(file, 'missing unique meta description');
  if (canonical.length !== 1 || canonical[0].href !== expected) fail(file, `canonical must be ${expected}`);
  if (robots.length !== 1) fail(file, 'expected one robots meta');
  if (notFound && !robots[0]?.content?.includes('noindex')) fail(file, '404 must be noindex');

  for (const [key, value] of [['property', 'og:title'], ['property', 'og:description'], ['property', 'og:url'], ['property', 'og:image'], ['name', 'twitter:card'], ['name', 'twitter:title'], ['name', 'twitter:description'], ['name', 'twitter:image']]) {
    const matches = meta(html, key, value);
    if (matches.length !== 1 || !matches[0].content) fail(file, `missing ${value}`);
  }
  for (const anchor of tags(html, 'a')) {
    const href = anchor.href;
    if (href?.startsWith(`${site}${basePath}`)) linked.add(href);
    else if (href?.startsWith(basePath)) linked.add(`${site}${href}`);
  }
  for (const entry of [...tags(html, 'a'), ...tags(html, 'link'), ...tags(html, 'script'), ...tags(html, 'img')]) {
    const target = entry.href ?? entry.src;
    if (target?.startsWith('/') && !target.startsWith(basePath)) fail(file, `root-relative URL escapes base path: ${target}`);
  }
  if (notFound) continue;
  indexable.set(expected, { file, alternates });
  const hreflang = Object.fromEntries(alternates.map((entry) => [entry.hreflang, entry.href]));
  const english = pt ? expected.replace(`${basePath}pt-br/`, basePath) : expected;
  const portuguese = pt ? expected : expected.replace(basePath, `${basePath}pt-br/`);
  if (hreflang.en !== english || hreflang['pt-BR'] !== portuguese || hreflang['x-default'] !== english) fail(file, 'invalid reciprocal hreflang cluster');
  const scripts = [...html.matchAll(/<script\b[^>]*type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/gi)];
  if (scripts.length !== 1) fail(file, `expected one JSON-LD block, found ${scripts.length}`);
  else {
    try {
      const graph = JSON.parse(scripts[0][1])['@graph'];
      const types = new Set(graph.flatMap((item) => Array.isArray(item['@type']) ? item['@type'] : [item['@type']]));
      if (!types.has('SoftwareSourceCode')) fail(file, 'JSON-LD missing SoftwareSourceCode');
      if (route !== basePath && route !== `${basePath}pt-br/`) for (const type of ['TechArticle', 'BreadcrumbList']) if (!types.has(type)) fail(file, `JSON-LD missing ${type}`);
    } catch (error) { fail(file, `invalid JSON-LD: ${error.message}`); }
  }
  if (title) { const owners = titles.get(title) ?? []; owners.push(file); titles.set(title, owners); }
  if (description[0]?.content) { const owners = descriptions.get(description[0].content) ?? []; owners.push(file); descriptions.set(description[0].content, owners); }
}

for (const page of indexable.values()) for (const alternate of page.alternates) if (!indexable.has(alternate.href)) fail(page.file, `hreflang target is missing: ${alternate.href}`);
for (const [value, owners] of [...titles, ...descriptions]) if (owners.length > 1) failures.push(`duplicate metadata on ${owners.map((file) => relative(dist, file)).join(', ')}: ${value}`);

const sitemapIndex = await readFile(resolve(dist, 'sitemap-index.xml'), 'utf8');
const sitemapFiles = [...sitemapIndex.matchAll(/<loc>[^<]*\/(sitemap-[^<]+\.xml)<\/loc>/g)].map((match) => match[1]);
const sitemapUrls = new Set();
for (const sitemapFile of sitemapFiles) {
  const xml = await readFile(resolve(dist, sitemapFile), 'utf8');
  for (const block of xml.matchAll(/<url>([\s\S]*?)<\/url>/g)) {
    const location = block[1].match(/<loc>([^<]+)<\/loc>/)?.[1];
    if (location) sitemapUrls.add(location);
  }
}
for (const url of indexable.keys()) {
  if (!sitemapUrls.has(url)) failures.push(`sitemap missing ${url}`);
  if (url !== `${site}${basePath}` && url !== `${site}${basePath}pt-br/` && !linked.has(url)) failures.push(`orphan page: ${url}`);
}
if (failures.length) throw new Error(`SEO validation failed (${failures.length}):\n${failures.slice(0, 100).join('\n')}`);
console.log(`Validated metadata, JSON-LD, hreflang, sitemap, and orphan coverage for ${indexable.size} pages.`);
