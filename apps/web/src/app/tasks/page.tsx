"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { ProcessBoardDraft } from "@/features/processes/ProcessBoardDraft";

export default function TasksPage() {
  return (
    <WorkspaceShell viewId="tasks">
      {() => <ProcessBoardDraft mode="tasks" />}
    </WorkspaceShell>
  );
}
