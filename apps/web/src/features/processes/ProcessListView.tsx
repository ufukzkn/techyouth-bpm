"use client";

import { CircleDot, Filter } from "lucide-react";
import { useMemo, useState } from "react";
import { StatusBadge } from "@/features/processes/StatusBadge";
import type { ProcessStatus, ProcessSummary } from "@/lib/types";

type ProcessListViewProps = {
  processes: ProcessSummary[];
  selectedProcessId: string;
  onSelectProcess: (id: string) => void;
};

type StatusFilter = "all" | ProcessStatus;

const filterOptions: { value: StatusFilter; label: string }[] = [
  { value: "all", label: "Tumu" },
  { value: "Pending", label: "Beklemede" },
  { value: "InProgress", label: "Devam Eden" },
  { value: "Completed", label: "Tamamlanan" },
  { value: "Rejected", label: "Reddedilen" },
];

export function ProcessListView({ processes, selectedProcessId, onSelectProcess }: ProcessListViewProps) {
  const [filter, setFilter] = useState<StatusFilter>("all");

  const filteredProcesses = useMemo(
    () => (filter === "all" ? processes : processes.filter((p) => p.status === filter)),
    [processes, filter],
  );

  return (
    <article className="process-card">
      <div className="process-card-header">
        <div>
          <span className="eyebrow">Surec listesi</span>
          <strong>{filteredProcesses.length} / {processes.length} kayit</strong>
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
            {opt.label}
          </button>
        ))}
      </div>

      {filteredProcesses.length === 0 ? (
        <p className="empty-state">
          {processes.length === 0 ? "Henuz baslatilmis surec yok." : "Bu filtreyle eslesen surec yok."}
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
                <small>{new Date(process.startedAt).toLocaleString("tr-TR")}</small>
              </span>
              <StatusBadge status={process.status} />
            </button>
          ))}
        </div>
      )}
    </article>
  );
}
