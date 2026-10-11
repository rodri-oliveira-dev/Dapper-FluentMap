import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import starlight from '@astrojs/starlight';

const repository = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap';

const localized = (en, pt) => ({ label: en, translations: { 'pt-BR': pt } });
const section = (en, pt, directory) => ({
  ...localized(en, pt),
  items: [{ autogenerate: { directory } }]
});

export default defineConfig({
  site: 'https://rodri-oliveira-dev.github.io',
  base: '/Dapper-FluentMap',
  trailingSlash: 'always',
  integrations: [
    sitemap({
      filter: (page) => !/\/404(?:\.html|\/)$/.test(new URL(page).pathname),
      i18n: { defaultLocale: 'en', locales: { en: 'en', 'pt-br': 'pt-BR' } }
    }),
    starlight({
      title: 'Dapper FluentMap',
      description: 'Fluent, strongly typed column mapping for Dapper without persistence attributes in domain models.',
      logo: { src: './src/assets/logo.svg', replacesTitle: true },
      favicon: '/favicon.svg',
      social: [{ icon: 'github', label: 'GitHub', href: repository }],
      editLink: { baseUrl: `${repository}/edit/main/website/` },
      lastUpdated: true,
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        'pt-br': { label: 'Português (Brasil)', lang: 'pt-BR' }
      },
      customCss: ['./src/styles/tokens.css', './src/styles/custom.css'],
      routeMiddleware: './src/routeData.ts',
      sidebar: [
        { ...localized('Home', 'Início'), slug: 'index' },
        section('Getting Started', 'Primeiros passos', 'getting-started'),
        section('Core Concepts', 'Conceitos fundamentais', 'concepts'),
        section('Advanced Mapping', 'Mapeamento avançado', 'advanced'),
        section('Integrations', 'Integrações', 'integrations'),
        section('Examples', 'Exemplos', 'examples'),
        section('Migration', 'Migração', 'migration'),
        section('Reference', 'Referência', 'reference')
      ]
    })
  ],
  markdown: {
    shikiConfig: { themes: { light: 'github-light', dark: 'github-dark' } }
  },
  vite: { build: { cssMinify: 'lightningcss' } }
});
