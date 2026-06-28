"use client";

import type { ProcessStatus } from "@/lib/types";

type StatusBadgeProps = {
  status: ProcessStatus;
};

const statusLabels: Record<ProcessStatus, string> = {
  Pending: "Beklemede",
  InProgress: "Devam Ediyor",
  Completed: "Tamamlandi",
  Rejected: "Reddedildi",
};

export function StatusBadge({ status }: StatusBadgeProps) {
  return (
    <span className={`status-badge status-${status.toLowerCase()}`}>
      {statusLabels[status]}
    </span>
  );
}
