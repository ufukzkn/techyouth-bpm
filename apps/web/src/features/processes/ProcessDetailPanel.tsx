"use client";

import { AuditTimeline } from "@/features/processes/AuditTimeline";
import { ProcessStepTimeline } from "@/features/processes/ProcessStepTimeline";

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

  const openTaskCount = detail.tasks.filter((t) => t.status === "Open" || t.status === "Claimed").length;
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
          {detail.currentStep ? (
            <>
              <div>
                <dt>{t("process.currentStepTitle")}</dt>
                <dd>{detail.currentStep.title || detail.currentStep.nodeKey}</dd>
              </div>
              {detail.currentStep.teamName ? (
                <div><dt>{t("process.assignmentTeam")}</dt><dd>{detail.currentStep.teamName}</dd></div>
              ) : null}
              {detail.currentStep.communityRoleName ? (
                <div><dt>{t("process.assignmentRole")}</dt><dd>{detail.currentStep.communityRoleName}</dd></div>
              ) : null}
              {detail.currentStep.assignedUserDisplayName || detail.currentStep.claimedByUserDisplayName ? (
                <div>
                  <dt>{detail.currentStep.claimedByUserDisplayName ? t("process.claimOwner") : t("process.assignmentUser")}</dt>
                  <dd>{detail.currentStep.claimedByUserDisplayName || detail.currentStep.assignedUserDisplayName}</dd>
                </div>
              ) : null}
              <div>
                <dt>{t("process.currentStepSince")}</dt>
                <dd>{formatApiDateTime(detail.currentStep.enteredAt, language)}</dd>
              </div>
            </>
          ) : detail.currentNodeKey ? (
            <div>
              <dt>{t("process.currentNode")}</dt>
              <dd>{detail.currentNodeKey}</dd>
            </div>
          ) : null}
          {detail.processDefinitionVersionId ? (
            <div>
              <dt>{t("process.definitionVersion")}</dt>
              <dd title={detail.processDefinitionVersionId}>{detail.processDefinitionVersionId.slice(0, 8)}</dd>
            </div>
          ) : null}
        </dl>

        <JsonViewer className="compact-json" language={language} value={detail.formData} />
        {detail.variables && Object.keys(detail.variables).length > 0 ? (
          <div className="process-variables">
            <span className="eyebrow">{t("process.variables")}</span>
            <JsonViewer className="compact-json" language={language} value={detail.variables} />
          </div>
        ) : null}
      </article>

      {detail.stepExecutions ? (
        <article className="process-card process-step-card">
          <span className="eyebrow">{t("process.executionHistory")}</span>
          <ProcessStepTimeline executions={detail.stepExecutions} language={language} />
        </article>
      ) : null}

      <article className="process-card audit-card">
        <span className="eyebrow">{t("process.auditRecords", { count: detail.auditLogs.length })}</span>
        <AuditTimeline logs={detail.auditLogs} language={language} />
      </article>
    </>
  );
}
