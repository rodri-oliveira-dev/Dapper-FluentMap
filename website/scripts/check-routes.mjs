import { access, readdir } from 'node:fs/promises';
import { resolve } from 'node:path';

const dist = resolve(import.meta.dirname, '../dist');
const groups = {
  'getting-started': ['introduction', 'why-fluentmap', 'installation', 'quick-start', 'first-mapping', 'configuration', 'common-questions'],
  concepts: ['entity-mapping', 'property-mapping', 'mapping-conventions', 'naming-policies', 'ignored-properties', 'configuration-validation', 'mapping-precedence'],
  advanced: ['immutable-objects', 'constructor-mapping', 'nested-objects', 'value-objects', 'mapping-profiles', 'property-converters', 'multi-mapping', 'multiple-result-sets', 'streaming', 'runtime-isolation'],
  integrations: ['dapper', 'dommel', 'dependency-injection', 'aspnet-core', 'roslyn-analyzers', 'source-generators'],
  examples: ['simple-mapping', 'legacy-database', 'aspnet-core', 'immutable-domain', 'postgresql', 'sql-server', 'sqlite', 'dependency-injection', 'generated-mapping'],
  migration: ['migrating-2-to-3', 'breaking-changes', 'compatibility-considerations', 'checklist', 'troubleshooting'],
  reference: ['api-overview', 'package-overview', 'compatibility-matrix', 'supported-providers', 'runtime-requirements', 'trimming-native-aot', 'configuration-reference', 'changelog', 'troubleshooting', 'faq']
};
const routes = ['index.html'];
for (const [group, pages] of Object.entries(groups)) {
  routes.push(`${group}/index.html`, ...pages.map((page) => `${group}/${page}/index.html`));
}
for (const route of routes) {
  await access(resolve(dist, route));
  await access(resolve(dist, 'pt-br', route));
}
const pagefind = await readdir(resolve(dist, 'pagefind'));
if (!pagefind.some((name) => name.startsWith('pagefind.'))) throw new Error('Pagefind output was not generated.');
console.log(`Verified ${routes.length * 2} bilingual routes and Pagefind output.`);
