import { AlertTriangle } from "lucide-react";
import type { ReactNode } from "react";

export function ConfirmationDialog({
  eyebrow,
  title,
  description,
  confirmLabel,
  tone = "danger",
  children,
  onCancel,
  onConfirm,
}: {
  eyebrow: string;
  title: string;
  description: string;
  confirmLabel: string;
  tone?: "danger" | "primary";
  children?: ReactNode;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{eyebrow}</span>
            <strong>{title}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">{description}</p>
        {children}
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>Vazgec</button>
          <button className={tone === "danger" ? "danger-button strong-danger-button" : "primary-button"} type="button" onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
