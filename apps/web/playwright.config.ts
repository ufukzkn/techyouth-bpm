import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../..");
const apiBaseUrl = process.env.E2E_API_BASE_URL ?? "http://localhost:5292";
const appBaseUrl = process.env.E2E_APP_BASE_URL ?? "http://localhost:3002";
const appPort = new URL(appBaseUrl).port || "3002";
const databasePath = path.join(repoRoot, "apps/api/.e2e/techyouth-bpm-e2e.db");
const runId = process.env.GITHUB_RUN_ID ?? `${Date.now()}-${process.pid}`;
const runOutputDirectory = path.join(__dirname, ".e2e", "runs", runId);

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  outputDir: path.join(runOutputDirectory, "test-results"),
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: path.join(runOutputDirectory, "playwright-report") }],
  ],
  use: {
    baseURL: appBaseUrl,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: "dotnet run --project apps/api/src/TechYouthBpm.Api/TechYouthBpm.Api.csproj --no-launch-profile",
      cwd: repoRoot,
      url: `${apiBaseUrl}/swagger/v1/swagger.json`,
      timeout: 240_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiBaseUrl,
        Database__Provider: "Sqlite",
        ConnectionStrings__DefaultConnection: `Data Source=${databasePath}`,
        Frontend__BaseUrl: appBaseUrl,
        Seed__MockData: "true",
        Email__Provider: "Demo",
        Auth__RateLimitPermitLimit: "100",
      },
    },
    {
      command: process.env.CI
        ? `npm run start -- --hostname 127.0.0.1 --port ${appPort}`
        : `npm run dev -- --hostname 127.0.0.1 --port ${appPort}`,
      cwd: __dirname,
      url: appBaseUrl,
      timeout: 240_000,
      reuseExistingServer: false,
      env: {
        NEXT_PUBLIC_API_BASE_URL: apiBaseUrl,
      },
    },
  ],
});
