"use client";

import { ArrowRight, ArrowUpRight, CheckCheck, CheckCircle2, Play, RotateCcw, XCircle } from "lucide-react";
import { statusLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { formatApiDateTime } from "@/lib/dateTime";
import type { AuditLog, Language, WorkflowAction } from "@/lib/types";

type AuditTimelineProps = {
  logs: AuditLog[];
  language: Language;
};

const actionConfig: Record<WorkflowAction, { icon: typeof Play; className: string }> = {
  Start: { icon: Play, className: "audit-node-start" },
  Approve: { icon: CheckCircle2, className: "audit-node-approve" },
  Reject: { icon: XCircle, className: "audit-node-reject" },
  Escalate: { icon: ArrowUpRight, className: "audit-node-escalate" },
  SendBack: { icon: RotateCcw, className: "audit-node-send-back" },
  Complete: { icon: CheckCheck, className: "audit-node-approve" },
};

export function AuditTimeline({ logs, language }: AuditTimelineProps) {
  if (logs.length === 0) {
    return <p className="empty-state">{translate(language, "process.noAudit")}</p>;
  }

  return (
    <div className="audit-timeline">
      {logs.map((log) => {
        const config = actionConfig[log.action];
        const Icon = config.icon;
        return (
          <div className={`audit-entry ${config.className}`} key={log.id}>
            <div className="audit-node">
              <Icon size={16} />
            </div>
            <div className="audit-content">
              <div className="audit-header">
                <strong>{translate(language, `audit.${log.action}` as TranslationKey)}</strong>
                <span className="audit-transition">
                  {statusLabel(language, log.fromStatus)} <ArrowRight size={13} /> {statusLabel(language, log.toStatus)}
                </span>
              </div>
              <div className="audit-meta">
                <span>
                  {log.userDisplayName} / {log.userUsername}
                </span>
                <time dateTime={log.createdAt}>
                  {formatApiDateTime(log.createdAt, language)}
                </time>
              </div>
              {log.note ? <p className="audit-note">{log.note}</p> : null}
            </div>
          </div>
        );
      })}
    </div>
  );
}
