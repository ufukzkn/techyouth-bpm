"use client";

import { CheckCircle2, X, XCircle } from "lucide-react";
import { useState } from "react";
import type { WorkflowAction } from "@/lib/types";

type TaskActionDialogProps = {
  action: Exclude<WorkflowAction, "Start">;
  onConfirm: (note: string) => void;
  onCancel: () => void;
  disabled: boolean;
};

export function TaskActionDialog({ action, onConfirm, onCancel, disabled }: TaskActionDialogProps) {
  const [note, setNote] = useState("");
  const isApprove = action === "Approve";

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog" onClick={(e) => e.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{isApprove ? "Onay" : "Red"} aksiyonu</span>
            <strong>{isApprove ? "Sureci onayla" : "Sureci reddet"}</strong>
          </div>
          <button className="icon-button" onClick={onCancel} type="button" aria-label="Kapat">
            <X size={18} />
          </button>
        </div>

        <label className="action-dialog-label">
          Aksiyon notu
          <textarea
            className="action-dialog-textarea"
            placeholder={isApprove ? "Onay aciklamasi ekle..." : "Red sebebini yaz..."}
            rows={3}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>

        <div className="action-dialog-actions">
          <button className="secondary-button" onClick={onCancel} type="button" disabled={disabled}>
            Vazgec
          </button>
          <button
            className={isApprove ? "success-button" : "danger-button"}
            disabled={disabled}
            onClick={() => onConfirm(note || (isApprove ? "UI uzerinden onaylandi." : "UI uzerinden reddedildi."))}
            type="button"
          >
            {isApprove ? <CheckCircle2 size={17} /> : <XCircle size={17} />}
            {disabled ? "Kaydediliyor..." : isApprove ? "Onayla" : "Reddet"}
          </button>
        </div>
      </div>
    </div>
  );
}
