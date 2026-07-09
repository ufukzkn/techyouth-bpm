"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { FormRunnerDraft } from "@/features/form-runner/FormRunnerDraft";

export default function RunnerPage() {
  return <WorkspaceShell viewId="runner">{() => <FormRunnerDraft />}</WorkspaceShell>;
}
