import { defineConfig, devices } from '@playwright/test';

// Base URL of the deployed site to smoke-test. Defaults to production;
// override with E2E_BASE_URL (e.g. a SWA staging slot URL) when running
// against a preview environment.
const baseURL = process.env.E2E_BASE_URL || 'https://alpinehuts.silenced.eu';

export default defineConfig({
  testDir: './tests',
  timeout: 90 * 1000,
  expect: { timeout: 20 * 1000 },
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    locale: 'de-DE',
    viewport: { width: 1400, height: 950 },
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
