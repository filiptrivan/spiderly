import { test, expect, APIRequestContext } from '@playwright/test';
import { login, authenticateBrowser, API_BASE_URL } from '../helpers/auth';

// Exercises native one-to-one ([WithOne]) support end-to-end against the real
// backend + Postgres. ProjectCharter is the dependent (holds the FK); Project is
// the principal. Most assertions are API-level (the relationship semantics live
// in the backend); the final browser test guards the UI regression where a 1-1
// dependent rendered a broken `Unknown UIControlType: 'None'` instead of the
// autocomplete it now reuses from many-to-one.

const charterBodyFor = (title: string, charteredProjectId?: number) => ({
  projectCharterDTO: {
    title,
    scope: 'Charter scope authored by E2E test',
    ...(charteredProjectId !== undefined ? { charteredProjectId } : {}),
  },
});

const saveCharter = (request: APIRequestContext, accessToken: string, body: object) =>
  request.put(`${API_BASE_URL}/api/ProjectCharter/SaveProjectCharter`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: body,
  });

const createProject = async (request: APIRequestContext, accessToken: string, name: string): Promise<number> => {
  const res = await request.put(`${API_BASE_URL}/api/Project/SaveProject`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: {
      projectDTO: { name, budget: 10000, maxMembers: 5 },
      selectedMembersNamebookDTOList: [],
      orderedProjectTasksSaveBodyDTO: [],
    },
  });
  expect(res.ok()).toBeTruthy();
  return (await res.json()).projectDTO.id;
};

test.describe('ProjectCharter — native one-to-one ([WithOne])', () => {
  let accessToken: string;
  let mainProjectId: number;
  let mainCharterId: number;
  const orphanCharterIds: number[] = [];

  test.beforeAll(async ({ request }) => {
    accessToken = (await login(request)).accessToken;
    mainProjectId = await createProject(request, accessToken, 'Charter Test Project');

    // Pre-seed the chartered row in beforeAll so mainCharterId survives Playwright
    // retries (same rationale as project-task-crud.spec.ts's task pre-seed).
    const res = await saveCharter(request, accessToken, charterBodyFor('Main Charter', mainProjectId));
    expect(res.ok()).toBeTruthy();
    mainCharterId = (await res.json()).projectCharterDTO.id;
  });

  test.afterAll(async ({ request }) => {
    // Deleting the project cascades its charter (mainCharterId) — see the cascade test.
    if (mainProjectId) {
      await request.delete(`${API_BASE_URL}/api/Project/DeleteProject?id=${mainProjectId}`, {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
    }
    for (const id of orphanCharterIds) {
      await request.delete(`${API_BASE_URL}/api/ProjectCharter/DeleteProjectCharter?id=${id}`, {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
    }
  });

  // #2 — the dependent FK round-trips through the flattened DTO (charteredProjectId).
  test('dependent FK round-trips on the DTO', async ({ request }) => {
    const res = await request.get(
      `${API_BASE_URL}/api/ProjectCharter/GetProjectCharter?id=${mainCharterId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(res.ok()).toBeTruthy();
    const dto = await res.json();
    expect(dto.charteredProjectId).toBe(mainProjectId);
    // The principal's DisplayName is flattened onto the dependent DTO.
    expect(dto.charteredProjectDisplayName).toBe('Charter Test Project');
  });

  // #3 — optional 1-1: many un-chartered rows (null FK) must coexist. This is the
  // conclusive proof of the multi-NULL guarantee on REAL Postgres (NULLS DISTINCT);
  // a unique index that collapsed NULLs would reject the second insert.
  test('multiple un-chartered rows (null FK) all save — multi-NULL on Postgres', async ({ request }) => {
    const a = await saveCharter(request, accessToken, charterBodyFor('Unowned Charter A'));
    const b = await saveCharter(request, accessToken, charterBodyFor('Unowned Charter B'));
    expect(a.ok()).toBeTruthy();
    expect(b.ok()).toBeTruthy();
    const aId = (await a.json()).projectCharterDTO.id;
    const bId = (await b.json()).projectCharterDTO.id;
    expect(aId).not.toBe(bId);
    orphanCharterIds.push(aId, bId);
  });

  // #4 (optional) — the unique FK index rejects a second charter for the same project,
  // surfaced as a localized 4xx by the generic constraint handler (not a raw 500).
  test('a second charter for the same project is rejected', async ({ request }) => {
    const res = await saveCharter(request, accessToken, charterBodyFor('Duplicate Charter', mainProjectId));
    expect(res.ok()).toBeFalsy();
    expect(res.status()).toBeGreaterThanOrEqual(400);
    expect(res.status()).toBeLessThan(500); // clean client error, never a 500
    if (res.ok()) orphanCharterIds.push((await res.json()).projectCharterDTO.id);
  });

  // #5 — app-layer cascade: deleting the principal deletes its 1-1 dependent.
  test('deleting a project cascades its charter', async ({ request }) => {
    const projectBId = await createProject(request, accessToken, 'Cascade Project B');
    const saved = await saveCharter(request, accessToken, charterBodyFor('Charter B', projectBId));
    expect(saved.ok()).toBeTruthy();
    const charterBId = (await saved.json()).projectCharterDTO.id;

    const del = await request.delete(`${API_BASE_URL}/api/Project/DeleteProject?id=${projectBId}`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    expect(del.ok()).toBeTruthy();

    const after = await request.get(
      `${API_BASE_URL}/api/ProjectCharter/GetProjectCharter?id=${charterBId}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    expect(after.ok()).toBeFalsy(); // charter was cascade-deleted with its project
  });

  // #1 — the regression that escaped 374 unit tests + review: the dependent's nav must
  // render the reused many-to-one autocomplete, NOT `Unknown UIControlType: 'None'`.
  test('charter details renders the autocomplete (not a broken None control)', async ({ page, request }) => {
    await authenticateBrowser(page, request);
    await page.goto(`/project-charter-list/${mainCharterId}`);

    await expect(page.locator('spiderly-autocomplete')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('body')).not.toContainText('Unknown UIControlType');
  });
});
