import { describe, expect, it } from "vitest";
import { deriveTaskControlState, TEAM_LEAD_REQUIRED_REASON } from "@/features/processes/taskControlState";
import type { ProcessTask } from "@/lib/types";

const activeUserId = "current-user";

function candidateTask(overrides: Partial<ProcessTask> = {}): ProcessTask {
  return {
    id: "task",
    processInstanceId: "process",
    assignedCommunityRoleName: "Onay Sorumlusu",
    requiredPermission: "Tasks.Act",
    status: "Open",
    availableActions: ["Approve", "Reject"],
    createdAt: "2026-07-22T09:00:00Z",
    assignmentType: "TeamAndCommunityRole",
    canCurrentUserAct: false,
    canCurrentUserClaim: true,
    ...overrides,
  };
}

describe("task control state", () => {
  it("shows claim for a claimable candidate even though action is unavailable before claiming", () => {
    expect(deriveTaskControlState(candidateTask(), activeUserId)).toEqual({ kind: "claim", canClaim: true });
  });

  it("shows the team lead restriction only for the explicit denial code", () => {
    expect(deriveTaskControlState(candidateTask({
      canCurrentUserClaim: false,
      claimDenialReasonCode: TEAM_LEAD_REQUIRED_REASON,
      actionDenialReasonCode: TEAM_LEAD_REQUIRED_REASON,
    }), activeUserId)).toEqual({ kind: "team-lead-restricted", canRelease: false });
  });

  it("keeps a task claimed by another user read-only", () => {
    expect(deriveTaskControlState(candidateTask({
      claimedByUserId: "another-user",
      canCurrentUserClaim: false,
      actionDenialReasonCode: "task.claimedByAnotherUser",
    }), activeUserId)).toEqual({ kind: "claimed-by-another" });
  });

  it("shows actions and release after the current user has claimed the task", () => {
    expect(deriveTaskControlState(candidateTask({
      claimedByUserId: activeUserId,
      canCurrentUserAct: true,
      canCurrentUserClaim: false,
    }), activeUserId)).toEqual({ kind: "actions", canAct: true, canRelease: true });
  });
});
