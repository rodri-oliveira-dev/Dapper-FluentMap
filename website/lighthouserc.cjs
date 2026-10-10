module.exports = {
  ci: {
    collect: {
      staticDistDir: './dist',
      url: [
        'http://localhost/Dapper-FluentMap/',
        'http://localhost/Dapper-FluentMap/getting-started/quick-start/',
        'http://localhost/Dapper-FluentMap/pt-br/'
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
    },
    upload: { target: 'temporary-public-storage' }
  }
};
