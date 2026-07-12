import { ArrowRight, CircleCheckBig, Clock3, FilePlus2, ListTodo, PlayCircle, UserRound, Workflow } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import type { ViewId } from "@/features/app-shell/navigation";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { StatusBadge } from "@/features/processes/StatusBadge";
import { EmptyState } from "@/features/ui/EmptyState";
import { api } from "@/lib/api";
import { formatApiDateTime } from "@/lib/dateTime";
import type { DashboardSummary, Language, User } from "@/lib/types";

const dashboardMetricsCache = new Map<string, DashboardSummary>();

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
  const dashboardCacheKey = `${user.id}:${user.communityId ?? "platform"}:${user.communityRoleId ?? user.role}`;
  const [summary, setSummary] = useState<DashboardSummary | null>(() => dashboardMetricsCache.get(dashboardCacheKey) ?? null);
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "error">(
    dashboardMetricsCache.has(dashboardCacheKey) ? "refreshing" : "loading",
  );
  const [hoveredChartSegment, setHoveredChartSegment] = useState<string | null>(null);
  const [chartTooltipPosition, setChartTooltipPosition] = useState<{ x: number; y: number } | null>(null);

  useEffect(() => {
    let ignore = false;

    async function loadMetrics() {
      if (!token || token.startsWith("demo-")) {
        setSummary(null);
        setStatus("idle");
        return;
      }

      try {
        const cached = dashboardMetricsCache.get(dashboardCacheKey);
        setSummary(cached ?? null);
        setStatus(cached ? "refreshing" : "loading");
        const dashboardSummary = await api.getDashboardSummary(token);
        if (!ignore) {
          dashboardMetricsCache.set(dashboardCacheKey, dashboardSummary);
          setSummary(dashboardSummary);
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
  }, [dashboardCacheKey, token]);

  const openTaskCount = summary?.openTaskCount ?? 0;
  const inProgressCount = summary?.inProgressProcessCount ?? 0;
  const completedCount = summary?.completedProcessCount ?? 0;
  const recentOpenTasks = summary?.recentOpenTasks ?? [];
  const recentProcesses = summary?.recentProcesses ?? [];
  const chartSegments = useMemo(() => {
    const circumference = 2 * Math.PI * 44;
    const total = Math.max(1, openTaskCount + inProgressCount + completedCount);
    let offset = 0;
    return [
      { key: "open", label: t("dashboard.pendingTasks"), value: openTaskCount, className: "chart-segment-open" },
      { key: "progress", label: t("dashboard.inProgress"), value: inProgressCount, className: "chart-segment-progress" },
      { key: "completed", label: t("dashboard.completed"), value: completedCount, className: "chart-segment-completed" },
    ].map((segment) => {
      const length = (segment.value / total) * circumference;
      const result = {
        ...segment,
        percentage: total === 1 && segment.value === 0 ? 0 : Math.round((segment.value / total) * 100),
        dashArray: `${length} ${circumference - length}`,
        dashOffset: -offset,
      };
      offset += length;
      return result;
    });
  }, [completedCount, inProgressCount, openTaskCount, t]);
  const canOpen = useCallback((viewId: ViewId) => visibleViewIds.includes(viewId), [visibleViewIds]);
  const shouldShowMetricLoader = status === "loading" && !summary;
  const activeChartSegment = chartSegments.find((segment) => segment.key === hoveredChartSegment);
  const currentAccessLabel = user.communityRoleName || (user.role === "SuperAdmin" ? "SuperAdmin" : "Atanmadi");

  const metricCards: Array<{ label: string; value: number; viewId?: ViewId; icon: typeof ListTodo }> = [
    { label: t("dashboard.pendingTasks"), value: openTaskCount, viewId: canOpen("tasks") ? "tasks" : undefined, icon: ListTodo },
    { label: t("dashboard.inProgress"), value: inProgressCount, viewId: canOpen("processes") ? "processes" : undefined, icon: Workflow },
    { label: t("dashboard.completed"), value: completedCount, viewId: canOpen("processes") ? "processes" : undefined, icon: CircleCheckBig },
  ];

  const availableQuickActions: Array<{ label: string; caption: string; viewId: ViewId; icon: typeof ListTodo }> = [
    { label: t("dashboard.quick.design"), caption: t("dashboard.quick.designCaption"), viewId: "forms", icon: FilePlus2 },
    { label: t("dashboard.quick.start"), caption: t("dashboard.quick.startCaption"), viewId: "runner", icon: PlayCircle },
    { label: t("dashboard.quick.tasks"), caption: t("dashboard.quick.tasksCaption"), viewId: "tasks", icon: ListTodo },
    { label: t("dashboard.quick.processes"), caption: t("dashboard.quick.processesCaption"), viewId: "processes", icon: Workflow },
  ];
  const quickActions = availableQuickActions.filter((action) => canOpen(action.viewId));

  return (
    <div className="view-panel">
      <section className="workspace-header">
        <div>
          <span className="eyebrow">{t("dashboard.eyebrow")}</span>
          <h1>{t("dashboard.welcome", { name: user.displayName })}</h1>
          {user.communityName ? <p className="dashboard-community-label"><span>{t("dashboard.communityLabel")}</span><strong>{user.communityName}</strong></p> : null}
        </div>
        <p>
          {status === "error"
            ? t("dashboard.error")
            : status === "loading"
              ? t("dashboard.loading")
              : `${user.communityName || "Platform"} / ${currentAccessLabel} - ${t("dashboard.summary", { role: currentAccessLabel })}`}
        </p>
      </section>

      {user.communityId && user.isCommunityActive === false ? (
        <p className="dashboard-community-inactive" role="status">{t("dashboard.communityInactive")}</p>
      ) : null}

      <section className="metric-grid" aria-label="Process summary">
        {metricCards.map((card) =>
          card.viewId ? (
            <button
              className="metric-card metric-action"
              key={card.label}
              onClick={() => onNavigate(card.viewId!)}
              type="button"
            >
              <card.icon className="metric-card-icon" size={20} aria-hidden="true" />
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
              <card.icon className="metric-card-icon" size={20} aria-hidden="true" />
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

      <section className="dashboard-insight-grid">
        <article className="dashboard-chart-card dashboard-chart-card-prominent">
          <div>
            <span className="eyebrow">Dagilim</span>
            <h3>Surec ve is yogunlugu</h3>
          </div>
          <div
            className={shouldShowMetricLoader ? "dashboard-donut is-loading" : "dashboard-donut"}
            aria-label={activeChartSegment ? `${activeChartSegment.label}: %${activeChartSegment.percentage}` : "Surec dagilimi"}
          >
            <svg
              aria-label={activeChartSegment ? `${activeChartSegment.label}: %${activeChartSegment.percentage}` : "Surec dagilimi"}
              className="dashboard-donut-svg"
              role="img"
              viewBox="0 0 112 112"
            >
              <circle className="chart-track" cx="56" cy="56" r="44" />
              {!shouldShowMetricLoader ? chartSegments.map((segment) => (
                <circle
                  className={segment.className}
                  cx="56"
                  cy="56"
                  key={segment.key}
                  r="44"
                  strokeDasharray={segment.dashArray}
                  strokeDashoffset={segment.dashOffset}
                  tabIndex={0}
                  aria-label={`${segment.label}: %${segment.percentage}`}
                  data-active={activeChartSegment ? activeChartSegment.key === segment.key : undefined}
                  onBlur={() => {
                    setHoveredChartSegment(null);
                    setChartTooltipPosition(null);
                  }}
                  onFocus={() => setHoveredChartSegment(segment.key)}
                  onMouseEnter={() => setHoveredChartSegment(segment.key)}
                  onMouseLeave={() => {
                    setHoveredChartSegment(null);
                    setChartTooltipPosition(null);
                  }}
                  onMouseMove={(event) => {
                    const bounds = event.currentTarget.ownerSVGElement?.getBoundingClientRect();
                    if (bounds) {
                      setChartTooltipPosition({ x: event.clientX - bounds.left, y: event.clientY - bounds.top });
                    }
                  }}
                />
              )) : null}
            </svg>
            <span className="dashboard-donut-value" aria-live="polite">
              {shouldShowMetricLoader
                ? <InlineValueLoader label={t("common.loading")} />
                : activeChartSegment
                  ? activeChartSegment.value
                  : openTaskCount + inProgressCount}
            </span>
            {activeChartSegment && chartTooltipPosition ? (
              <span
                className="chart-hover-tooltip"
                role="tooltip"
                style={{ left: chartTooltipPosition.x, top: chartTooltipPosition.y }}
              >
                %{activeChartSegment.percentage}
              </span>
            ) : null}
          </div>
          <div className="chart-legend">
            <span><i className="legend-open" /> {t("dashboard.pendingTasks")}</span>
            <span><i className="legend-progress" /> {t("dashboard.inProgress")}</span>
            <span><i className="legend-completed" /> {t("dashboard.completed")}</span>
          </div>
        </article>
        <article className="dashboard-context-card">
          <span className="eyebrow">Baglam</span>
          <h3>{user.communityName || "Platform yonetimi"}</h3>
          <div className="dashboard-role-context">
            <UserRound size={18} aria-hidden="true" />
            <p>{currentAccessLabel}</p>
          </div>
          <small>{t("dashboard.contextDescription", { count: quickActions.length })}</small>
        </article>
      </section>

      <section className="dashboard-work-grid">
        <article className="dashboard-work-card">
          <div className="dashboard-card-heading">
            <div>
              <span className="eyebrow">{t("dashboard.priorityEyebrow")}</span>
              <h3>{t("dashboard.priorityTitle")}</h3>
            </div>
            {canOpen("tasks") ? (
              <button className="dashboard-heading-action" onClick={() => onNavigate("tasks")} type="button">
                {t("dashboard.viewAll")} <ArrowRight size={15} />
              </button>
            ) : null}
          </div>
          {status === "loading" && !summary ? (
            <DashboardListSkeleton />
          ) : recentOpenTasks.length > 0 ? (
            <div className="dashboard-activity-list">
              {recentOpenTasks.map((task) => (
                <button className="dashboard-activity-item" key={task.id} onClick={() => onNavigate("tasks")} type="button">
                  <span className="dashboard-activity-icon"><ListTodo size={17} /></span>
                  <span className="dashboard-activity-copy">
                    <strong>{task.formName}</strong>
                    <small><Clock3 size={13} /> {formatApiDateTime(task.createdAt, language)}</small>
                  </span>
                  <ArrowRight size={16} aria-hidden="true" />
                </button>
              ))}
            </div>
          ) : (
            <EmptyState
              description={t("dashboard.priorityEmptyDescription")}
              icon={<CircleCheckBig size={20} />}
              title={t("dashboard.priorityEmptyTitle")}
            />
          )}
        </article>

        <article className="dashboard-work-card">
          <div className="dashboard-card-heading">
            <div>
              <span className="eyebrow">{t("dashboard.recentEyebrow")}</span>
              <h3>{t("dashboard.recentTitle")}</h3>
            </div>
            {canOpen("processes") ? (
              <button className="dashboard-heading-action" onClick={() => onNavigate("processes")} type="button">
                {t("dashboard.viewAll")} <ArrowRight size={15} />
              </button>
            ) : null}
          </div>
          {status === "loading" && !summary ? (
            <DashboardListSkeleton />
          ) : recentProcesses.length > 0 ? (
            <div className="dashboard-activity-list">
              {recentProcesses.map((process) => (
                <button className="dashboard-activity-item" key={process.id} onClick={() => onNavigate("processes")} type="button">
                  <span className="dashboard-activity-copy">
                    <strong>{process.formName}</strong>
                    <small>{formatApiDateTime(process.startedAt, language)}</small>
                  </span>
                  <span className="dashboard-activity-status">
                    <StatusBadge language={language} status={process.status} />
                  </span>
                  <ArrowRight size={16} aria-hidden="true" />
                </button>
              ))}
            </div>
          ) : (
            <EmptyState
              description={t("dashboard.recentEmptyDescription")}
              icon={<Workflow size={20} />}
              title={t("dashboard.recentEmptyTitle")}
            />
          )}
        </article>
      </section>

      {quickActions.length > 0 ? (
        <section className="dashboard-quick-section">
          <div className="dashboard-card-heading">
            <div>
              <span className="eyebrow">{t("dashboard.quickEyebrow")}</span>
              <h3>{t("dashboard.quickTitle")}</h3>
            </div>
          </div>
          <div className="dashboard-quick-actions">
            {quickActions.map((action) => (
              <button className="dashboard-quick-action" key={action.viewId} onClick={() => onNavigate(action.viewId)} type="button">
                <action.icon size={18} aria-hidden="true" />
                <span>
                  <strong>{action.label}</strong>
                  <small>{action.caption}</small>
                </span>
                <ArrowRight size={15} aria-hidden="true" />
              </button>
            ))}
          </div>
        </section>
      ) : null}
    </div>
  );
}

function DashboardListSkeleton() {
  return (
    <div className="dashboard-list-skeleton" aria-hidden="true">
      {[0, 1, 2].map((item) => (
        <span key={item} />
      ))}
    </div>
  );
}
