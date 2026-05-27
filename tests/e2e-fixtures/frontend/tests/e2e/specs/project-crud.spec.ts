import { test, expect, Locator } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';

test.describe('Project CRUD Operations', () => {
  let accessToken: string;
  let projectId: number;

  test.beforeAll(async ({ request }) => {
    const tokens = await login(request);
    accessToken = tokens.accessToken;
  });

  test('should create a new project via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/Project/SaveProject`,
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
          selectedMembersNamebookDTOList: [],
          orderedProjectTasksSaveBodyDTO: [],
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
      `${API_BASE_URL}/api/Project/GetProjectList`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const projects = await response.json();
    expect(Array.isArray(projects)).toBeTruthy();
    expect(projects.length).toBeGreaterThanOrEqual(1);
  });

  test('should navigate to project list page', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    const projectLink = page.locator('a[href="/project-list"]');
    await expect(projectLink).toBeVisible({ timeout: 10000 });
    await projectLink.click();
    await page.waitForURL('**/project-list**');
  });

  test('should open project details page', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    await page.goto(`/project-list/${projectId}`);
    await expect(page.locator('spiderly-textbox input').first()).toHaveValue('E2E Test Project', { timeout: 10000 });
  });

  // Regression: nested [UIOrderedOneToMany] dropdowns must have options fetched in
  // the parent component's ngOnInit — browser-driven because the bug (empty options)
  // doesn't affect the API contract and is invisible to API tests.
  test('should populate dropdowns at every UIOrderedOneToMany nesting depth', async ({ page, request }) => {
    if (!projectId) test.skip();
    await authenticateBrowser(page, request);
    await page.goto(`/project-list/${projectId}`);
    await expect(page.locator('spiderly-textbox input').first()).toHaveValue('E2E Test Project', { timeout: 10000 });

    const expectSeededCategories = async (card: Locator, expectedOption: string) => {
      await card.locator('spiderly-dropdown p-select').click();
      const overlay = page.locator('.p-select-overlay');
      await expect(overlay).toBeVisible({ timeout: 5000 });
      await expect(overlay.locator('.p-select-option')).toHaveCount(5);
      await expect(overlay.getByText(expectedOption, { exact: true })).toBeVisible();
    };

    await page.locator('.panel-add-button spiderly-button button').click();
    const projectTaskCard = page.locator('index-card').first();
    await expect(projectTaskCard).toBeVisible({ timeout: 5000 });
    await expectSeededCategories(projectTaskCard, 'Bug');

    await projectTaskCard.locator('.panel-add-button spiderly-button button').click();
    const taskCommentCard = projectTaskCard.locator('index-card').first();
    await expect(taskCommentCard).toBeVisible({ timeout: 5000 });
    await expectSeededCategories(taskCommentCard, 'Feature');
  });

  // Regression: the Markdown control (spiderly-markdown) must render and preview in a real
  // app. Browser-driven because the risks here are invisible to API tests and the build — a
  // missing provideMarkdown() provider or a broken Write/Preview tab only surfaces at runtime.
  test('should render the markdown control with a live preview', async ({ page, request }) => {
    if (!projectId) test.skip();
    await authenticateBrowser(page, request);
    await page.goto(`/project-list/${projectId}`);
    await expect(page.locator('spiderly-textbox input').first()).toHaveValue('E2E Test Project', { timeout: 10000 });

    const markdown = page.locator('spiderly-markdown');
    await expect(markdown).toBeVisible({ timeout: 5000 });

    // Write tab: enter markdown source.
    await markdown.locator('textarea').fill('# Heading\n\nSome **bold** text');

    // Switch to Preview (also blurs the textarea so the blur-updated control value commits).
    await markdown.locator('p-tab:has-text("Preview")').click();

    // ngx-markdown should have rendered the source to HTML.
    const preview = markdown.locator('markdown');
    await expect(preview.locator('h1')).toHaveText('Heading');
    await expect(preview.locator('strong')).toHaveText('bold');
  });

  test('should update project via API', async ({ request }) => {
    if (!projectId) test.skip();
    const response = await request.put(
      `${API_BASE_URL}/api/Project/SaveProject`,
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
          selectedMembersNamebookDTOList: [],
          orderedProjectTasksSaveBodyDTO: [],
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
      `${API_BASE_URL}/api/Project/DeleteProject?id=${projectId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });
});
