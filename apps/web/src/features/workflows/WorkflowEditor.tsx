"use client";

import "@xyflow/react/dist/style.css";
import "./workflow-editor.css";

import {
  AlertCircle,
  CheckCircle2,
  Eye,
  GitBranch,
  Save,
  Send,
  Trash2,
} from "lucide-react";
import { ReactFlowProvider } from "@xyflow/react";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import type {
  WorkflowDefinitionDraft,
  WorkflowEditorLookups,
  WorkflowPublishHandler,
  WorkflowSaveHandler,
} from "@/features/workflows/contracts";
import { emptyWorkflowLookups } from "@/features/workflows/contracts";
import { Button } from "@/features/ui/Button";
import { IconButton } from "@/features/ui/IconButton";
import { createWorkflowWriteModel } from "@/features/workflows/apiGraphAdapter";
import { WorkflowCanvas } from "@/features/workflows/WorkflowCanvas";
import { WorkflowInspector } from "@/features/workflows/WorkflowInspector";
import { WorkflowPalette } from "@/features/workflows/WorkflowPalette";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";
import { validateWorkflow, workflowHasErrors } from "@/features/workflows/validation";

export type WorkflowEditorProps = {
  canPublish?: boolean;
  initialDraft?: WorkflowDefinitionDraft;
  lookups?: WorkflowEditorLookups;
  onChange?: (draft: WorkflowDefinitionDraft) => void;
  onPublish: WorkflowPublishHandler;
  onSave: WorkflowSaveHandler;
  readOnly?: boolean;
};

type SubmissionState = "idle" | "saving" | "publishing" | "success" | "error";

