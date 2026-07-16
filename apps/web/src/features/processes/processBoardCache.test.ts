import { beforeEach, describe, expect, it } from "vitest";
import {
  clearProcessBoardCaches,
  createProcessCacheKey,
  processDetailCache,
  processPageCache,
  taskPageCache,
} from "@/features/processes/processBoardCache";

describe("process board cache", () => {
  beforeEach(() => clearProcessBoardCaches());

  it("isolates server page results by user and visibility scope", () => {
    const personal = createProcessCacheKey("user-a", { page: 1, scope: "personal" });
    const community = createProcessCacheKey("user-a", { page: 1, scope: "community" });
    const anotherUser = createProcessCacheKey("user-b", { page: 1, scope: "personal" });

    expect(new Set([personal, community, anotherUser]).size).toBe(3);
  });

  it("clears process, task and detail entries when a session ends", () => {
    processPageCache.set("processes", { items: [], page: 1, pageSize: 10, totalCount: 0 });
    taskPageCache.set("tasks", { items: [], page: 1, pageSize: 10, totalCount: 0 });
    processDetailCache.set("detail", {} as never);

    clearProcessBoardCaches();

    expect(processPageCache.size).toBe(0);
    expect(taskPageCache.size).toBe(0);
    expect(processDetailCache.size).toBe(0);
  });
});
