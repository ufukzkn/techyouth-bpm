"use client";

import "@xyflow/react/dist/style.css";
import "./workflow-editor.css";

import {
  AlertCircle,
  CheckCircle2,
  Expand,
  Eye,
  GitBranch,
  Maximize2,
  Minimize2,
  Save,
  Send,
  Trash2,
} from "lucide-react";
import { ReactFlowProvider } from "@xyflow/react";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { createPortal } from "react-dom";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { useSessionStore } from "@/features/session/sessionStore";
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
import { workflowApiValidationIssues } from "@/features/workflows/apiValidation";
import { WorkflowCanvas } from "@/features/workflows/WorkflowCanvas";
import { WorkflowInspector } from "@/features/workflows/WorkflowInspector";
import { workflowText } from "@/features/workflows/workflowI18n";
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
type EditorMode = "normal" | "wide" | "fullscreen";
type ServerValidationState = {
  fingerprint: string;
  issues: ReturnType<typeof workflowApiValidationIssues>;
};

export function WorkflowEditor({
  canPublish = true,
  initialDraft,
  lookups = emptyWorkflowLookups,
  onChange,
  onPublish,
  onSave,
  readOnly = false,
}: WorkflowEditorProps) {
  const language = useSessionStore((state) => state.language);
  const text = (tr: string, en: string) => workflowText(language, tr, en);
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
  const [editorMode, setEditorMode] = useState<EditorMode>("normal");
  const [message, setMessage] = useState(() => text("Taslak düzenlemeye hazır.", "The draft is ready to edit."));
  const [serverValidation, setServerValidation] = useState<ServerValidationState | null>(null);
  const isMobile = useMobileEditorNotice();
  const draftFingerprint = useMemo(() => JSON.stringify(createWorkflowWriteModel(draft)), [draft]);
  const localIssues = useMemo(() => validateWorkflow(draft, lookups), [draft, lookups]);
  const issues = useMemo(
    () => serverValidation?.fingerprint === draftFingerprint
      ? [...localIssues, ...serverValidation.issues]
      : localIssues,
    [draftFingerprint, localIssues, serverValidation],
  );
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

  useEffect(() => {
    if (editorMode === "normal") {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setEditorMode("normal");
      }
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", closeOnEscape);
    };
  }, [editorMode]);

  useEffect(() => {
    if (submissionState === "idle") {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setMessage(text("Taslak düzenlemeye hazır.", "The draft is ready to edit."));
    }
    // The language switch should update idle editor feedback immediately.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [language, submissionState]);

  async function saveDraft() {
    if (effectiveReadOnly || isSubmitting) {
      return;
    }
    try {
      setServerValidation(null);
      setSubmissionState("saving");
      setMessage(text("Taslak kaydediliyor...", "Saving draft..."));
      const result = await onSave(createWorkflowWriteModel(draft));
      markSaved(result?.draft);
      setSubmissionState("success");
      setMessage(result?.message || text("Taslak kaydedildi.", "Draft saved."));
    } catch (error) {
      const apiIssues = workflowApiValidationIssues(error, draft, language);
      if (apiIssues.length > 0) {
        setServerValidation({ fingerprint: draftFingerprint, issues: apiIssues });
      }
      setSubmissionState("error");
      setMessage(apiIssues[0]?.message
        ?? errorMessage(error, text("Taslak kaydedilemedi.", "Draft could not be saved."), language));
    }
  }

  async function publishDraft() {
    if (!canPublish || effectiveReadOnly || isSubmitting) {
      return;
    }
    if (workflowHasErrors(issues)) {
      setSubmissionState("error");
      setMessage(text(
        `Yayınlama engellendi: ${errorCount} hata var. Doğrulama listesinden bir hatayı seçerek ilgili alanı açın.`,
        `Publishing is blocked by ${errorCount} ${errorCount === 1 ? "error" : "errors"}. Select an item in the validation list to open it.`,
      ));
      return;
    }
    try {
      setServerValidation(null);
      setSubmissionState("publishing");
      setMessage(text("Akış yayınlanıyor...", "Publishing workflow..."));
      const result = await onPublish(createWorkflowWriteModel(draft));
      markPublished(result?.draft ? { ...result.draft, status: "Published" } : undefined);
      setSubmissionState("success");
      setMessage(result?.message || text("Akış yayınlandı.", "Workflow published."));
    } catch (error) {
      const apiIssues = workflowApiValidationIssues(error, draft, language);
      if (apiIssues.length > 0) {
        setServerValidation({ fingerprint: draftFingerprint, issues: apiIssues });
      }
      setSubmissionState("error");
      setMessage(apiIssues.length > 0
        ? text(
          `Yayınlama engellendi: ${apiIssues.length} sunucu doğrulama hatası bulundu. Hata listesinden ilgili öğeyi açın.`,
          `Publishing is blocked by ${apiIssues.length} server validation ${apiIssues.length === 1 ? "error" : "errors"}. Open the related item from the validation list.`,
        )
        : errorMessage(error, text("Akış yayınlanamadı.", "Workflow could not be published."), language));
    }
  }

  if (isMobile) {
    return <WorkflowMobileNotice draft={draft} errorCount={errorCount} language={language} />;
  }

  const editor = (
    <section
      className={`workflow-editor-shell workflow-editor-shell-mode-${editorMode}`}
      data-editor-mode={editorMode}
    >
      <header className="workflow-editor-header">
        <div className="workflow-editor-title">
          <span className="workflow-editor-mark" aria-hidden="true"><GitBranch size={20} /></span>
          <span>
            <small>Visual Workflow</small>
            <h2>{draft.name || text("Adsız akış", "Untitled workflow")}</h2>
          </span>
        </div>
        <div className="workflow-editor-statuses">
          <span className={`workflow-status-chip workflow-status-chip-${draft.status.toLowerCase()}`}>
            {draft.status === "Published" ? text("Yayında", "Published") : text("Taslak", "Draft")}
          </span>
          {isDirty ? <span className="workflow-unsaved-indicator">{text("Kaydedilmemiş", "Unsaved")}</span> : null}
        </div>
        <div className="workflow-editor-actions">
          <IconButton
            label={editorMode === "wide"
              ? text("Normal görünüme dön", "Return to normal view")
              : text("Geniş görünüm", "Wide view")}
            onClick={() => setEditorMode((current) => current === "wide" ? "normal" : "wide")}
          >
            {editorMode === "wide" ? <Minimize2 size={16} aria-hidden="true" /> : <Expand size={16} aria-hidden="true" />}
          </IconButton>
          <IconButton
            label={editorMode === "fullscreen"
              ? text("Tam ekrandan çık", "Exit full screen")
              : text("Tam ekran", "Full screen")}
            onClick={() => setEditorMode((current) => current === "fullscreen" ? "normal" : "fullscreen")}
          >
            {editorMode === "fullscreen" ? <Minimize2 size={16} aria-hidden="true" /> : <Maximize2 size={16} aria-hidden="true" />}
          </IconButton>
          <IconButton
            disabled={!hasSelection || effectiveReadOnly || isSubmitting}
            label={text("Seçimi sil", "Delete selection")}
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
            {text("Kaydet", "Save")}
          </Button>
          <Button
            disabled={!canPublish || effectiveReadOnly || isSubmitting}
            isLoading={submissionState === "publishing"}
            leadingIcon={<Send size={16} aria-hidden="true" />}
            onClick={publishDraft}
            size="sm"
          >
            {text("Yayınla", "Publish")}
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
        <strong>{errorCount > 0
          ? text(`${errorCount} doğrulama hatası`, `${errorCount} validation ${errorCount === 1 ? "error" : "errors"}`)
          : text("Yerel doğrulama tamam", "Local validation complete")}</strong>
      </div>

      <div className="workflow-editor-grid">
        <WorkflowPalette language={language} readOnly={effectiveReadOnly} />
        <ReactFlowProvider>
          <WorkflowCanvas fitViewKey={editorMode} issues={issues} language={language} readOnly={effectiveReadOnly} />
        </ReactFlowProvider>
        <WorkflowInspector issues={issues} lookups={lookups} readOnly={effectiveReadOnly} />
      </div>
    </section>
  );

  return editorMode === "normal" ? editor : createPortal(editor, document.body);
}

function WorkflowMobileNotice({
  draft,
  errorCount,
  language,
}: {
  draft: WorkflowDefinitionDraft;
  errorCount: number;
  language: "tr" | "en";
}) {
  const text = (tr: string, en: string) => workflowText(language, tr, en);
  const nodeCount = draft.nodes.filter((node) => node.type !== "teamSwimlane").length;
  return (
    <section className="workflow-mobile-notice">
      <span className="workflow-mobile-notice-icon" aria-hidden="true"><Eye size={22} /></span>
      <div>
        <small>{text("Mobil görünüm", "Mobile view")}</small>
        <h2>{draft.name || text("Adsız akış", "Untitled workflow")}</h2>
        <p>{text(
          "Akış düzenleme tablet ve masaüstünde kullanılabilir. Bu görünüm salt okunurdur.",
          "Workflow editing is available on tablets and desktops. This view is read only.",
        )}</p>
      </div>
      <dl>
        <div><dt>{text("Düğüm", "Nodes")}</dt><dd>{nodeCount}</dd></div>
        <div><dt>{text("Bağlantı", "Connections")}</dt><dd>{draft.edges.length}</dd></div>
        <div><dt>{text("Hata", "Errors")}</dt><dd>{errorCount}</dd></div>
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

function errorMessage(error: unknown, fallback: string, language: "tr" | "en") {
  return localizeApiError(error, language, error instanceof Error && error.message ? error.message : fallback);
}
