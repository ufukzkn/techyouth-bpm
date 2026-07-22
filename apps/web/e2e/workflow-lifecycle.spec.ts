import { expect, test } from "@playwright/test";
import { apiBaseUrl, apiLogin, bearerHeaders, expectJson, loginThroughUi } from "./helpers";

type Community = { id: string; name: string; isActive: boolean };
type FormResponse = { id: string; latestPublishedVersionId: string };
type DefinitionResponse = { id: string };
type VersionResponse = { id: string; status: string };
type TaskResponse = { id: string; nodeKey: string; status: string };
type ProcessResponse = { id: string; status: string; tasks: TaskResponse[]; auditLogs: Array<{ action: string }> };
type RunnableDefinition = { name: string; processDefinitionVersionId: string };

test("published form and workflow run through the real servers", async ({ page, request }) => {
  const adminToken = await apiLogin(request, "admin", "admin123");
  const headers = bearerHeaders(adminToken);
  const communities = await expectJson<Community[]>(
    await request.get(`${apiBaseUrl}/api/communities`, { headers }),
    200,
  );
  const community = communities.find((item) => item.isActive);
  expect(community).toBeTruthy();

  const suffix = Date.now().toString(36);
  const form = await expectJson<FormResponse>(
    await request.post(`${apiBaseUrl}/api/forms`, {
      headers,
      data: {
        name: `E2E Form ${suffix}`,
        description: "Playwright lifecycle form",
        communityId: community!.id,
        fields: [{
          key: "amount",
          label: "Amount",
          type: "Number",
          required: true,
          sortOrder: 1,
          options: [],
          validationRules: [],
        }],
      },
    }),
    201,
  );

  const workflowName = `E2E Workflow ${suffix}`;
  const definition = await expectJson<DefinitionResponse>(
    await request.post(`${apiBaseUrl}/api/process-definitions`, {
      headers,
      data: { name: workflowName, description: "Playwright lifecycle workflow", communityId: community!.id },
    }),
    201,
  );
  const graph = {
    schemaVersion: "1.0",
    nodes: [
      { key: "start", type: "Start", title: "Start", formDefinitionVersionId: form.latestPublishedVersionId },
      { key: "operation", type: "UserTask", title: "Operation", actions: ["Complete"], assignment: { type: "ProcessStarter" } },
      { key: "completed", type: "CompletedEnd", title: "Completed" },
    ],
    edges: [
      { source: "start", target: "operation" },
      { source: "operation", target: "completed", action: "Complete" },
    ],
  };
  const version = await expectJson<VersionResponse>(
    await request.post(`${apiBaseUrl}/api/process-definitions/${definition.id}/versions`, {
      headers,
      data: { formDefinitionVersionId: form.latestPublishedVersionId, graph },
    }),
    201,
  );
  await expectJson<VersionResponse>(
    await request.post(`${apiBaseUrl}/api/process-definitions/${definition.id}/versions/${version.id}/publish`, { headers }),
    200,
  );

  const started = await expectJson<ProcessResponse>(
    await request.post(`${apiBaseUrl}/api/processes/start/version`, {
      headers,
      data: { processDefinitionVersionId: version.id, formData: { amount: 125000 } },
    }),
    200,
  );
  expect(started.status).toBe("InProgress");
  const completed = await expectJson<ProcessResponse>(
    await request.post(`${apiBaseUrl}/api/tasks/${started.tasks[0].id}/actions`, {
      headers,
      data: { action: "Complete", note: "Playwright operation completed." },
    }),
    200,
  );
  expect(completed.status).toBe("Completed");

  await loginThroughUi(page, "admin", "admin123");
  await page.goto(`/processes?scope=global&processId=${started.id}`);
  await expect(page.getByText(workflowName).first()).toBeVisible();
});

