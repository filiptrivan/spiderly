import { test, expect } from '@playwright/test';
import { ProjectListPage } from './page-objects/project-list-page';

test.describe('Project CRUD Operations', () => {
  let accessToken: string;
  let refreshToken: string;
  let projectId: number;

  test.beforeAll(async ({ request }) => {
    const sendCodeResponse = await request.post(
      'http://localhost:5000/api/Security/SendLoginVerificationEmail',
      { data: { email: 'test@e2e.com', browserId: 'e2e-browser' } }
    );
    expect(sendCodeResponse.ok()).toBeTruthy();
    const sendCodeResult = await sendCodeResponse.json();
    const verificationCode = sendCodeResult.verificationCode;
    expect(verificationCode).toBeTruthy();

    const loginResponse = await request.post(
      'http://localhost:5000/api/Security/Login',
      {
        data: {
          verificationCode,
          email: 'test@e2e.com',
          browserId: 'e2e-browser',
        },
      }
    );
    expect(loginResponse.ok()).toBeTruthy();
    const loginResult = await loginResponse.json();
    accessToken = loginResult.accessToken;
    refreshToken = loginResult.refreshToken;
    expect(accessToken).toBeTruthy();
  });

  async function authenticateBrowser(page: any) {
    await page.goto('/');
    await page.evaluate(([at, rt]: [string, string]) => {
      localStorage.setItem('access_token', at);
      localStorage.setItem('refresh_token', rt);
      localStorage.setItem('browser_id', 'e2e-browser');
    }, [accessToken, refreshToken]);
    await page.reload();
    await page.waitForLoadState('networkidle');
  }

  test('should create a new project via API', async ({ request }) => {
    const response = await request.put(
      'http://localhost:5000/api/Project/SaveProject',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectDTO: {
            name: 'E2E Test Project',
            description: 'Created by Playwright E2E test',
            budget: 50000.50,
            maxMembers: 10,
            isArchived: false,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.projectDTO.id).toBeDefined();
    expect(result.projectDTO.name).toBe('E2E Test Project');
    expect(result.projectDTO.budget).toBe(50000.50);
    expect(result.projectDTO.maxMembers).toBe(10);
    projectId = result.projectDTO.id;
  });

  test('should retrieve project list via API', async ({ request }) => {
    const response = await request.get(
      'http://localhost:5000/api/Project/GetProjectList',
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const projects = await response.json();
    expect(Array.isArray(projects)).toBeTruthy();
    expect(projects.length).toBeGreaterThanOrEqual(1);
  });

  test('should navigate to project list page', async ({ page }) => {
    await authenticateBrowser(page);
    const projectLink = page.locator('text=Project').first();
    await expect(projectLink).toBeVisible();
    await projectLink.click();
    await page.waitForURL('**/project**');
  });

  test('should open project details page', async ({ page }) => {
    await authenticateBrowser(page);
    const projectListPage = new ProjectListPage(page);
    await projectListPage.goto();
    await projectListPage.openProject('E2E Test Project');
    await expect(page.locator('input[value="E2E Test Project"]').or(page.getByLabel('Name').and(page.locator('[value="E2E Test Project"]')))).toBeVisible({ timeout: 10000 });
  });

  test('should update project via API', async ({ request }) => {
    if (!projectId) test.skip();
    const response = await request.put(
      'http://localhost:5000/api/Project/SaveProject',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectDTO: {
            id: projectId,
            name: 'Updated E2E Project',
            description: 'Updated by E2E test',
            budget: 75000.00,
            maxMembers: 15,
            isArchived: false,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.projectDTO.name).toBe('Updated E2E Project');
    expect(result.projectDTO.budget).toBe(75000.00);
  });

  test('should delete project via API', async ({ request }) => {
    if (!projectId) test.skip();
    const response = await request.delete(
      `http://localhost:5000/api/Project/DeleteProject?id=${projectId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });
});
