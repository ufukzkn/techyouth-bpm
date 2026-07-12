"use client";

import { ArrowUpRight, CheckCircle2, X, XCircle } from "lucide-react";
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

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog" onClick={(e) => e.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">
              {action === "Approve" ? t("dialog.approveAction") : action === "Escalate" ? t("dialog.escalateAction") : t("dialog.rejectAction")}
            </span>
            <strong>
              {action === "Approve" ? t("dialog.approveTitle") : action === "Escalate" ? t("dialog.escalateTitle") : t("dialog.rejectTitle")}
            </strong>
          </div>
          <button className="icon-button" onClick={onCancel} type="button" aria-label={t("common.close")}>
            <X size={18} />
          </button>
        </div>

        <label className="action-dialog-label">
          {t("dialog.actionNote")}
          <textarea
            className="action-dialog-textarea"
            placeholder={action === "Approve" ? t("dialog.approvePlaceholder") : action === "Escalate" ? t("dialog.escalatePlaceholder") : t("dialog.rejectPlaceholder")}
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
            className={action === "Approve" ? "success-button" : action === "Escalate" ? "escalate-button" : "danger-button"}
            disabled={disabled}
            onClick={() => onConfirm(note || (action === "Approve" ? t("dialog.defaultApproveNote") : action === "Escalate" ? t("dialog.defaultEscalateNote") : t("dialog.defaultRejectNote")))}
            type="button"
          >
            {action === "Approve" ? <CheckCircle2 size={17} /> : action === "Escalate" ? <ArrowUpRight size={17} /> : <XCircle size={17} />}
            {disabled ? t("common.saving") : action === "Approve" ? t("process.approve") : action === "Escalate" ? t("action.Escalate") : t("process.reject")}
          </button>
        </div>
      </div>
    </div>
  );
}
