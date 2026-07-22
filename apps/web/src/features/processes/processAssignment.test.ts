import { describe, expect, it } from "vitest";
import { describeProcessAssignment } from "@/features/processes/processAssignment";

describe("process assignment copy", () => {
  it("describes a team and community role without exposing a technical node key", () => {
    expect(describeProcessAssignment("tr", {
      assignmentType: "TeamAndCommunityRole",
      teamName: "Teknik Değerlendirme",
      communityRoleName: "Onay Sorumlusu",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "",
      requiresTeamLead: false,
    })).toBe("Teknik Değerlendirme ekibindeki Onay Sorumlusu rolü ilgileniyor");
  });

  it("prioritizes the claimant when a task has already been claimed", () => {
    expect(describeProcessAssignment("en", {
      assignmentType: "Team",
      teamName: "Finance",
      communityRoleName: "",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "Ada Lovelace",
      requiresTeamLead: true,
    })).toBe("Ada Lovelace is working on it");
  });
});
