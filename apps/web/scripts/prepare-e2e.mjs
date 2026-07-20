import { mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const apiDataDirectory = path.resolve(webRoot, "../api/.e2e");
const databasePath = path.join(apiDataDirectory, "techyouth-bpm-e2e.db");

await Promise.all([
  rm(databasePath, { force: true }),
  rm(`${databasePath}-shm`, { force: true }),
  rm(`${databasePath}-wal`, { force: true }),
]);
await mkdir(apiDataDirectory, { recursive: true });
