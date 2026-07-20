import type {
  WorkflowDefinitionDraft,
  WorkflowValidationIssue,
} from "@/features/workflows/contracts";
import { ApiError } from "@/lib/api";
import type { Language } from "@/lib/types";

export function workflowApiValidationIssues(
  error: unknown,
  draft: WorkflowDefinitionDraft,
  language: Language,
): WorkflowValidationIssue[] {
  if (!(error instanceof ApiError)) {
    return [];
  }

  return error.errors.map((message, index) => mapApiIssue(message, index, draft, language));
}

function mapApiIssue(
  message: string,
  index: number,
  draft: WorkflowDefinitionDraft,
  language: Language,
): WorkflowValidationIssue {
  const unknownField = /^Condition path '([^']+)' references an unknown form field\.$/.exec(message);
  if (unknownField) {
    const path = unknownField[1];
    const edge = draft.edges.find((candidate) => candidate.data?.condition?.fieldKey === path);
    return issue(
      `server.gateway.condition.field.missing.${index}`,
      language === "tr"
        ? `“${path}” koşulu bağlı formda bulunmayan bir alanı kullanıyor. Bağlantıyı seçip yayınlanmış formdaki bir alanı seçin.`
        : `The “${path}” condition uses a field that is missing from the bound form. Select a field from the published form.`,
      edge ? "transition" : "workflow",
      edge?.id,
    );
  }

  if (message === "The start form version must be published and belong to the process community.") {
    const start = draft.nodes.find((node) => node.type === "start");
    return issue(
      `server.start.form.invalid.${index}`,
      language === "tr"
        ? "Başlangıç formu yayınlanmış olmalı ve süreçle aynı toplulukta bulunmalıdır."
        : message,
      start ? "node" : "workflow",
      start?.id,
    );
  }

  const taskForm = /^Task form version '([^']+)' must be published and belong to the process community\.$/.exec(message);
  if (taskForm) {
    const task = draft.nodes.find((node) =>
      node.type === "userTask" && node.data.formBinding?.formVersionId === taskForm[1]);
    return issue(
      `server.task.form.invalid.${index}`,
      language === "tr"
        ? "Görev formu yayınlanmış olmalı ve süreçle aynı toplulukta bulunmalıdır."
        : message,
      task ? "node" : "workflow",
      task?.id,
    );
  }

  const entityKey = extractEntityKey(message);
  const node = entityKey ? draft.nodes.find((candidate) => candidate.id === entityKey) : undefined;
  return issue(
    `server.workflow.validation.${index}`,
    language === "tr" ? `Sunucu doğrulaması: ${message}` : message,
    node ? "node" : "workflow",
    node?.id,
  );
}

function extractEntityKey(message: string) {
  const patterns = [
    /(?:User task|task|Task|Gateway|gateway|Swimlane|swimlane|Node|node) '([^']+)'/,
    /for task '([^']+)'/,
  ];
  return patterns.map((pattern) => pattern.exec(message)?.[1]).find(Boolean);
}

function issue(
  code: string,
  message: string,
  scope: WorkflowValidationIssue["scope"],
  entityId?: string,
): WorkflowValidationIssue {
  return { code, severity: "error", scope, entityId, message };
}
