import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

const siteUrl = 'https://rodri-oliveira-dev.github.io/Dapper-FluentMap/';
const repositoryUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap';
const imageUrl = `${siteUrl}social/fluentmap-social.png`;

type HeadTag = 'link' | 'style' | 'title' | 'base' | 'meta' | 'script' | 'noscript' | 'template';
const tag = (name: HeadTag, attrs: Record<string, string | boolean | undefined>, content?: string) => ({ tag: name, attrs, content });

const sectionNames: Record<string, Record<string, string>> = {
  'getting-started': { en: 'Getting Started', 'pt-BR': 'Primeiros passos' },
  concepts: { en: 'Core Concepts', 'pt-BR': 'Conceitos fundamentais' },
  advanced: { en: 'Advanced Mapping', 'pt-BR': 'Mapeamento avançado' },
  integrations: { en: 'Integrations', 'pt-BR': 'Integrações' },
  examples: { en: 'Examples', 'pt-BR': 'Exemplos' },
  migration: { en: 'Migration', 'pt-BR': 'Migração' },
  reference: { en: 'Reference', 'pt-BR': 'Referência' }
};

export const onRequest = defineRouteMiddleware((context) => {
  const route = context.locals.starlightRoute;
  const title = route.entry.data.title;
  const description = route.entry.data.description ?? title;
  const canonical = new URL(context.url.pathname, siteUrl).href;
  const isNotFound = route.id === '404' || route.id.endsWith('/404');
  const isHomepage = route.id === '' || route.id === 'index' || route.id === 'pt-br' || route.id === 'pt-br/index';
  const socialAlt = route.lang === 'pt-BR'
    ? 'Dapper FluentMap — mapeamento fluente e fortemente tipado para Dapper'
    : 'Dapper FluentMap — fluent, strongly typed mapping for Dapper';

  route.head.push(
    tag('meta', { name: 'robots', content: isNotFound ? 'noindex,follow' : 'index,follow,max-image-preview:large' }),
    tag('meta', { property: 'og:type', content: isHomepage ? 'website' : 'article' }),
    tag('meta', { property: 'og:image', content: imageUrl }),
    tag('meta', { property: 'og:image:type', content: 'image/png' }),
    tag('meta', { property: 'og:image:width', content: '1200' }),
    tag('meta', { property: 'og:image:height', content: '630' }),
    tag('meta', { property: 'og:image:alt', content: socialAlt }),
    tag('meta', { name: 'twitter:title', content: title }),
    tag('meta', { name: 'twitter:description', content: description }),
    tag('meta', { name: 'twitter:image', content: imageUrl }),
    tag('meta', { name: 'twitter:image:alt', content: socialAlt })
  );
  if (isNotFound) return;

  const graph: Record<string, unknown>[] = [{
    '@type': 'SoftwareSourceCode',
    '@id': `${siteUrl}#software`,
    name: 'Dapper FluentMap',
    url: siteUrl,
    codeRepository: repositoryUrl,
    programmingLanguage: 'C#',
    runtimePlatform: '.NET Standard 2.0',
    license: `${repositoryUrl}/blob/main/LICENSE`,
    description: 'Fluent, strongly typed property-to-column mapping for Dapper.',
    inLanguage: ['en', 'pt-BR']
  }];

  if (!isHomepage) {
    const path = route.locale ? route.id.replace(new RegExp(`^${route.locale}/?`), '') : route.id;
    const section = path.split('/')[0] ?? '';
    const home = route.lang === 'pt-BR' ? `${siteUrl}pt-br/` : siteUrl;
    const items: Record<string, unknown>[] = [
      { '@type': 'ListItem', position: 1, name: 'Dapper FluentMap', item: home }
    ];
    if (sectionNames[section] && path !== section) {
      items.push({
        '@type': 'ListItem', position: 2,
        name: sectionNames[section][route.lang] ?? sectionNames[section].en,
        item: `${home}${section}/`
      });
    }
    items.push({ '@type': 'ListItem', position: items.length + 1, name: title, item: canonical });
    graph.push(
      { '@type': 'BreadcrumbList', '@id': `${canonical}#breadcrumb`, itemListElement: items },
      {
        '@type': 'TechArticle', '@id': `${canonical}#article`, headline: title,
        description, url: canonical, inLanguage: route.lang, image: imageUrl,
        author: { '@type': 'Organization', name: 'Dapper.FluentMap contributors', url: repositoryUrl },
        about: { '@id': `${siteUrl}#software` },
        isPartOf: { '@id': `${siteUrl}#software` }
      }
    );
  }

  const structured = JSON.stringify({ '@context': 'https://schema.org', '@graph': graph }).replaceAll('<', '\\u003c');
  route.head.push(tag('script', { type: 'application/ld+json' }, structured));
});
