import { useCallback, useEffect, useState } from "react";
import type { ViewId } from "@/features/app-shell/navigation";
import { roleLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Language, ProcessSummary, ProcessTask, User } from "@/lib/types";

let dashboardMetricsCache: { processes: ProcessSummary[]; tasks: ProcessTask[] } | null = null;

export function DashboardView({
  token,
  user,
  language,
  visibleViewIds,
  onNavigate,
}: {
  token: string | null;
  user: User;
  language: Language;
  visibleViewIds: ViewId[];
  onNavigate: (viewId: ViewId) => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [processes, setProcesses] = useState<ProcessSummary[]>(() => dashboardMetricsCache?.processes ?? []);
  const [tasks, setTasks] = useState<ProcessTask[]>(() => dashboardMetricsCache?.tasks ?? []);
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "error">(
    dashboardMetricsCache ? "refreshing" : "loading",
  );

  useEffect(() => {
    let ignore = false;

    async function loadMetrics() {
      if (!token || token.startsWith("demo-")) {
        setStatus("idle");
        return;
      }

      try {
        setStatus(dashboardMetricsCache ? "refreshing" : "loading");
        const [processResult, taskResult] = await Promise.all([api.listProcesses(token), api.listMyTasks(token)]);
        if (!ignore) {
          dashboardMetricsCache = { processes: processResult, tasks: taskResult };
          setProcesses(processResult);
          setTasks(taskResult);
          setStatus("idle");
        }
      } catch {
        if (!ignore) {
          setStatus("error");
        }
      }
    }

    void loadMetrics();

    return () => {
      ignore = true;
    };
  }, [token]);

  const openTaskCount = tasks.filter((task) => task.status === "Open").length;
  const inProgressCount = processes.filter((process) => process.status === "InProgress").length;
  const completedCount = processes.filter((process) => process.status === "Completed").length;
  const canOpen = useCallback((viewId: ViewId) => visibleViewIds.includes(viewId), [visibleViewIds]);
  const shouldShowMetricLoader = status === "loading" && !dashboardMetricsCache;

  const metricCards: Array<{ label: string; value: number; viewId?: ViewId }> = [
    { label: t("dashboard.pendingTasks"), value: openTaskCount, viewId: canOpen("tasks") ? "tasks" : undefined },
    { label: t("dashboard.inProgress"), value: inProgressCount, viewId: canOpen("processes") ? "processes" : undefined },
    { label: t("dashboard.completed"), value: completedCount, viewId: canOpen("processes") ? "processes" : undefined },
  ];

  const flowSteps: Array<{ label: string; caption: string; viewId: ViewId }> = [
    { label: t("dashboard.flow.session"), caption: t("dashboard.flow.sessionCaption"), viewId: "settings" },
    {
      label: t("dashboard.flow.formDefinition"),
      caption: t("dashboard.flow.formDefinitionCaption"),
      viewId: "forms",
    },
    {
      label: t("dashboard.flow.processInstance"),
      caption: t("dashboard.flow.processInstanceCaption"),
      viewId: "runner",
    },
    { label: t("dashboard.flow.taskAction"), caption: t("dashboard.flow.taskActionCaption"), viewId: "tasks" },
    { label: t("dashboard.flow.auditLog"), caption: t("dashboard.flow.auditLogCaption"), viewId: "processes" },
  ];

  return (
    <div className="view-panel">
      <section className="workspace-header">
        <div>
          <span className="eyebrow">{t("dashboard.eyebrow")}</span>
          <h1>{t("dashboard.title")}</h1>
        </div>
        <p>
          {status === "error"
            ? t("dashboard.error")
            : status === "loading"
              ? t("dashboard.loading")
              : t("dashboard.summary", { role: roleLabel(language, user.role) })}
        </p>
      </section>

      <section className="metric-grid" aria-label="Process summary">
        {metricCards.map((card) =>
          card.viewId ? (
            <button
              className="metric-card metric-action"
              key={card.label}
              onClick={() => onNavigate(card.viewId!)}
              type="button"
            >
              <span>{card.label}</span>
              {shouldShowMetricLoader ? (
                <span className="metric-inline-loader" aria-label={t("common.loading")}>
                  <span className="button-spinner" aria-hidden="true" />
                </span>
              ) : (
                <strong>{card.value}</strong>
              )}
            </button>
          ) : (
            <article className="metric-card" key={card.label}>
              <span>{card.label}</span>
              {shouldShowMetricLoader ? (
                <span className="metric-inline-loader" aria-label={t("common.loading")}>
                  <span className="button-spinner" aria-hidden="true" />
                </span>
              ) : (
                <strong>{card.value}</strong>
              )}
            </article>
          ),
        )}
      </section>

      <section className="flow-preview">
        {flowSteps
          .filter((step) => canOpen(step.viewId))
          .map((step) => (
            <button className="flow-step" key={step.label} onClick={() => onNavigate(step.viewId)} type="button">
              <strong>{step.label}</strong>
              <span>{step.caption}</span>
            </button>
          ))}
      </section>
    </div>
  );
}
