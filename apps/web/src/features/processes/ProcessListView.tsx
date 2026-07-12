"use client";

import { CircleDot, Filter } from "lucide-react";
import { useMemo, useState } from "react";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { StatusBadge } from "@/features/processes/StatusBadge";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, ProcessStatus, ProcessSummary } from "@/lib/types";

type ProcessListViewProps = {
  processes: ProcessSummary[];
  language: Language;
  selectedProcessId: string;
  onSelectProcess: (id: string) => void;
};

type StatusFilter = "all" | ProcessStatus;

const filterOptions: { value: StatusFilter; labelKey: TranslationKey }[] = [
  { value: "all", labelKey: "process.filterAll" },
  { value: "Pending", labelKey: "process.filterPending" },
  { value: "InProgress", labelKey: "process.filterInProgress" },
  { value: "Completed", labelKey: "process.filterCompleted" },
  { value: "Rejected", labelKey: "process.filterRejected" },
  { value: "Escalated", labelKey: "process.filterEscalated" },
];

export function ProcessListView({ processes, language, selectedProcessId, onSelectProcess }: ProcessListViewProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [filter, setFilter] = useState<StatusFilter>("all");

  const filteredProcesses = useMemo(
    () => (filter === "all" ? processes : processes.filter((p) => p.status === filter)),
    [processes, filter],
  );

  return (
    <article className="process-card">
      <div className="process-card-header">
        <div>
          <span className="eyebrow">{t("process.listEyebrow")}</span>
          <strong>{t("process.records", { visible: filteredProcesses.length, total: processes.length })}</strong>
        </div>
        <CircleDot size={22} />
      </div>

      <div className="status-filters">
        <Filter size={14} />
        {filterOptions.map((opt) => (
          <button
            className={filter === opt.value ? "filter-chip active" : "filter-chip"}
            key={opt.value}
            onClick={() => setFilter(opt.value)}
            type="button"
          >
            {t(opt.labelKey)}
          </button>
        ))}
      </div>

      {filteredProcesses.length === 0 ? (
        <p className="empty-state">
          {processes.length === 0 ? t("process.noProcess") : t("process.noFilterMatch")}
        </p>
      ) : (
        <div className="process-list">
          {filteredProcesses.map((process) => (
            <button
              className={process.id === selectedProcessId ? "process-list-item active" : "process-list-item"}
              key={process.id}
              onClick={() => onSelectProcess(process.id)}
              type="button"
            >
              <span>
                <strong>{process.formName}</strong>
                <small>{formatApiDateTime(process.startedAt, language)}</small>
              </span>
              <StatusBadge status={process.status} language={language} />
            </button>
          ))}
        </div>
      )}
    </article>
  );
}
