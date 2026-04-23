import { test, expect } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';

test.describe('ProjectTask Inline Management (UIOrderedOneToMany)', () => {
  let accessToken: string;
  let projectId: number;
  let taskId: number;

  test.beforeAll(async ({ request }) => {
    const tokens = await login(request);
    accessToken = tokens.accessToken;

    // Create a project to hold tasks
    const projectResponse = await request.put(
      `${API_BASE_URL}/api/Project/SaveProject`,
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
        `${API_BASE_URL}/api/Project/DeleteProject?id=${projectId}`,
        { headers: { Authorization: `Bearer ${accessToken}` } }
      );
    }
  });

  test('should create a project task via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/ProjectTask/SaveProjectTask`,
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
          orderedTaskCommentsSaveBodyDTO: [],
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
      `${API_BASE_URL}/api/ProjectTask/GetProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.internalNotes).toBeUndefined();
  });

  test('should update project task via API', async ({ request }) => {
    if (!taskId) test.skip();
    const response = await request.put(
      `${API_BASE_URL}/api/ProjectTask/SaveProjectTask`,
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
          orderedTaskCommentsSaveBodyDTO: [],
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.projectTaskDTO.title).toBe('Updated E2E Task');
    expect(result.projectTaskDTO.isCompleted).toBe(true);
    expect(result.projectTaskDTO.taskCategoryId).toBe(2);
  });

  test('should navigate to project and see tasks inline', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    await page.goto(`/project-list/${projectId}`);
    await expect(page.locator('index-card input').first()).toHaveValue('Updated E2E Task', { timeout: 10000 });
  });

  test('should delete project task via API', async ({ request }) => {
    if (!taskId) test.skip();
    const response = await request.delete(
      `${API_BASE_URL}/api/ProjectTask/DeleteProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });
});
