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
});
