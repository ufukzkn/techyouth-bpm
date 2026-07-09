"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { FormDesignerDraft } from "@/features/form-designer/FormDesignerDraft";

export default function FormsPage() {
  return <WorkspaceShell viewId="forms">{() => <FormDesignerDraft />}</WorkspaceShell>;
}
