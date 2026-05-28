import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test, expect } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';
import { BasePage } from '../page-objects/base-page';

const mp4Fixture = readFileSync(join(__dirname, '..', 'fixtures', 'test.mp4'));

// Shared SaveProduct body — used by beforeAll's seed and by the "should
// create" test. Single source of truth so a Product schema change lands once.
const TEST_PRODUCT_BODY = {
  productDTO: {
    name: 'E2E Test Product',
    description: 'Created by Playwright E2E test',
    price: 99.99,
    stock: 100,
    isActive: true,
  },
};

test.describe('Product CRUD Operations', () => {
  let accessToken: string;
  let productId: number;
  const tableTestSeedIds: number[] = [];

  test.beforeAll(async ({ request }) => {
    const tokens = await login(request);
    accessToken = tokens.accessToken;

    // Pre-seed product so productId survives Playwright retries — same
    // rationale as project-crud.spec.ts beforeAll: an id set in a regular
    // test() doesn't always survive a retry, and the `if (!productId)
    // test.skip()` guards then silently masquerade consistent failures as
    // "flaky" with CI exit 0.
    const seedResponse = await request.put(
      `${API_BASE_URL}/api/Product/SaveProduct`,
      { headers: { Authorization: `Bearer ${accessToken}` }, data: TEST_PRODUCT_BODY }
    );
    expect(seedResponse.ok()).toBeTruthy();
    productId = (await seedResponse.json()).productDTO.id;
  });

  test('should create a new product via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/Product/SaveProduct`,
      { headers: { Authorization: `Bearer ${accessToken}` }, data: TEST_PRODUCT_BODY }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.productDTO.id).toBeDefined();
    expect(result.productDTO.name).toBe(TEST_PRODUCT_BODY.productDTO.name);
    productId = result.productDTO.id;
  });

  test('should retrieve product via API', async ({ request }) => {
    const response = await request.get(
      `${API_BASE_URL}/api/Product/GetProductList`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const products = await response.json();
    expect(Array.isArray(products)).toBeTruthy();
  });

  test('should load the application homepage', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    await expect(page).not.toHaveURL(/.*login.*/);
  });

  test('should navigate to product list page', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    const productLink = page.locator('a[href="/product-list"]');
    await expect(productLink).toBeVisible({ timeout: 10000 });
    await productLink.click();
    await page.waitForURL('**/product-list**');
  });

  test('should update product via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/Product/SaveProduct`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          productDTO: {
            id: productId,
            name: 'Updated E2E Product',
            description: 'Updated by E2E test',
            price: 149.99,
            stock: 50,
            isActive: true,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
  });

  test('should upload video/mp4 to VideoUrl via API', async ({ request }) => {
    const response = await request.post(
      `${API_BASE_URL}/api/Product/UploadVideoUrlForProduct`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        multipart: {
          file: { name: '0-test.mp4', mimeType: 'video/mp4', buffer: mp4Fixture },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const blobName = await response.text();
    expect(blobName).toBeTruthy();
  });

  test('should reject PNG content when Content-Type claims video/mp4', async ({ request }) => {
    // Valid PNG header sent with Content-Type: video/mp4 — declared type is in
    // [AcceptedFileTypes] but magic bytes disagree, so the content check must reject.
    const pngBytes = Buffer.from([
      0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
      0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
    ]);

    const response = await request.post(
      `${API_BASE_URL}/api/Product/UploadVideoUrlForProduct`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        multipart: {
          file: { name: '0-fake.mp4', mimeType: 'video/mp4', buffer: pngBytes },
        },
      }
    );
    expect(response.ok()).toBeFalsy();
    expect(response.status()).toBeGreaterThanOrEqual(400);
  });

  test('should delete product via API', async ({ request }) => {
    const response = await request.delete(
      `${API_BASE_URL}/api/Product/DeleteProduct?id=${productId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });

  test.afterAll(async ({ request }) => {
    await Promise.all(
      tableTestSeedIds.map((id) =>
        request.delete(
          `${API_BASE_URL}/api/Product/DeleteProduct?id=${id}`,
          { headers: { Authorization: `Bearer ${accessToken}` } }
        )
      )
    );
  });

  // Relies on the product-list.component.ts override shipped in e2e-fixtures/frontend/app/
  // (spiderly-cli's default list renders only the Id column, which can't exercise
  // text/numeric/boolean filters). Exercises the full stateful-table feature:
  // three filter types + multi-column sort + pagination, then a page.reload()
  // must restore every bit of state from sessionStorage.
  test('product list table restores filters, multi-sort, and pagination after refresh', async ({ page, request }) => {
    // 40 products so filters leave enough rows to span multiple pager pages:
    // 20 "Widget N" and 20 "Gadget N"; prices 10..410 step 10; stock 0..312 step 8;
    // all active so the boolean filter trivially matches.
    const seedResponses = await Promise.all(
      Array.from({ length: 40 }, (_, i) =>
        request.put(
          `${API_BASE_URL}/api/Product/SaveProduct`,
          {
            headers: { Authorization: `Bearer ${accessToken}` },
            data: {
              productDTO: {
                name: `${i % 2 === 0 ? 'Widget' : 'Gadget'} ${i}`,
                description: 'E2E table test seed',
                price: 10 + i * 10,
                stock: i * 8,
                isActive: true,
              },
            },
          }
        )
      )
    );
    for (const res of seedResponses) {
      expect(res.ok()).toBeTruthy();
      tableTestSeedIds.push((await res.json()).productDTO.id);
    }

    await authenticateBrowser(page, request);
    await page.goto('/product-list');
    await expect(page.locator('thead th').filter({ hasText: /^\s*Name\s*$/ }).first()).toBeVisible({ timeout: 15000 });

    const listPage = new BasePage(page);
    const stateKey = 'spiderly-table:/product-list';

    await listPage.applyTextFilter('Name', 'Widget');
    await listPage.applyNumericFilter('Price', 100, 'greaterThan');
    await listPage.applyBooleanFilter('IsActive', true);

    // Multi-sort: Price (asc→desc via tri-state), then Ctrl+click Stock for asc.
    await listPage.sortByColumn('Price');
    await listPage.sortByColumn('Price');
    await listPage.sortByColumn('Stock', { multi: true });

    await listPage.gotoTablePage(2);
    await page.waitForLoadState('networkidle');

    const preReload = await listPage.getSessionStorageEntry<{
      filters?: Record<string, unknown>;
      multiSortMeta?: Array<{ field: string; order: number }>;
      first?: number;
      rows?: number;
    }>(stateKey);
    expect(preReload).not.toBeNull();
    expect(preReload!.first).toBeGreaterThan(0);
    expect(preReload!.multiSortMeta?.length).toBe(2);
    expect(preReload!.multiSortMeta?.some((m) => m.field === 'price' && m.order === -1)).toBeTruthy();
    expect(preReload!.multiSortMeta?.some((m) => m.field === 'stock' && m.order === 1)).toBeTruthy();
    expect(Object.keys(preReload!.filters ?? {})).toEqual(expect.arrayContaining(['name', 'price', 'isActive']));

    await page.reload();
    await page.locator('sidebar-menu').waitFor({ state: 'visible', timeout: 15000 });
    await page.waitForLoadState('networkidle');

    const postReload = await listPage.getSessionStorageEntry<typeof preReload>(stateKey);
    expect(postReload).toEqual(preReload);
    await expect(page.locator('.p-paginator-page-selected', { hasText: '2' })).toBeVisible();

    await listPage.clearTableFilters();
    await page.waitForLoadState('networkidle');
    const afterClear = await listPage.getSessionStorageEntry(stateKey);
    expect(afterClear).toBeNull();
  });
});
