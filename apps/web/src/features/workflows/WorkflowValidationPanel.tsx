import { AlertCircle, AlertTriangle, CheckCircle2 } from "lucide-react";
import type { WorkflowValidationIssue } from "@/features/workflows/contracts";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";

type WorkflowValidationPanelProps = {
  issues: WorkflowValidationIssue[];
};

export function WorkflowValidationPanel({ issues }: WorkflowValidationPanelProps) {
  const selectNode = useWorkflowDraftStore((state) => state.selectNode);
  const selectEdge = useWorkflowDraftStore((state) => state.selectEdge);
  const errors = issues.filter((issue) => issue.severity === "error").length;
  const warnings = issues.length - errors;

  function reveal(issue: WorkflowValidationIssue) {
    if (issue.scope === "node" && issue.entityId) {
      selectNode(issue.entityId);
    } else if (issue.scope === "transition" && issue.entityId) {
      selectEdge(issue.entityId);
    } else {
      selectNode(null);
    }
  }

  return (
    <section className="workflow-validation" aria-label="Akış doğrulaması">
      <div className="workflow-validation-heading">
        <span>
          <strong>Doğrulama</strong>
          <small>{errors > 0 ? `${errors} hata` : warnings > 0 ? `${warnings} uyarı` : "Yayına hazır"}</small>
        </span>
        {errors > 0
          ? <AlertCircle className="workflow-validation-status-error" size={18} aria-hidden="true" />
          : warnings > 0
            ? <AlertTriangle className="workflow-validation-status-warning" size={18} aria-hidden="true" />
            : <CheckCircle2 className="workflow-validation-status-success" size={18} aria-hidden="true" />}
      </div>

      {issues.length > 0 ? (
        <div className="workflow-validation-list">
          {issues.map((issue, index) => (
            <button
              className={`workflow-validation-item workflow-validation-item-${issue.severity}`}
              key={`${issue.code}-${issue.entityId ?? "workflow"}-${index}`}
              onClick={() => reveal(issue)}
              type="button"
            >
              {issue.severity === "error"
                ? <AlertCircle size={14} aria-hidden="true" />
                : <AlertTriangle size={14} aria-hidden="true" />}
              <span>{issue.message}</span>
            </button>
          ))}
        </div>
      ) : (
        <p className="workflow-validation-empty">Yerel kontroller tamamlandı.</p>
      )}
    </section>
  );
}
