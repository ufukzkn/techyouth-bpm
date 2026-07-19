import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

const repoRoot = path.resolve(__dirname, "../..");
const apiBaseUrl = "http://localhost:5291";
const appBaseUrl = "http://localhost:3000";
const databasePath = path.join(repoRoot, "apps/api/.e2e/techyouth-bpm-e2e.db");

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  outputDir: ".e2e/test-results",
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: ".e2e/playwright-report" }],
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
      timeout: 120_000,
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
        ? "npm run start -- --hostname 127.0.0.1 --port 3000"
        : "npm run dev -- --hostname 127.0.0.1 --port 3000",
      cwd: __dirname,
      url: appBaseUrl,
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        NEXT_PUBLIC_API_BASE_URL: apiBaseUrl,
      },
    },
  ],
});
