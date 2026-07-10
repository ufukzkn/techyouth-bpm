"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { UsersAndRolesView } from "@/features/app-shell/views/UsersAndRolesView";

export default function ManagementUsersPage() {
  return (
    <WorkspaceShell viewId="managementUsers">
      {({ user, language, token }) => <UsersAndRolesView activeUser={user} language={language} token={token} />}
    </WorkspaceShell>
  );
}
