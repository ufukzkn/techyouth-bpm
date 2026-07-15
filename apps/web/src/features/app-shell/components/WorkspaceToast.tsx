import { AlertTriangle, CheckCircle2, Info } from "lucide-react";

type WorkspaceToastProps = {
  compact?: boolean;
  kind: "success" | "error" | "info";
  text: string;
};

export function WorkspaceToast({ compact = false, kind, text }: WorkspaceToastProps) {
  return (
    <div className={`toast toast-${kind}${compact ? " toast-compact" : ""}`} role="status" aria-live="polite">
      {kind === "success" ? <CheckCircle2 size={17} /> : kind === "info" ? <Info size={17} /> : <AlertTriangle size={17} />}
      <span>{text}</span>
    </div>
  );
}
