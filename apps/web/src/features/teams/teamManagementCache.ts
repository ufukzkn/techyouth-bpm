import type { Community, TeamCandidatePage, TeamMemberPage, TeamPage } from "@/lib/types";

export const teamCommunitiesCache = new Map<string, Community[]>();
export const teamPageCache = new Map<string, TeamPage>();
export const teamMemberPageCache = new Map<string, TeamMemberPage>();
export const teamCandidatePageCache = new Map<string, TeamCandidatePage>();

export function clearTeamDataCache() {
  teamPageCache.clear();
  teamMemberPageCache.clear();
  teamCandidatePageCache.clear();
}

export function clearTeamDetailCache(teamId: string) {
  for (const key of [...teamMemberPageCache.keys(), ...teamCandidatePageCache.keys()]) {
    if (key.includes(`:${teamId}:`)) {
      teamMemberPageCache.delete(key);
      teamCandidatePageCache.delete(key);
    }
  }
}
