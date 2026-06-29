"use client";

import { ArrowRight, CheckCircle2, Play, XCircle } from "lucide-react";
import type { AuditLog, WorkflowAction } from "@/lib/types";

type AuditTimelineProps = {
  logs: AuditLog[];
};

const actionConfig: Record<WorkflowAction, { icon: typeof Play; className: string; label: string }> = {
  Start: { icon: Play, className: "audit-node-start", label: "Baslatildi" },
  Approve: { icon: CheckCircle2, className: "audit-node-approve", label: "Onaylandi" },
  Reject: { icon: XCircle, className: "audit-node-reject", label: "Reddedildi" },
};

export function AuditTimeline({ logs }: AuditTimelineProps) {
  if (logs.length === 0) {
    return <p className="empty-state">Audit kaydi yok.</p>;
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
                <strong>{config.label}</strong>
                <span className="audit-transition">
                  {log.fromStatus} <ArrowRight size={13} /> {log.toStatus}
                </span>
              </div>
              <div className="audit-meta">
                <span>{log.userDisplayName}</span>
                <time dateTime={log.createdAt}>
                  {new Date(log.createdAt).toLocaleString("tr-TR")}
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
