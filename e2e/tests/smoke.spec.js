import { test, expect } from '@playwright/test';

// Read-only smoke tests that exercise the public site end-to-end against a
// deployed environment (see playwright.config.js baseURL / E2E_BASE_URL).
// Counts are asserted as thresholds rather than exact values so the suite
// stays stable as hut data changes over time.

test.describe('Alpine Huts public site smoke', () => {
  test('root redirects to the map and renders markers', async ({ page }) => {
    await page.goto('/');
    await page.waitForURL('**/map');
    await page.waitForSelector('#mainmap');
    await page.waitForSelector('img.leaflet-tile');
    await expect
      .poll(() => page.locator('.leaflet-marker-icon').count(), { timeout: 30000 })
      .toBeGreaterThan(100);
  });

  test('GET /api/huts returns a populated list', async ({ request }) => {
    const res = await request.get('/api/huts');
    expect(res.ok()).toBeTruthy();
    const huts = await res.json();
    expect(Array.isArray(huts)).toBe(true);
    expect(huts.length).toBeGreaterThan(100);
  });

  test('hut list page renders rows', async ({ page }) => {
    await page.goto('/hut');
    await page.waitForSelector('h1.page-title');
    await page.waitForSelector('.vue3-easy-data-table tbody tr');
    const rows = await page.locator('.vue3-easy-data-table tbody tr').count();
    expect(rows).toBeGreaterThan(50);
  });

  test('hut detail page loads with an availability response', async ({ page, request }) => {
    const huts = await (await request.get('/api/huts')).json();
    const hut = huts.find((h) => h.latitude != null && h.longitude != null && h.enabled) || huts[0];
    await page.goto(`/hut/${hut.id}`);
    await expect(page.locator('h1.hut-name')).toBeVisible();
    const avail = await request.get(`/api/huts/${hut.id}/Availability`);
    expect([200, 404]).toContain(avail.status());
  });

  test('info page renders with a GitHub link', async ({ page }) => {
    await page.goto('/info');
    await expect(page.locator('h1').first()).toBeVisible();
    await expect(page.getByText(/GitHub/i).first()).toBeVisible();
  });

  test('GET /api/availability/{today} responds', async ({ request }) => {
    const today = new Date().toISOString().split('T')[0];
    const res = await request.get(`/api/availability/${today}`);
    expect([200, 404]).toContain(res.status());
  });
});
