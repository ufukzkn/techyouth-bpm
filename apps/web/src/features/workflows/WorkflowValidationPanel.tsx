import { AlertCircle, AlertTriangle, CheckCircle2 } from "lucide-react";
import type { WorkflowValidationIssue } from "@/features/workflows/contracts";
import { localizeWorkflowValidationIssue, workflowText } from "@/features/workflows/workflowI18n";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";
import type { Language } from "@/lib/types";

type WorkflowValidationPanelProps = {
  issues: WorkflowValidationIssue[];
  language: Language;
};

export function WorkflowValidationPanel({ issues, language }: WorkflowValidationPanelProps) {
  const selectNode = useWorkflowDraftStore((state) => state.selectNode);
  const selectEdge = useWorkflowDraftStore((state) => state.selectEdge);
  const errors = issues.filter((issue) => issue.severity === "error").length;
  const warnings = issues.length - errors;
  const text = (tr: string, en: string) => workflowText(language, tr, en);

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
    <section className="workflow-validation" aria-label={text("Akış doğrulaması", "Workflow validation")}>
      <div className="workflow-validation-heading">
        <span>
          <strong>{text("Doğrulama", "Validation")}</strong>
          <small>{errors > 0
            ? text(`${errors} hata`, `${errors} ${errors === 1 ? "error" : "errors"}`)
            : warnings > 0
              ? text(`${warnings} uyarı`, `${warnings} ${warnings === 1 ? "warning" : "warnings"}`)
              : text("Yayına hazır", "Ready to publish")}</small>
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
              <span>{localizeWorkflowValidationIssue(issue, language)}</span>
            </button>
          ))}
        </div>
      ) : (
        <p className="workflow-validation-empty">{text("Yerel kontroller tamamlandı.", "Local checks completed.")}</p>
      )}
    </section>
  );
}
