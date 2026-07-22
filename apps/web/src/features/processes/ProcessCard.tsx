"use client";

import { Clock3, Workflow } from "lucide-react";
import { useState } from "react";
import { actionLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { describeProcessAssignment } from "@/features/processes/processAssignment";
import { StatusBadge } from "@/features/processes/StatusBadge";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, ProcessSummary, TaskPriority } from "@/lib/types";

type ProcessCardProps = {
  process: ProcessSummary;
  language: Language;
  isSelected: boolean;
  onSelect: (id: string) => void;
};

export function ProcessCard({ process, language, isSelected, onSelect }: ProcessCardProps) {
  const [renderedAt] = useState(() => Date.now());
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const currentStep = process.currentStep;
  const lastCompletedStep = process.lastCompletedStep;
  const currentStepTitle = currentStep?.title || currentStep?.nodeKey || process.currentNodeKey;
  return (
    <button
      className={isSelected ? "process-list-item active" : "process-list-item"}
      onClick={() => onSelect(process.id)}
      type="button"
    >
      <span>
        <strong>{process.workflowName || process.formName}</strong>
        {process.workflowName ? <small><Workflow size={12} /> {process.formName}</small> : null}
        <small><Clock3 size={12} /> {formatApiDateTime(process.startedAt, language)}</small>
        {currentStepTitle ? (
          <small className="process-context-line"><strong>{t("process.currentStep")}:</strong> {currentStepTitle}</small>
        ) : null}
        {currentStep ? (
          <small className="process-context-line"><strong>{t("process.currentResponsible")}:</strong> {describeProcessAssignment(language, currentStep)}</small>
        ) : null}
        {lastCompletedStep ? (
          <small className="process-context-line">
            <strong>{t("process.previousStep")}:</strong> {lastCompletedStep.title || lastCompletedStep.nodeKey}
            {lastCompletedStep.completedByUserDisplayName || lastCompletedStep.action ? (
              <> · {t("process.stepCompleted", {
                user: lastCompletedStep.completedByUserDisplayName || "-",
                action: lastCompletedStep.action === "Start"
                  ? t("audit.Start")
                  : lastCompletedStep.action
                    ? actionLabel(language, lastCompletedStep.action)
                    : "-",
              })}</>
            ) : null}
          </small>
        ) : null}
        {process.nearestOpenTaskDueAt ? (
          <small className={Date.parse(process.nearestOpenTaskDueAt) < renderedAt ? "process-deadline is-overdue" : "process-deadline"}>
            <Clock3 size={12} /> {language === "tr" ? "En yakın son tarih" : "Nearest deadline"}: {formatApiDateTime(process.nearestOpenTaskDueAt, language)}
          </small>
        ) : null}
      </span>
      <span className="process-card-status-stack">
        {process.highestOpenTaskPriority ? <small className={`task-priority priority-${process.highestOpenTaskPriority.toLowerCase()}`}>{translatePriority(language, process.highestOpenTaskPriority)}</small> : null}
        <StatusBadge status={process.status} language={language} />
      </span>
    </button>
  );
}

function translatePriority(language: Language, priority: TaskPriority) {
  if (language !== "tr") return priority;
  const labels: Record<TaskPriority, string> = { Low: "Düşük", Normal: "Normal", High: "Yüksek", Critical: "Kritik" };
  return labels[priority];
}
