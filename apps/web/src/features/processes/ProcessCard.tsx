"use client";

import { StatusBadge } from "@/features/processes/StatusBadge";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, ProcessSummary } from "@/lib/types";

type ProcessCardProps = {
  process: ProcessSummary;
  language: Language;
  isSelected: boolean;
  onSelect: (id: string) => void;
};

export function ProcessCard({ process, language, isSelected, onSelect }: ProcessCardProps) {
  return (
    <button
      className={isSelected ? "process-list-item active" : "process-list-item"}
      onClick={() => onSelect(process.id)}
      type="button"
    >
      <span>
        <strong>{process.formName}</strong>
        <small>{formatApiDateTime(process.startedAt, language)}</small>
      </span>
      <StatusBadge status={process.status} language={language} />
    </button>
  );
}
