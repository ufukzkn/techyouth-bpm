"use client";

import { CheckCircle2, X, XCircle } from "lucide-react";
import { useState } from "react";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language, WorkflowAction } from "@/lib/types";

type TaskActionDialogProps = {
  action: Exclude<WorkflowAction, "Start">;
  language: Language;
  onConfirm: (note: string) => void;
  onCancel: () => void;
  disabled: boolean;
};

export function TaskActionDialog({ action, language, onConfirm, onCancel, disabled }: TaskActionDialogProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [note, setNote] = useState("");
  const isApprove = action === "Approve";

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog" onClick={(e) => e.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{isApprove ? t("dialog.approveAction") : t("dialog.rejectAction")}</span>
            <strong>{isApprove ? t("dialog.approveTitle") : t("dialog.rejectTitle")}</strong>
          </div>
          <button className="icon-button" onClick={onCancel} type="button" aria-label={t("common.close")}>
            <X size={18} />
          </button>
        </div>

        <label className="action-dialog-label">
          {t("dialog.actionNote")}
          <textarea
            className="action-dialog-textarea"
            placeholder={isApprove ? t("dialog.approvePlaceholder") : t("dialog.rejectPlaceholder")}
            rows={3}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>

        <div className="action-dialog-actions">
          <button className="secondary-button" onClick={onCancel} type="button" disabled={disabled}>
            {t("common.cancel")}
          </button>
          <button
            className={isApprove ? "success-button" : "danger-button"}
            disabled={disabled}
            onClick={() => onConfirm(note || (isApprove ? t("dialog.defaultApproveNote") : t("dialog.defaultRejectNote")))}
            type="button"
          >
            {isApprove ? <CheckCircle2 size={17} /> : <XCircle size={17} />}
            {disabled ? t("common.saving") : isApprove ? t("process.approve") : t("process.reject")}
          </button>
        </div>
      </div>
    </div>
  );
}
