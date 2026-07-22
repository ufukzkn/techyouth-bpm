import type { ProcessTask } from "@/lib/types";

export const TEAM_LEAD_REQUIRED_REASON = "task.teamLeadRequired";

type TaskControlState =
  | { kind: "claim"; canClaim: boolean }
  | { kind: "team-lead-restricted"; canRelease: boolean }
  | { kind: "claimed-by-another" }
  | { kind: "actions"; canAct: boolean; canRelease: boolean };

function requiresClaim(task: ProcessTask) {
  return task.assignmentType === "Team"
    || task.assignmentType === "CommunityRole"
    || task.assignmentType === "TeamAndCommunityRole";
}

function hasTeamLeadDenial(task: ProcessTask) {
  return task.claimDenialReasonCode === TEAM_LEAD_REQUIRED_REASON
    || task.actionDenialReasonCode === TEAM_LEAD_REQUIRED_REASON;
}

export function deriveTaskControlState(task: ProcessTask, activeUserId: string): TaskControlState {
  const taskRequiresClaim = requiresClaim(task);
  const isClaimedByCurrentUser = task.claimedByUserId === activeUserId;

  if (taskRequiresClaim && task.claimedByUserId && !isClaimedByCurrentUser) {
    return { kind: "claimed-by-another" };
  }

  if (taskRequiresClaim && !task.claimedByUserId) {
    if (hasTeamLeadDenial(task)) {
      return { kind: "team-lead-restricted", canRelease: false };
    }

    return { kind: "claim", canClaim: task.canCurrentUserClaim !== false };
  }

  if (hasTeamLeadDenial(task)) {
    return { kind: "team-lead-restricted", canRelease: taskRequiresClaim && isClaimedByCurrentUser };
  }

  return {
    kind: "actions",
    canAct: task.canCurrentUserAct !== false,
    canRelease: taskRequiresClaim && isClaimedByCurrentUser,
  };
}
