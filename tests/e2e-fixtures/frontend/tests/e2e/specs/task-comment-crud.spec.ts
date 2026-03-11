import { test, expect } from '@playwright/test';

test.describe('TaskComment CRUD + Cascade Delete', () => {
  let accessToken: string;
  let refreshToken: string;
  let projectId: number;
  let taskId: number;
  let commentId: number;

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

    // Create project
    const projectResponse = await request.put(
      'http://localhost:5000/api/Project/SaveProject',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          projectDTO: {
            name: 'Comment Test Project',
            budget: 5000,
            maxMembers: 3,
          },
        },
      }
    );
    expect(projectResponse.ok()).toBeTruthy();
    projectId = (await projectResponse.json()).projectDTO.id;

    // Create task
    const taskResponse = await request.put(
      'http://localhost:5000/api/ProjectTask/SaveProjectTask',
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
        },
      }
    );
    expect(taskResponse.ok()).toBeTruthy();
    taskId = (await taskResponse.json()).projectTaskDTO.id;
  });

  test('should create a task comment via API', async ({ request }) => {
    const response = await request.put(
      'http://localhost:5000/api/TaskComment/SaveTaskComment',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'E2E test comment content',
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
      'http://localhost:5000/api/TaskComment/GetTaskCommentList',
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
    const comments = await response.json();
    expect(Array.isArray(comments)).toBeTruthy();
    expect(comments.length).toBeGreaterThanOrEqual(1);
  });

  test('should navigate to task comment list page', async ({ page }) => {
    await page.goto('/');
    await page.evaluate(([at, rt]: [string, string]) => {
      localStorage.setItem('access_token', at);
      localStorage.setItem('refresh_token', rt);
      localStorage.setItem('browser_id', 'e2e-browser');
    }, [accessToken, refreshToken]);
    await page.reload();
    await page.waitForLoadState('networkidle');

    const commentLink = page.locator('text=Task Comment').first();
    await expect(commentLink).toBeVisible();
    await commentLink.click();
    await page.waitForURL('**/task-comment**');
  });

  test('should update task comment via API', async ({ request }) => {
    if (!commentId) test.skip();
    const response = await request.put(
      'http://localhost:5000/api/TaskComment/SaveTaskComment',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            id: commentId,
            content: 'Updated E2E comment',
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
      'http://localhost:5000/api/TaskComment/SaveTaskComment',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'Comment to be cascade deleted',
            projectTaskId: taskId,
          },
        },
      }
    );
    expect(createResponse.ok()).toBeTruthy();
    const cascadeCommentId = (await createResponse.json()).taskCommentDTO.id;

    // Delete the task — should cascade delete all comments
    const deleteTaskResponse = await request.delete(
      `http://localhost:5000/api/ProjectTask/DeleteProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(deleteTaskResponse.ok()).toBeTruthy();

    // Verify comment is gone
    const getResponse = await request.get(
      `http://localhost:5000/api/TaskComment/GetTaskComment?id=${cascadeCommentId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getResponse.ok()).toBeFalsy();

    // Also verify original comment is gone
    const getOriginalResponse = await request.get(
      `http://localhost:5000/api/TaskComment/GetTaskComment?id=${commentId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getOriginalResponse.ok()).toBeFalsy();
  });

  test('should cascade delete tasks when project is deleted', async ({ request }) => {
    // Create a new task for cascade test
    const taskResponse = await request.put(
      'http://localhost:5000/api/ProjectTask/SaveProjectTask',
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
        },
      }
    );
    expect(taskResponse.ok()).toBeTruthy();
    const cascadeTaskId = (await taskResponse.json()).projectTaskDTO.id;

    // Create a comment on that task
    const commentResponse = await request.put(
      'http://localhost:5000/api/TaskComment/SaveTaskComment',
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
          taskCommentDTO: {
            content: 'Deep cascade target',
            projectTaskId: cascadeTaskId,
          },
        },
      }
    );
    expect(commentResponse.ok()).toBeTruthy();
    const deepCommentId = (await commentResponse.json()).taskCommentDTO.id;

    // Delete the project — should cascade tasks and their comments
    const deleteResponse = await request.delete(
      `http://localhost:5000/api/Project/DeleteProject?id=${projectId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(deleteResponse.ok()).toBeTruthy();

    // Verify task is gone
    const getTaskResponse = await request.get(
      `http://localhost:5000/api/ProjectTask/GetProjectTask?id=${cascadeTaskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getTaskResponse.ok()).toBeFalsy();

    // Verify comment is gone
    const getCommentResponse = await request.get(
      `http://localhost:5000/api/TaskComment/GetTaskComment?id=${deepCommentId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(getCommentResponse.ok()).toBeFalsy();
  });
});
