import { test, expect } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';

// Shared SaveProjectTask body builder — used by beforeAll's seed and by the
// "should create" test. projectId is injected at call time because it's only
// known after the project seed runs.
const taskBodyFor = (projectId: number) => ({
  projectTaskDTO: {
    title: 'E2E Test Task',
    description: 'A task created by E2E test',
    estimatedHours: 8.5,
    isCompleted: false,
    orderNumber: 1,
    projectId,
    taskCategoryId: 1,
  },
  orderedTaskCommentsSaveBodyDTO: [],
});

test.describe('ProjectTask Inline Management (UIOrderedOneToMany)', () => {
  let accessToken: string;
  let userId: number;
  let projectId: number;
  let taskId: number;

  test.beforeAll(async ({ request }) => {
    const tokens = await login(request);
    accessToken = tokens.accessToken;
    userId = tokens.userId;

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

    // Pre-seed task so taskId survives retries — same rationale as
    // project-crud.spec.ts. The "should create a project task via API" test
    // below independently re-creates one for explicit endpoint verification.
    const taskResponse = await request.put(
      `${API_BASE_URL}/api/ProjectTask/SaveProjectTask`,
      { headers: { Authorization: `Bearer ${accessToken}` }, data: taskBodyFor(projectId) }
    );
    expect(taskResponse.ok()).toBeTruthy();
    taskId = (await taskResponse.json()).projectTaskDTO.id;
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
    const body = taskBodyFor(projectId);
    const response = await request.put(
      `${API_BASE_URL}/api/ProjectTask/SaveProjectTask`,
      { headers: { Authorization: `Bearer ${accessToken}` }, data: body }
    );
    expect(response.ok()).toBeTruthy();
    const result = await response.json();
    expect(result.projectTaskDTO.id).toBeDefined();
    expect(result.projectTaskDTO.title).toBe(body.projectTaskDTO.title);
    expect(result.projectTaskDTO.estimatedHours).toBe(body.projectTaskDTO.estimatedHours);
    expect(result.projectTaskDTO.taskCategoryId).toBe(body.projectTaskDTO.taskCategoryId);
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
    // beforeAll pre-seeds an additional ProjectTask (so taskId survives
    // retries), so two task cards now exist. Asserting `.first()` would pick
    // the pre-seed (unchanged title); the updated card sits at a later
    // position. Playwright has no `getByDisplayValue` (that's React Testing
    // Library) and Angular reactive forms don't sync to the `value` attribute,
    // so CSS selectors don't help — scan input values via evaluateAll and
    // poll until the updated title appears.
    await expect.poll(
      () => page.locator('index-card spiderly-textbox input').evaluateAll(
        (inputs) => (inputs as HTMLInputElement[]).map((i) => i.value)
      ),
      { timeout: 10000 }
    ).toContain('Updated E2E Task');
  });

  test('should delete project task via API', async ({ request }) => {
    const response = await request.delete(
      `${API_BASE_URL}/api/ProjectTask/DeleteProjectTask?id=${taskId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(response.ok()).toBeTruthy();
  });

  // The generated admin edits ordered children INLINE on the parent form — their multiselects save
  // through SaveProject's ordered list, never through the standalone SaveProjectTask the tests above
  // use (regression background: tests/e2e-fixtures/CLAUDE.md, the composition paragraph). Uses its
  // own project because the parent-path save replaces the project's ordered children, which would
  // clobber the shared taskId above.
  test('should persist child M2M selections through the parent ordered path across add and removal', async ({ request }) => {
    const headers = { Authorization: `Bearer ${accessToken}` };

    const saveResponse = await request.put(`${API_BASE_URL}/api/Project/SaveProject`, {
      headers,
      data: {
        projectDTO: { name: 'Watcher Test Project', budget: 500, maxMembers: 3 },
        selectedMembersNamebookDTOList: [],
        orderedProjectTasksSaveBodyDTO: [
          {
            // No projectId: the parent save stamps it, which is exactly the
            // insert-parent-with-children flow the inline UI uses.
            projectTaskDTO: {
              title: 'Watched task',
              estimatedHours: 2,
              orderNumber: 1,
              taskCategoryId: 1,
            },
            selectedWatchersIds: [userId],
            orderedTaskCommentsSaveBodyDTO: [],
          },
        ],
      },
    });
    expect(saveResponse.ok()).toBeTruthy();
    const saved = await saveResponse.json();
    const watchedProjectId = saved.projectDTO.id;

    const watchersOf = (form: any): number[] =>
      form.orderedProjectTasksMainUIFormDTO[0].watchersIds;

    // Re-saves the same child through the parent path, threading the previous
    // response's DTOs forward for the version checks. taskPatch carries the one
    // thing a leg varies (e.g. selectedWatchersIds).
    const resaveWatchedTask = (from: any, taskPatch: object) =>
      request.put(`${API_BASE_URL}/api/Project/SaveProject`, {
        headers,
        data: {
          projectDTO: from.projectDTO,
          selectedMembersNamebookDTOList: [],
          orderedProjectTasksSaveBodyDTO: [
            {
              projectTaskDTO: from.orderedProjectTasksMainUIFormDTO[0].projectTaskDTO,
              ...taskPatch,
              orderedTaskCommentsSaveBodyDTO: [],
            },
          ],
        },
      });

    const readForm = async (): Promise<any> => {
      const response = await request.get(
        `${API_BASE_URL}/api/Project/GetProjectMainUIFormDTO?id=${watchedProjectId}`,
        { headers }
      );
      expect(response.ok()).toBeTruthy();
      return response.json();
    };

    try {
      // The form repopulates from the save response, so it must echo the selection...
      expect(watchersOf(saved)).toEqual([userId]);
      // ...and a fresh read must prove it persisted.
      expect(watchersOf(await readForm())).toEqual([userId]);

      // Removal leg — rationale: tests/e2e-fixtures/CLAUDE.md, the OPERATION axis.
      // NOTE there is no omitted-list no-op at this boundary: generated SaveBody
      // DTOs initialize selection lists to empty, so an omitted key deserializes
      // to [] and CLEARS the selections (this leg's first CI run proved it). The
      // null guard in Update...For... is reachable only by an explicit JSON null
      // or an internal call — do not add an omission leg expecting a no-op.
      const removalResponse = await resaveWatchedTask(saved, {
        selectedWatchersIds: [],
      });
      expect(removalResponse.ok()).toBeTruthy();
      expect(watchersOf(await removalResponse.json())).toEqual([]);
      expect(watchersOf(await readForm())).toEqual([]);
    } finally {
      await request.delete(`${API_BASE_URL}/api/Project/DeleteProject?id=${watchedProjectId}`, {
        headers,
      });
    }
  });
});
