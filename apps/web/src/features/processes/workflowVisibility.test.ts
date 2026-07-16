import { describe, expect, it } from "vitest";
import { getAvailableWorkflowScopes, resolveWorkflowScope } from "@/features/processes/workflowVisibility";
import type { User } from "@/lib/types";

const baseUser: User = {
  id: "user",
  username: "user",
  displayName: "User",
  email: "user@example.test",
  role: "User",
  status: "Active",
  isEmailVerified: true,
  mustChangePassword: false,
  communityId: "community",
  communityName: "Community",
  communityRoleId: "role",
  communityRoleName: "Role",
  permissions: ["Processes.View"],
  isCommunityActive: true,
};

describe("workflow visibility options", () => {
  it("keeps normal users in the personal scope", () => {
    expect(getAvailableWorkflowScopes(baseUser)).toEqual(["personal"]);
  });

  it("offers community scope only with ViewAll permission", () => {
    expect(getAvailableWorkflowScopes({
      ...baseUser,
      permissions: ["Processes.View", "Processes.ViewAll"],
    })).toEqual(["personal", "community"]);
  });

  it("offers platform scope to SuperAdmin and rejects stale URL scopes", () => {
    const scopes = getAvailableWorkflowScopes({ ...baseUser, role: "SuperAdmin", communityId: null });

    expect(scopes).toEqual(["personal", "global"]);
    expect(resolveWorkflowScope("community", scopes)).toBe("personal");
    expect(resolveWorkflowScope("global", scopes)).toBe("global");
  });
});
