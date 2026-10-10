import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');

describe('portal configuration', () => {
  it('uses the repository GitHub Pages base path and both locales', () => {
    const config = readFileSync(resolve(root, 'astro.config.mjs'), 'utf8');
    expect(config).toContain("base: '/Dapper-FluentMap'");
    expect(config).toContain("'pt-br': { label: 'Português (Brasil)', lang: 'pt-BR' }");
  });

  it('keeps compatibility-sensitive reference pages tied to source documents', () => {
    const generator = readFileSync(resolve(root, 'scripts/generate-content.mjs'), 'utf8');
    expect(generator).toContain('COMPATIBILITY.md');
    expect(generator).toContain('MIGRATION.pt-BR.md');
    expect(generator).toContain('USAGE.md');
  });

  it('keeps the quick start executable from registration through query', () => {
    const quickStart = readFileSync(
      resolve(root, 'src/content/docs/getting-started/quick-start.md'),
      'utf8'
    );
    expect(quickStart).toContain('FluentMapper.Initialize');
    expect(quickStart).toContain('FluentMapper.Validate');
    expect(quickStart).toContain('connection.QuerySingle<Customer>');
    expect(quickStart).toContain('Microsoft.Data.Sqlite');
  });

  it.each([
    ['postgresql', 'Npgsql'],
    ['sql-server', 'Microsoft.Data.SqlClient'],
    ['sqlite', 'Microsoft.Data.Sqlite']
  ])('lists the ADO.NET package required by the %s example', (slug, packageName) => {
    const example = readFileSync(resolve(root, `src/content/docs/examples/${slug}.md`), 'utf8');
    expect(example).toContain('Dapper.FluentMap');
    expect(example).toContain(packageName);
  });
});
