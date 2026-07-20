import { expect, type APIRequestContext, type Page } from "@playwright/test";

export const apiBaseUrl = process.env.E2E_API_BASE_URL ?? "http://localhost:5292";

export async function loginThroughUi(page: Page, username: string, password: string) {
  await page.goto("/login");
  await page.locator('input[name="username"]').fill(username);
  await page.locator('input[name="password"]').fill(password);
  await page.locator("form.login-form").getByRole("button", { name: "Giriş yap" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

export async function apiLogin(request: APIRequestContext, username: string, password: string) {
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: { username, password, rememberMe: false },
  });
  const raw = await response.text();
  expect(response.ok(), raw).toBeTruthy();
  const body = JSON.parse(raw) as { token: string };
  return body.token;
}

export function bearerHeaders(token: string) {
  return { Authorization: `Bearer ${token}` };
}

export async function expectJson<T>(response: Awaited<ReturnType<APIRequestContext["get"]>>, expectedStatus: number) {
  const raw = await response.text();
  expect(response.status(), raw).toBe(expectedStatus);
  return JSON.parse(raw) as T;
}