export function WorkflowEditor({
  canPublish = true,
  initialDraft,
  lookups = emptyWorkflowLookups,
  onChange,
  onPublish,
  onSave,
  readOnly = false,
}: WorkflowEditorProps) {
  const draft = useWorkflowDraftStore((state) => state.draft);
  const isDirty = useWorkflowDraftStore((state) => state.isDirty);
  const selectedNodeId = useWorkflowDraftStore((state) => state.selectedNodeId);
  const selectedEdgeId = useWorkflowDraftStore((state) => state.selectedEdgeId);
  const hydrate = useWorkflowDraftStore((state) => state.hydrate);
  const reset = useWorkflowDraftStore((state) => state.reset);
  const deleteSelection = useWorkflowDraftStore((state) => state.deleteSelection);
  const markSaved = useWorkflowDraftStore((state) => state.markSaved);
  const markPublished = useWorkflowDraftStore((state) => state.markPublished);
  const [submissionState, setSubmissionState] = useState<SubmissionState>("idle");
  const [message, setMessage] = useState("Taslak düzenlemeye hazır.");
  const isMobile = useMobileEditorNotice();
  const issues = useMemo(() => validateWorkflow(draft), [draft]);
  const errorCount = issues.filter((issue) => issue.severity === "error").length;
  const effectiveReadOnly = readOnly || draft.status === "Published";
  const isSubmitting = submissionState === "saving" || submissionState === "publishing";
  const hasSelection = Boolean(selectedNodeId || selectedEdgeId);

  useEffect(() => {
    if (initialDraft) {
      hydrate(initialDraft);
    } else {
      reset();
    }
  }, [hydrate, initialDraft, reset]);

  useEffect(() => {
    if (isDirty) {
      onChange?.(draft);
    }
  }, [draft, isDirty, onChange]);

  async function saveDraft() {
    if (effectiveReadOnly || isSubmitting) {
      return;
    }
    try {
      setSubmissionState("saving");
      setMessage("Taslak kaydediliyor...");
      const result = await onSave(createWorkflowWriteModel(draft));
      markSaved(result?.draft);
      setSubmissionState("success");
      setMessage(result?.message || "Taslak kaydedildi.");
    } catch (error) {
      setSubmissionState("error");
      setMessage(errorMessage(error, "Taslak kaydedilemedi."));
    }
  }

  async function publishDraft() {
    if (!canPublish || effectiveReadOnly || isSubmitting) {
      return;
    }
    if (workflowHasErrors(issues)) {
      setSubmissionState("error");
      setMessage("Yayınlamadan önce doğrulama hatalarını giderin.");
      return;
    }
    try {
      setSubmissionState("publishing");
      setMessage("Akış yayınlanıyor...");
      const result = await onPublish(createWorkflowWriteModel(draft));
      markPublished(result?.draft ? { ...result.draft, status: "Published" } : undefined);
      setSubmissionState("success");
      setMessage(result?.message || "Akış yayınlandı.");
    } catch (error) {
      setSubmissionState("error");
      setMessage(errorMessage(error, "Akış yayınlanamadı."));
    }
  }

  if (isMobile) {
    return <WorkflowMobileNotice draft={draft} errorCount={errorCount} />;
  }

  return (
    <section className="workflow-editor-shell">
      <header className="workflow-editor-header">
        <div className="workflow-editor-title">
          <span className="workflow-editor-mark" aria-hidden="true"><GitBranch size={20} /></span>
          <span>
            <small>Visual Workflow</small>
            <h2>{draft.name || "Adsız akış"}</h2>
          </span>
        </div>
        <div className="workflow-editor-statuses">
          <span className={`workflow-status-chip workflow-status-chip-${draft.status.toLowerCase()}`}>
            {draft.status === "Published" ? "Yayında" : "Taslak"}
          </span>
          {isDirty ? <span className="workflow-unsaved-indicator">Kaydedilmemiş</span> : null}
        </div>
        <div className="workflow-editor-actions">
          <IconButton
            disabled={!hasSelection || effectiveReadOnly || isSubmitting}
            label="Seçimi sil"
            onClick={deleteSelection}
            tone="danger"
          >
            <Trash2 size={16} aria-hidden="true" />
          </IconButton>
          <Button
            disabled={effectiveReadOnly || isSubmitting}
            isLoading={submissionState === "saving"}
            leadingIcon={<Save size={16} aria-hidden="true" />}
            onClick={saveDraft}
            size="sm"
            variant="secondary"
          >
            Kaydet
          </Button>
          <Button
            disabled={!canPublish || effectiveReadOnly || isSubmitting || errorCount > 0}
            isLoading={submissionState === "publishing"}
            leadingIcon={<Send size={16} aria-hidden="true" />}
            onClick={publishDraft}
            size="sm"
          >
            Yayınla
          </Button>
        </div>
      </header>

      <div className="workflow-editor-feedback" aria-live="polite">
        <span className={`workflow-feedback-icon workflow-feedback-icon-${submissionState}`} aria-hidden="true">
          {submissionState === "error" || errorCount > 0
            ? <AlertCircle size={15} />
            : <CheckCircle2 size={15} />}
        </span>
        <span>{message}</span>
        <strong>{errorCount > 0 ? `${errorCount} doğrulama hatası` : "Yerel doğrulama tamam"}</strong>
      </div>

      <div className="workflow-editor-grid">
        <WorkflowPalette readOnly={effectiveReadOnly} />
        <ReactFlowProvider>
          <WorkflowCanvas issues={issues} readOnly={effectiveReadOnly} />
        </ReactFlowProvider>
        <WorkflowInspector issues={issues} lookups={lookups} readOnly={effectiveReadOnly} />
      </div>
    </section>
  );
}

function WorkflowMobileNotice({ draft, errorCount }: { draft: WorkflowDefinitionDraft; errorCount: number }) {
  const nodeCount = draft.nodes.filter((node) => node.type !== "teamSwimlane").length;
  return (
    <section className="workflow-mobile-notice">
      <span className="workflow-mobile-notice-icon" aria-hidden="true"><Eye size={22} /></span>
      <div>
        <small>Mobil görünüm</small>
        <h2>{draft.name || "Adsız akış"}</h2>
        <p>Akış düzenleme tablet ve masaüstünde kullanılabilir. Bu görünüm salt okunurdur.</p>
      </div>
      <dl>
        <div><dt>Düğüm</dt><dd>{nodeCount}</dd></div>
        <div><dt>Bağlantı</dt><dd>{draft.edges.length}</dd></div>
        <div><dt>Hata</dt><dd>{errorCount}</dd></div>
      </dl>
    </section>
  );
}

function useMobileEditorNotice() {
  return useSyncExternalStore(subscribeToMobileQuery, getMobileSnapshot, () => false);
}

function subscribeToMobileQuery(callback: () => void) {
  const media = window.matchMedia("(max-width: 767px)");
  media.addEventListener("change", callback);
  return () => media.removeEventListener("change", callback);
}

function getMobileSnapshot() {
  return window.matchMedia("(max-width: 767px)").matches;
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof Error && error.message ? error.message : fallback;
}
