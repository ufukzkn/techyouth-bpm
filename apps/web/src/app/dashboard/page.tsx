"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { DashboardView } from "@/features/app-shell/views/DashboardView";

export default function DashboardPage() {
  return (
    <WorkspaceShell viewId="dashboard">
      {({ token, user, language, visibleViewIds, navigate }) => (
        <DashboardView
          token={token}
          user={user}
          language={language}
          visibleViewIds={visibleViewIds}
          onNavigate={navigate}
        />
      )}
    </WorkspaceShell>
  );
}
