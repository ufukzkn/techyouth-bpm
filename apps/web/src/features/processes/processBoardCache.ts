import type { PagedResult, ProcessDetail, ProcessSummary, ProcessTask } from "@/lib/types";

export const processPageCache = new Map<string, PagedResult<ProcessSummary>>();
export const taskPageCache = new Map<string, PagedResult<ProcessTask>>();
export const processDetailCache = new Map<string, ProcessDetail>();

export function createProcessCacheKey(userId: string, params: object) {
  return `${userId}:${JSON.stringify(params)}`;
}

export function invalidateProcessCaches(processId: string) {
  processPageCache.clear();
  taskPageCache.clear();
  processDetailCache.delete(processId);
}

export function clearProcessBoardCaches() {
  processPageCache.clear();
  taskPageCache.clear();
  processDetailCache.clear();
}
