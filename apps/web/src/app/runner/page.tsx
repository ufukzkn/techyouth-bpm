"use client";

import { FormRunnerDraft } from "@/features/form-runner/FormRunnerDraft";

export default function RunnerPage() {
  return <WorkspaceShell viewId="runner">{() => <FormRunnerDraft />}</WorkspaceShell>;
}
import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