test("team and role candidates enforce claim before workflow action", async ({ request }) => {
  const starterToken = await apiLogin(request, "user", "user123");
  const wrongRoleToken = starterToken;
  const scoutToken = await apiLogin(request, "quaresma", "trivela123");
  const runnable = await expectJson<RunnableDefinition[]>(
    await request.get(`${apiBaseUrl}/api/process-definitions/runnable`, { headers: bearerHeaders(starterToken) }),
    200,
  );
  const transfer = runnable.find((item) => item.name === "Transfer Teklif ve Onay Akışı");
  expect(transfer).toBeTruthy();

  const started = await expectJson<ProcessResponse>(
    await request.post(`${apiBaseUrl}/api/processes/start/version`, {
      headers: bearerHeaders(starterToken),
      data: {
        processDefinitionVersionId: transfer!.processDefinitionVersionId,
        formData: {
          talepSahibi: "Playwright Kullanıcısı",
          iletisimEmail: "playwright@techyouth.local",
          oyuncuAdi: "Demo Oyuncu",
          kulup: "Beşiktaş",
          pozisyon: "Forvet",
          bonservis: 7500000,
          paraBirimi: "EUR",
          teklifTarihi: "2026-07-18",
          acilMi: true,
          gerekce: "E2E takım ve rol doğrulaması.",
          teklifDosyasi: { name: "teklif.pdf", size: 245760, type: "application/pdf", lastModified: 1752787200000 },
          veriOnayi: true,
        },
      },
    }),
    200,
  );
  const scoutTask = started.tasks.find((task) => task.nodeKey === "scoutReview");
  expect(scoutTask).toBeTruthy();

  const forbiddenClaim = await request.post(`${apiBaseUrl}/api/tasks/${scoutTask!.id}/claim`, {
    headers: bearerHeaders(wrongRoleToken),
    data: {},
  });
  expect(forbiddenClaim.status()).toBe(400);

  await expectJson<TaskResponse>(
    await request.post(`${apiBaseUrl}/api/tasks/${scoutTask!.id}/claim`, { headers: bearerHeaders(scoutToken), data: {} }),
    200,
  );
  const advanced = await expectJson<ProcessResponse>(
    await request.post(`${apiBaseUrl}/api/tasks/${scoutTask!.id}/actions`, {
      headers: bearerHeaders(scoutToken),
      data: {
        action: "Approve",
        note: "Scout incelemesi tamamlandı.",
        formData: { raporOzeti: "Oyuncu izlendi.", scoutTavsiyesi: "Olumlu", izlemePuani: 9 },
      },
    }),
    200,
  );
  expect(advanced.tasks.some((task) => task.nodeKey === "technicalReview" && task.status === "Open")).toBeTruthy();
  expect(advanced.auditLogs.some((entry) => entry.action === "Approve")).toBeTruthy();
});

test("task view slider stays mounted while rapid filters resolve", async ({ page }) => {
  await loginThroughUi(page, "sport.admin", "sport123");
  await page.goto("/tasks");

  const viewSelector = page.getByRole("radiogroup", { name: "İş görünümü" });
  await expect(viewSelector).toBeVisible();

  await viewSelector.getByText("Geçmiş", { exact: true }).click();
  await viewSelector.getByText("Aktif", { exact: true }).click();
  await viewSelector.getByText("Geçmiş", { exact: true }).click();

  await expect(viewSelector).toBeVisible();
  await expect(page).toHaveURL(/\/tasks\?view=history/);
  await expect(page.getByLabel("Geçmiş", { exact: true })).toBeChecked();
  await expect(page.locator(".process-list-load-error")).toHaveCount(0);
});

test("form designer shell and deferred drag canvas load independently", async ({ page }) => {
  await page.setViewportSize({ width: 1600, height: 1000 });
  await loginThroughUi(page, "admin", "admin123");
  await page.goto("/forms");

  await expect(page.getByRole("heading", { name: "Dinamik form modeli" })).toBeVisible();
  await expect(page.locator(".designer-form-info-panel")).toBeVisible();
  await expect(page.locator(".designer-pages-panel")).toBeVisible();
  await expect(page.locator(".field-palette")).toBeVisible();
});
