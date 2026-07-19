import { mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const apiDataDirectory = path.resolve(webRoot, "../api/.e2e");
const browserOutputDirectory = path.resolve(webRoot, ".e2e");

await Promise.all([
  rm(apiDataDirectory, { recursive: true, force: true }),
  rm(browserOutputDirectory, { recursive: true, force: true }),
]);
await mkdir(apiDataDirectory, { recursive: true });
