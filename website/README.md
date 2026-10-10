# Dapper FluentMap documentation portal

This directory contains the bilingual Astro/Starlight portal published at `https://rodri-oliveira-dev.github.io/Dapper-FluentMap/`.

Its validation and localization approach was informed by the MIT-licensed [ADR Guard documentation portal](https://github.com/rodri-oliveira-dev/adr-guard). FluentMap uses its own content, components, visual tokens, brand assets, and route model.

## Requirements

- Node.js 24 or newer
- npm (the lockfile is authoritative)

## Local development

```bash
npm ci
npm run dev
```

The English portal is served under `/Dapper-FluentMap/`; Brazilian Portuguese uses `/Dapper-FluentMap/pt-br/`.

## Validation

```bash
npm run validate
```

This command regenerates deterministic editorial pages, type-checks Astro and TypeScript, runs unit tests, compiles the checked C# portal example, creates the production build, and checks localized routes, translation parity, internal links, SEO, JSON-LD, sitemap coverage, orphan pages, and generated HTML.

Browser accessibility and Lighthouse checks run in `.github/workflows/documentation-portal.yml` against representative English and Portuguese routes. To run them locally, build the site, start `npm run preview -- --host 127.0.0.1`, then execute `npm run audit:a11y` or `npm run audit:lighthouse` in another terminal. Both audit tools are pinned in `package-lock.json`.

## Content model

- `src/content/docs/index.mdx` and its Portuguese counterpart are hand-authored landing pages.
- `scripts/generate-content.mjs` is the source for the progressive bilingual journeys. Files marked as generated must not be edited directly.
- Root `README*`, `USAGE*`, `MIGRATION*`, `COMPATIBILITY.md`, and `CHANGELOG.md` remain the authoritative technical documents. Generated pages link to these sources instead of copying full release-sensitive text.
- `scripts/check-translations.mjs` requires an equivalent Portuguese page for every English route.

## GitHub Pages

The workflow validates pull requests and deploys only after a push to `main`. In repository settings, configure **Pages → Build and deployment → Source** as **GitHub Actions**. The workflow uses the protected `github-pages` environment created by Pages and requires no long-lived deployment secret.
