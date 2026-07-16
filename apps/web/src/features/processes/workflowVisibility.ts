import type { User, WorkflowVisibilityScope } from "@/lib/types";

export function getAvailableWorkflowScopes(user: User): WorkflowVisibilityScope[] {
  if (user.role === "SuperAdmin") {
    return ["personal", "global"];
  }

  return user.communityId && user.permissions.includes("Processes.ViewAll")
    ? ["personal", "community"]
    : ["personal"];
}

export function resolveWorkflowScope(
  requestedScope: string | null,
  availableScopes: readonly WorkflowVisibilityScope[],
): WorkflowVisibilityScope {
  return availableScopes.includes(requestedScope as WorkflowVisibilityScope)
    ? requestedScope as WorkflowVisibilityScope
    : "personal";
}
