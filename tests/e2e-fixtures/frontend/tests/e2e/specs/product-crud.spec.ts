import { test, expect } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';

test.describe('Product CRUD Operations', () => {
  let accessToken: string;
  let productId: number;

  test.beforeAll(async ({ request }) => {
    const tokens = await login(request);
    accessToken = tokens.accessToken;
  });

  test('should create a new product via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/Product/SaveProduct`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          productDTO: {
            name: 'E2E Test Product',
            description: 'Created by Playwright E2E test',
            price: 99.99,
            stock: 100,
            isActive: true,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.productDTO.id).toBeDefined();
    expect(result.productDTO.name).toBe('E2E Test Product');
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
    if (!productId) test.skip();
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
    // Minimal valid ISO-BMFF MP4: ftyp box at offset 4 (`ftyp` + `isom` brand).
    // Mime-Detective's default pack recognizes this as video/mp4.
    const mp4Bytes = Buffer.from([
      0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70,
      0x69, 0x73, 0x6f, 0x6d, 0x00, 0x00, 0x02, 0x00,
      0x69, 0x73, 0x6f, 0x6d, 0x69, 0x73, 0x6f, 0x32,
      0x61, 0x76, 0x63, 0x31, 0x6d, 0x70, 0x34, 0x31,
    ]);

    const response = await request.post(
      `${API_BASE_URL}/api/Product/UploadVideoUrlForProduct`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        multipart: {
          file: { name: 'test.mp4', mimeType: 'video/mp4', buffer: mp4Bytes },
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
          file: { name: 'fake.mp4', mimeType: 'video/mp4', buffer: pngBytes },
        },
      }
    );
    expect(response.ok()).toBeFalsy();
    expect(response.status()).toBeGreaterThanOrEqual(400);
  });

  test('should delete product via API', async ({ request }) => {
    if (!productId) test.skip();
    const response = await request.delete(
      `${API_BASE_URL}/api/Product/DeleteProduct?id=${productId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });
});
