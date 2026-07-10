"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { ProcessBoardDraft } from "@/features/processes/ProcessBoardDraft";

export default function ProcessesPage() {
  return (
    <WorkspaceShell viewId="processes">
      {() => <ProcessBoardDraft mode="processes" />}
    </WorkspaceShell>
  );
}
