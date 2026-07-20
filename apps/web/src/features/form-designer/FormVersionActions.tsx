import { Archive, Save, Send } from "lucide-react";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import { getVersionStatusLabel, type DesignerVersionState } from "@/features/form-designer/formDesignerModel";
import type { Language } from "@/lib/types";

export type FormSaveState =
  | "idle"
  | "saving"
  | "publishing"
  | "archiving"
  | "success"
  | "error";

type SharedProps = {
  canArchive: boolean;
  canPublish: boolean;
  canSaveDraft: boolean;
  isPersisting: boolean;
  language: Language;
  onArchive: () => void;
  onPublish: () => void;
  onSaveDraft: () => void;
  saveState: FormSaveState;
  savingLabel: string;
  versionState: DesignerVersionState;
};

export function FormPrimaryVersionActions({
  canArchive,
  canPublish,
  isPersisting,
  language,
  onArchive,
  onPublish,
  onSaveDraft,
  saveState,
  savingLabel,
  versionState,
}: SharedProps) {
  const copy = getFormPagingCopy(language);
  return (
    <div className="designer-primary-version-actions" aria-label={copy.version}>
      <button className="primary-button" disabled={isPersisting} type="button" onClick={onSaveDraft}>
        <Save size={17} />
        {saveState === "saving" ? savingLabel : copy.saveDraft}
      </button>
      <button
        className="success-button"
        disabled={isPersisting || !canPublish || versionState.status !== "draft"}
        type="button"
        onClick={onPublish}
      >
        <Send size={17} />
        {saveState === "publishing" ? copy.publishing : copy.publish}
      </button>
      <button
        className="secondary-button"
        disabled={isPersisting || !canArchive || versionState.status !== "published"}
        type="button"
        onClick={onArchive}
      >
        <Archive size={17} />
        {saveState === "archiving" ? copy.archiving : copy.archive}
      </button>
    </div>
  );
}

export function FormVersionActionRail({
  canArchive,
  canPublish,
  canSaveDraft,
  isPersisting,
  language,
  onArchive,
  onPublish,
  onSaveDraft,
  saveState,
  savingLabel,
  versionState,
}: SharedProps) {
  const copy = getFormPagingCopy(language);
  const saveLabel = saveState === "saving" ? savingLabel : copy.saveDraft;
  const publishLabel = saveState === "publishing" ? copy.publishing : copy.publish;
  const archiveLabel = saveState === "archiving" ? copy.archiving : copy.archive;
  const publishTitle = !canPublish
    ? copy.publishUnavailable
    : versionState.status === "published"
      ? copy.alreadyPublished
      : versionState.status === "archived"
        ? copy.archivedVersion
        : publishLabel;

  return (
    <>
      <div className="designer-version-summary">
        <span>
          {copy.version} {versionState.version}
        </span>
        <strong className={`form-version-status form-version-status-${versionState.status}`}>
          {getVersionStatusLabel(copy, versionState.status)}
        </strong>
      </div>
      <div className="designer-version-actions">
        <button
          aria-label={saveLabel}
          className="icon-button designer-version-icon-action"
          disabled={isPersisting}
          title={saveLabel}
          type="button"
          onClick={onSaveDraft}
        >
          <Save size={18} />
        </button>
        <button
          aria-label={publishLabel}
          className="icon-button designer-version-icon-action designer-version-icon-action-success"
          disabled={isPersisting || !canPublish || versionState.status !== "draft"}
          title={publishTitle}
          type="button"
          onClick={onPublish}
        >
          <Send size={18} />
        </button>
        <button
          aria-label={archiveLabel}
          className="icon-button designer-version-icon-action"
          disabled={isPersisting || !canArchive || versionState.status !== "published"}
          title={archiveLabel}
          type="button"
          onClick={onArchive}
        >
          <Archive size={18} />
        </button>
      </div>
      {!canSaveDraft ? <p className="helper-copy">{copy.layoutPersistenceUnavailable}</p> : null}
      {!canPublish ? <p className="helper-copy">{copy.publishUnavailable}</p> : null}
    </>
  );
}
