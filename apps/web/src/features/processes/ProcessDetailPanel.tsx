"use client";

import { AuditTimeline } from "@/features/processes/AuditTimeline";
import { StatusBadge } from "@/features/processes/StatusBadge";
import type { ProcessDetail } from "@/lib/types";

type ProcessDetailPanelProps = {
  detail: ProcessDetail | null;
};

export function ProcessDetailPanel({ detail }: ProcessDetailPanelProps) {
  if (!detail) {
    return (
      <>
        <article className="process-card">
          <div className="process-card-header">
            <div>
              <span className="eyebrow">Surec detayi</span>
              <strong>Secili surec yok</strong>
            </div>
          </div>
          <p className="empty-state">Detay goruntulemek icin bir surec sec.</p>
        </article>

        <article className="process-card audit-card">
          <span className="eyebrow">Audit Log</span>
          <p className="empty-state">Audit kaydi yok.</p>
        </article>
      </>
    );
  }

  const openTaskCount = detail.tasks.filter((t) => t.status === "Open").length;
  const completedTaskCount = detail.tasks.filter((t) => t.status === "Completed").length;

  return (
    <>
      <article className="process-card">
        <div className="process-card-header">
          <div>
            <span className="eyebrow">Surec detayi</span>
            <strong>{detail.formName}</strong>
          </div>
          <StatusBadge status={detail.status} />
        </div>

        <dl className="detail-list">
          <div>
            <dt>Baslangic</dt>
            <dd>{new Date(detail.startedAt).toLocaleString("tr-TR")}</dd>
          </div>
          <div>
            <dt>Durum</dt>
            <dd>{detail.status}</dd>
          </div>
          {detail.completedAt ? (
            <div>
              <dt>Tamamlanma</dt>
              <dd>{new Date(detail.completedAt).toLocaleString("tr-TR")}</dd>
            </div>
          ) : null}
          <div>
            <dt>Tasklar</dt>
            <dd>{openTaskCount} acik / {completedTaskCount} tamamlanan</dd>
          </div>
        </dl>

        <pre className="json-preview compact-json">{JSON.stringify(detail.formData, null, 2)}</pre>
      </article>

      <article className="process-card audit-card">
        <span className="eyebrow">Audit Log — {detail.auditLogs.length} kayit</span>
        <AuditTimeline logs={detail.auditLogs} />
      </article>
    </>
  );
}
