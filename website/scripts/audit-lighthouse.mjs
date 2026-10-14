import { createRequire } from 'node:module';

import { launch } from 'chrome-launcher';
import lighthouse from 'lighthouse';

const require = createRequire(import.meta.url);
const { ci } = require('../lighthouserc.cjs');
const assertions = ci.assert.assertions;
const auditedCategories = Object.keys(assertions)
  .filter((assertion) => assertion.startsWith('categories:'))
  .map((assertion) => assertion.replace('categories:', ''));
const failures = [];
const configuredChromeFlags = ci.collect.settings.chromeFlags.split(/\s+/).filter(Boolean);

const chrome = await launch({
  chromeFlags: ['--headless=new', ...configuredChromeFlags],
  chromePath: process.env.CHROME_PATH || process.env.PUPPETEER_EXECUTABLE_PATH
});

try {
  for (const url of ci.collect.url) {
    for (let run = 0; run < ci.collect.numberOfRuns; run += 1) {
      const result = await lighthouse(url, {
        logLevel: 'info',
        output: 'json',
        port: chrome.port,
        onlyCategories: auditedCategories
      });

      if (!result) {
        failures.push(`${url}: Lighthouse did not return a report`);
        continue;
      }

      for (const [assertion, [, options]] of Object.entries(assertions)) {
        const categoryId = assertion.replace('categories:', '');
        const score = result.lhr.categories[categoryId]?.score;

        if (typeof score !== 'number' || score < options.minScore) {
          failures.push(
            `${url}: ${categoryId} score ${score ?? 'missing'} is below ${options.minScore}`
          );
        }
      }
    }
  }
} finally {
  chrome.kill();
}

if (failures.length > 0) {
  console.error(failures.join('\n'));
  process.exitCode = 1;
}
