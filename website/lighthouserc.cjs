module.exports = {
  ci: {
    collect: {
      url: [
        'http://127.0.0.1:4321/Dapper-FluentMap/',
        'http://127.0.0.1:4321/Dapper-FluentMap/getting-started/quick-start/',
        'http://127.0.0.1:4321/Dapper-FluentMap/pt-br/'
      ],
      numberOfRuns: 1,
      settings: {
        chromeFlags: '--no-sandbox'
      }
    },
    assert: {
      assertions: {
        'categories:performance': ['error', { minScore: 0.9 }],
        'categories:accessibility': ['error', { minScore: 0.9 }],
        'categories:best-practices': ['error', { minScore: 0.9 }],
        'categories:seo': ['error', { minScore: 0.9 }]
      }
    }
  }
};
