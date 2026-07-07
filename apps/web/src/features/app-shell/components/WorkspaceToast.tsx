import { AlertTriangle, CheckCircle2 } from "lucide-react";

type WorkspaceToastProps = {
  kind: "success" | "error";
  text: string;
};

export function WorkspaceToast({ kind, text }: WorkspaceToastProps) {
  return (
    <div className={`toast toast-${kind}`} role="status" aria-live="polite">
      {kind === "success" ? <CheckCircle2 size={17} /> : <AlertTriangle size={17} />}
      <span>{text}</span>
    </div>
  );
}
