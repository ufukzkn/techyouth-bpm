import type { ProcessSummary } from "@/lib/types";

/**
 * Merges a fresh list of processes from the API with a saved order of process IDs.
 * 
 * Rules:
 * 1. Processes whose IDs are in `savedIds` appear first, in the exact order of `savedIds`.
 * 2. Processes whose IDs are NOT in `savedIds` (e.g. newly created processes) are appended at the end.
 * 3. Stale IDs in `savedIds` (processes that no longer exist in `fresh`) are ignored.
 * 
 * @example
 * // Preserves saved order:
 * applyStoredOrder([{id: '1'}, {id: '2'}, {id: '3'}], ['3', '1', '2']) 
 * // => [{id: '3'}, {id: '1'}, {id: '2'}]
 * 
 * @example
 * // Appends unseen processes:
 * applyStoredOrder([{id: '1'}, {id: '2'}], ['2'])
 * // => [{id: '2'}, {id: '1'}]
 * 
 * @example
 * // Drops stale IDs safely:
 * applyStoredOrder([{id: '2'}], ['1', '2', '3'])
 * // => [{id: '2'}]
 */
export function applyStoredOrder(fresh: ProcessSummary[], savedIds: string[]): ProcessSummary[] {
  const byId = new Map(fresh.map((p) => [p.id, p]));
  const ordered = savedIds.flatMap((id) => (byId.has(id) ? [byId.get(id)!] : []));
  const unseen = fresh.filter((p) => !savedIds.includes(p.id));
  return [...ordered, ...unseen];
}
