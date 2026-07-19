import { expect, test } from "@playwright/test";
import { loginThroughUi } from "./helpers";

test("cookie session survives reload and logout returns to login", async ({ page }) => {
  await page.goto("/management/users");
  await expect(page).toHaveURL(/\/login$/);

  await loginThroughUi(page, "admin", "admin123");
  await expect(page.getByRole("heading", { name: "Hoş geldin, Admin User" })).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("navigation", { name: "Ana gezinme" })).toBeVisible();

  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test("normal user cannot open management by direct URL", async ({ page }) => {
  await loginThroughUi(page, "user", "user123");
  await page.goto("/management/users");

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("navigation", { name: "Ana gezinme" }).getByText("Yönetim")).toHaveCount(0);
});
