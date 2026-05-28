import { test, expect } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';

test.describe('TaskComment CRUD + Cascade Delete', () => {
  let accessToken: string;
  let projectId: number;
  let taskId: number;
  let commentId: number;

  test.beforeAll(async ({ request }) => {
    const tokens = await login(request);
    accessToken = tokens.accessToken;

    // Create project
    const projectResponse = await request.put(
      `${API_BASE_URL}/api/Project/SaveProject`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectDTO: {
            name: 'Comment Test Project',
            budget: 5000,
            maxMembers: 3,
          },
          selectedMembersNamebookDTOList: [],
          orderedProjectTasksSaveBodyDTO: [],
        },
      }
    );
    expect(projectResponse.ok()).toBeTruthy();
    projectId = (await projectResponse.json()).projectDTO.id;

    // Create task
    const taskResponse = await request.put(
      `${API_BASE_URL}/api/ProjectTask/SaveProjectTask`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectTaskDTO: {
            title: 'Comment Target Task',
            estimatedHours: 4,
            orderNumber: 1,
            projectId: projectId,
            taskCategoryId: 1,
          },
          orderedTaskCommentsSaveBodyDTO: [],
        },
      }
    );
    expect(taskResponse.ok()).toBeTruthy();
    taskId = (await taskResponse.json()).projectTaskDTO.id;

    // Pre-seed comment so commentId survives retries — same rationale as
    // project-crud.spec.ts. The "should create a task comment via API" test
    // below independently re-creates one for explicit endpoint verification.
    const commentResponse = await request.put(
      `${API_BASE_URL}/api/TaskComment/SaveTaskComment`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'E2E test comment content',
            orderNumber: 1,
            projectTaskId: taskId,
          },
        },
      }
    );
    expect(commentResponse.ok()).toBeTruthy();
    commentId = (await commentResponse.json()).taskCommentDTO.id;
  });

  test('should create a task comment via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/TaskComment/SaveTaskComment`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'E2E test comment content',
            orderNumber: 1,
            projectTaskId: taskId,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.taskCommentDTO.id).toBeDefined();
    expect(result.taskCommentDTO.content).toBe('E2E test comment content');
    commentId = result.taskCommentDTO.id;
  });

  test('should retrieve task comment list via API', async ({ request }) => {
    const response = await request.get(
      `${API_BASE_URL}/api/TaskComment/GetTaskCommentList`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const comments = await response.json();
    expect(Array.isArray(comments)).toBeTruthy();
    expect(comments.length).toBeGreaterThanOrEqual(1);
  });

  test('should navigate to task comment list page', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    const commentLink = page.locator('text=Task Comment').first();
    await expect(commentLink).toBeVisible();
    await commentLink.click();
    await page.waitForURL('**/task-comment**');
  });

  test('should update task comment via API', async ({ request }) => {
    const response = await request.put(
      `${API_BASE_URL}/api/TaskComment/SaveTaskComment`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            id: commentId,
            content: 'Updated E2E comment',
            orderNumber: 1,
            projectTaskId: taskId,
          },
        },
      }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.taskCommentDTO.content).toBe('Updated E2E comment');
  });

  test('should cascade delete comments when task is deleted', async ({ request }) => {
    // Create a second comment on the same task
    const createResponse = await request.put(
      `${API_BASE_URL}/api/TaskComment/SaveTaskComment`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'Comment to be cascade deleted',
            orderNumber: 2,
            projectTaskId: taskId,
          },
        },
      }
    );
    expect(createResponse.ok()).toBeTruthy();
    const cascadeCommentId = (await createResponse.json()).taskCommentDTO.id;

    // Delete the task — should cascade delete all comments
    const deleteTaskResponse = await request.delete(
      `${API_BASE_URL}/api/ProjectTask/DeleteProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(deleteTaskResponse.ok()).toBeTruthy();

    // Verify comment is gone
    const getResponse = await request.get(
      `${API_BASE_URL}/api/TaskComment/GetTaskComment?id=${cascadeCommentId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getResponse.ok()).toBeFalsy();

    // Also verify original comment is gone
    const getOriginalResponse = await request.get(
      `${API_BASE_URL}/api/TaskComment/GetTaskComment?id=${commentId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getOriginalResponse.ok()).toBeFalsy();
  });

  test('should cascade delete tasks when project is deleted', async ({ request }) => {
    // Create a new task for cascade test
    const taskResponse = await request.put(
      `${API_BASE_URL}/api/ProjectTask/SaveProjectTask`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectTaskDTO: {
            title: 'Cascade Delete Target',
            estimatedHours: 1,
            orderNumber: 1,
            projectId: projectId,
            taskCategoryId: 3,
          },
          orderedTaskCommentsSaveBodyDTO: [],
        },
      }
    );
    expect(taskResponse.ok()).toBeTruthy();
    const cascadeTaskId = (await taskResponse.json()).projectTaskDTO.id;

    // Create a comment on that task
    const commentResponse = await request.put(
      `${API_BASE_URL}/api/TaskComment/SaveTaskComment`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'Deep cascade target',
            orderNumber: 1,
            projectTaskId: cascadeTaskId,
          },
        },
      }
    );
    expect(commentResponse.ok()).toBeTruthy();
    const deepCommentId = (await commentResponse.json()).taskCommentDTO.id;

    // Delete the project — should cascade tasks and their comments
    const deleteResponse = await request.delete(
      `${API_BASE_URL}/api/Project/DeleteProject?id=${projectId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(deleteResponse.ok()).toBeTruthy();

    // Verify task is gone
    const getTaskResponse = await request.get(
      `${API_BASE_URL}/api/ProjectTask/GetProjectTask?id=${cascadeTaskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getTaskResponse.ok()).toBeFalsy();

    // Verify comment is gone
    const getCommentResponse = await request.get(
      `${API_BASE_URL}/api/TaskComment/GetTaskComment?id=${deepCommentId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getCommentResponse.ok()).toBeFalsy();
  });
});
