"use client";

import { Clock3, Workflow } from "lucide-react";
import { useState } from "react";
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
