import { test, expect } from '@playwright/test';

test.describe('ProjectTask Inline Management (UIOrderedOneToMany)', () => {
  let accessToken: string;
  let refreshToken: string;
  let projectId: number;
  let taskId: number;

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

    // Create a project to hold tasks
    const projectResponse = await request.put(
      'http://localhost:5000/api/Project/SaveProject',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectDTO: {
            name: 'Task Test Project',
            budget: 10000,
            maxMembers: 5,
          },
          selectedMembersNamebookDTOList: [],
          orderedProjectTasksSaveBodyDTO: [],
        },
      }
    );
    expect(projectResponse.ok()).toBeTruthy();
    const projectResult = await projectResponse.json();
    projectId = projectResult.projectDTO.id;
  });

  test.afterAll(async ({ request }) => {
    if (projectId) {
      await request.delete(
        `http://localhost:5000/api/Project/DeleteProject?id=${projectId}`,
        { headers: { Authorization: `Bearer ${accessToken}` } }
      );
    }
  });

  test('should create a project task via API', async ({ request }) => {
    const response = await request.put(
      'http://localhost:5000/api/ProjectTask/SaveProjectTask',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectTaskDTO: {
            title: 'E2E Test Task',
            description: 'A task created by E2E test',
            estimatedHours: 8.5,
            isCompleted: false,
            orderNumber: 1,
            projectId: projectId,
            taskCategoryId: 1,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.projectTaskDTO.id).toBeDefined();
    expect(result.projectTaskDTO.title).toBe('E2E Test Task');
    expect(result.projectTaskDTO.estimatedHours).toBe(8.5);
    expect(result.projectTaskDTO.taskCategoryId).toBe(1);
    taskId = result.projectTaskDTO.id;
  });

  test('should verify ExcludeFromDTO hides InternalNotes', async ({ request }) => {
    const response = await request.get(
      `http://localhost:5000/api/ProjectTask/GetProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.internalNotes).toBeUndefined();
  });

  test('should update project task via API', async ({ request }) => {
    if (!taskId) test.skip();
    const response = await request.put(
      'http://localhost:5000/api/ProjectTask/SaveProjectTask',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectTaskDTO: {
            id: taskId,
            title: 'Updated E2E Task',
            description: 'Updated by E2E test',
            estimatedHours: 12.0,
            isCompleted: true,
            orderNumber: 1,
            projectId: projectId,
            taskCategoryId: 2,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.projectTaskDTO.title).toBe('Updated E2E Task');
    expect(result.projectTaskDTO.isCompleted).toBe(true);
    expect(result.projectTaskDTO.taskCategoryId).toBe(2);
  });

  test('should navigate to project and see tasks inline', async ({ page }) => {
    await page.goto('/');
    await page.evaluate(([at, rt]: [string, string]) => {
      localStorage.setItem('access_token', at);
      localStorage.setItem('refresh_token', rt);
      localStorage.setItem('browser_id', 'e2e-browser');
    }, [accessToken, refreshToken]);
    await page.reload();
    await page.waitForLoadState('networkidle');

    await page.goto(`/administration/project/${projectId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('text=Updated E2E Task').first()).toBeVisible({ timeout: 10000 });
  });

  test('should delete project task via API', async ({ request }) => {
    if (!taskId) test.skip();
    const response = await request.delete(
      `http://localhost:5000/api/ProjectTask/DeleteProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });
});
