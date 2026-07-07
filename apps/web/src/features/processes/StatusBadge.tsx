"use client";

import { statusLabel } from "@/features/i18n/translations";
import type { Language, ProcessStatus } from "@/lib/types";

type StatusBadgeProps = {
  status: ProcessStatus;
  language: Language;
};

export function StatusBadge({ status, language }: StatusBadgeProps) {
  return (
    <span className={`status-badge status-${status.toLowerCase()}`}>
      {statusLabel(language, status)}
    </span>
  );
}
