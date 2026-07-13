"use client";

import { AuditTimeline } from "@/features/processes/AuditTimeline";
import { ProcessFlowIndicator } from "@/features/processes/ProcessFlowIndicator";
import { statusLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { StatusBadge } from "@/features/processes/StatusBadge";
import { JsonViewer } from "@/features/ui/JsonViewer";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, ProcessDetail } from "@/lib/types";

type ProcessDetailPanelProps = {
  detail: ProcessDetail | null;
  language: Language;
};

export function ProcessDetailPanel({ detail, language }: ProcessDetailPanelProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);

  if (!detail) {
    return (
      <>
        <article className="process-card">
          <div className="process-card-header">
            <div>
              <span className="eyebrow">{t("process.detail")}</span>
              <strong>{t("process.noSelected")}</strong>
            </div>
          </div>
          <p className="empty-state">{t("process.selectForDetail")}</p>
        </article>

        <article className="process-card audit-card">
          <span className="eyebrow">Audit Log</span>
          <p className="empty-state">{t("process.noAudit")}</p>
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
            <span className="eyebrow">{t("process.detail")}</span>
            <strong>{detail.formName}</strong>
          </div>
          <StatusBadge status={detail.status} language={language} />
        </div>

        <ProcessFlowIndicator status={detail.status} language={language} />

        <dl className="detail-list">
          <div>
            <dt>{t("process.start")}</dt>
            <dd>{formatApiDateTime(detail.startedAt, language)}</dd>
          </div>
          <div>
            <dt>{t("process.status")}</dt>
            <dd>{statusLabel(language, detail.status)}</dd>
          </div>
          {detail.completedAt ? (
            <div>
              <dt>{t("process.completedAt")}</dt>
              <dd>{formatApiDateTime(detail.completedAt, language)}</dd>
            </div>
          ) : null}
          <div>
            <dt>{t("process.tasks")}</dt>
            <dd>{t("process.taskSummary", { open: openTaskCount, completed: completedTaskCount })}</dd>
          </div>
        </dl>

        <JsonViewer className="compact-json" language={language} value={detail.formData} />
      </article>

      <article className="process-card audit-card">
        <span className="eyebrow">{t("process.auditRecords", { count: detail.auditLogs.length })}</span>
        <AuditTimeline logs={detail.auditLogs} language={language} />
      </article>
    </>
  );
}
