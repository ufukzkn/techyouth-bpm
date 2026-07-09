"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { SystemLogsView } from "@/features/app-shell/views/SystemLogsView";

export default function LogsPage() {
  return (
    <WorkspaceShell viewId="logs">
      {({ language, token }) => <SystemLogsView language={language} token={token} />}
    </WorkspaceShell>
  );
}
