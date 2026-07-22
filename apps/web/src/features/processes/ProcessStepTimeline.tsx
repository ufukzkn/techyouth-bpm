"use client";

import { CheckCircle2, CircleDot, RotateCcw, XCircle } from "lucide-react";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { describeProcessAssignment } from "@/features/processes/processAssignment";
import { JsonViewer } from "@/features/ui/JsonViewer";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, ProcessStepExecution } from "@/lib/types";

export function ProcessStepTimeline({ executions, language }: { executions: ProcessStepExecution[]; language: Language }) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);

  if (executions.length === 0) {
    return <p className="empty-state">{t("process.executionEmpty")}</p>;
  }

  return (
    <ol className="process-step-timeline">
      {executions.map((execution) => {
        const StepIcon = execution.status === "Completed"
          ? CheckCircle2
          : execution.status === "Cancelled"
            ? XCircle
            : execution.attempt > 1
              ? RotateCcw
              : CircleDot;

        return (
          <li key={execution.id}>
            <span className={`process-step-marker status-${execution.status.toLowerCase()}`}><StepIcon size={16} /></span>
            <div className="process-step-content">
              <div className="process-step-heading">
                <strong>{execution.nodeTitle || execution.nodeKey}</strong>
                <span>{t("process.attempt", { count: execution.attempt })}</span>
              </div>
              {execution.assignmentType || execution.teamName || execution.communityRoleName || execution.assignedUserDisplayName ? (
                <div className="process-step-assignment">
                  <small><strong>{t("process.currentResponsible")}:</strong> {describeProcessAssignment(language, execution)}</small>
                </div>
              ) : null}
              <small>{t("process.enteredAt")}: {formatApiDateTime(execution.enteredAt, language)}</small>
              {execution.completedAt ? <small>{t("process.completedAt")}: {formatApiDateTime(execution.completedAt, language)}</small> : null}
              {execution.completedByUserDisplayName ? <small>{t("process.completedBy")}: {execution.completedByUserDisplayName}</small> : null}
              {execution.action ? <small>{translate(language, `audit.${execution.action}` as TranslationKey)}</small> : null}
              {execution.note ? <p className="process-step-note">{execution.note}</p> : null}
              {Object.keys(execution.output ?? {}).length > 0 ? (
                <JsonViewer className="process-step-output" language={language} value={execution.output} />
              ) : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
