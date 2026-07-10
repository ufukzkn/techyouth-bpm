"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { ManagementCommunitiesView } from "@/features/app-shell/views/ManagementCommunitiesView";

export default function ManagementCommunitiesPage() {
  return <WorkspaceShell viewId="managementCommunities">{({ user, language, token }) => <ManagementCommunitiesView activeUser={user} language={language} token={token} />}</WorkspaceShell>;
}
