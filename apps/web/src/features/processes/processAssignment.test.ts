import { describe, expect, it } from "vitest";
import { describeProcessAssignment, describeProcessClaim } from "@/features/processes/processAssignment";

describe("process assignment copy", () => {
  it("describes a team and community role without exposing a technical node key", () => {
    expect(describeProcessAssignment("tr", {
      assignmentType: "TeamAndCommunityRole",
      teamName: "Teknik Değerlendirme",
      communityRoleName: "Onay Sorumlusu",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "",
      requiresTeamLead: false,
    })).toBe("Teknik Değerlendirme · Onay Sorumlusu");
  });

  it("keeps the target and exposes the claimant separately", () => {
    const source = {
      assignmentType: "Team",
      teamName: "Finance",
      communityRoleName: "",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "Ada Lovelace",
      requiresTeamLead: true,
    } as const;

    expect(describeProcessAssignment("en", source)).toBe("Finance · team lead only");
    expect(describeProcessClaim(source)).toBe("Ada Lovelace");
  });

  it("describes an unrestricted team in both languages", () => {
    const source = {
      assignmentType: "Team",
      teamName: "Scout Ekibi",
      communityRoleName: "",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "",
      requiresTeamLead: false,
    } as const;

    expect(describeProcessAssignment("tr", source)).toBe("Scout Ekibi · takımdaki herhangi bir uygun kişi");
    expect(describeProcessAssignment("en", { ...source, teamName: "Scout Team" })).toBe("Scout Team · any eligible team member");
  });

  it("adds the lead restriction only when the assignment requires it", () => {
    expect(describeProcessAssignment("tr", {
      assignmentType: "TeamAndCommunityRole",
      teamName: "Mali İşler",
      communityRoleName: "Onay Sorumlusu",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "",
      requiresTeamLead: true,
    })).toBe("Mali İşler · Onay Sorumlusu · yalnız takım sorumlusu");

    expect(describeProcessAssignment("en", {
      assignmentType: "TeamAndCommunityRole",
      teamName: "Finance",
      communityRoleName: "Approver",
      assignedUserDisplayName: "",
      claimedByUserDisplayName: "",
      requiresTeamLead: false,
    })).toBe("Finance · Approver");
  });
});
