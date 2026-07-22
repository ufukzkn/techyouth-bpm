import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language, ProcessCurrentStep, ProcessCurrentStepSummary, ProcessStepExecution, ProcessTask } from "@/lib/types";

type AssignmentContext = Pick<
  ProcessCurrentStepSummary,
  "assignmentType" | "teamName" | "communityRoleName" | "assignedUserDisplayName" | "claimedByUserDisplayName" | "requiresTeamLead"
>;

type AssignmentSource = AssignmentContext | ProcessCurrentStep | ProcessStepExecution | ProcessTask;

export function describeProcessAssignment(language: Language, source: AssignmentSource): string {
  const assignmentType = source.assignmentType;
  const teamName = "candidateTeamName" in source
    ? source.candidateTeamName
    : "teamName" in source
      ? source.teamName
      : undefined;
  const communityRoleName = "candidateCommunityRoleName" in source
    ? source.candidateCommunityRoleName
    : "communityRoleName" in source
      ? source.communityRoleName
      : undefined;
  const assignedUserDisplayName = source.assignedUserDisplayName;
  const claimedByUserDisplayName = "claimedByUserDisplayName" in source ? source.claimedByUserDisplayName : undefined;
  const requiresTeamLead = "requiresTeamLead" in source && source.requiresTeamLead;
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);

  if (claimedByUserDisplayName) return t("process.assignmentClaimed", { user: claimedByUserDisplayName });
  if (assignmentType === "SpecificUser" && assignedUserDisplayName) return t("process.assignmentSpecificUser", { user: assignedUserDisplayName });
  if (assignmentType === "ProcessStarter") return t("process.assignmentProcessStarter");
  if (assignmentType === "TeamAndCommunityRole" && teamName && communityRoleName) {
    return requiresTeamLead
      ? t("process.assignmentTeamRoleLead", { team: teamName, role: communityRoleName })
      : t("process.assignmentTeamRole", { team: teamName, role: communityRoleName });
  }
  if (assignmentType === "Team" && teamName) {
    return requiresTeamLead
      ? t("process.assignmentTeamLead", { team: teamName })
      : t("process.assignmentTeamCandidate", { team: teamName });
  }
  if (assignmentType === "CommunityRole" && communityRoleName) return t("process.assignmentCommunityRole", { role: communityRoleName });
  if (assignedUserDisplayName) return t("process.assignmentSpecificUser", { user: assignedUserDisplayName });
  if (communityRoleName) return t("process.assignmentCommunityRole", { role: communityRoleName });
  if (teamName) return requiresTeamLead ? t("process.assignmentTeamLead", { team: teamName }) : t("process.assignmentTeamCandidate", { team: teamName });
  return t("process.assignmentUnspecified");
}
