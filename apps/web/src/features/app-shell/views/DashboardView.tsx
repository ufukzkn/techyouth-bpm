"use client";

import { ArrowRight, Bell, CircleCheckBig, Clock3, FilePlay, FilePlus2, ListTodo, Workflow } from "lucide-react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import type { ViewId } from "@/features/app-shell/navigation";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { getNotificationTarget } from "@/features/notifications/notificationNavigation";
import { useNotificationStore } from "@/features/notifications/notificationStore";
import { StatusBadge } from "@/features/processes/StatusBadge";
import { EmptyState } from "@/features/ui/EmptyState";
import { api } from "@/lib/api";
import { formatApiDateTime } from "@/lib/dateTime";
import type { DashboardSummary, Language, NotificationItem, User } from "@/lib/types";

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
  const router = useRouter();
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
  const {
    previewItems,
    isLoading: notificationsLoading,
    isPreviewRefreshing,
    loadPreview,
    setReadState,
  } = useNotificationStore();

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
        if (!ignore) setStatus("error");
      }
    }

    void loadMetrics();
    return () => { ignore = true; };
  }, [dashboardCacheKey, token]);

  useEffect(() => {
    if (!token || token.startsWith("demo-")) return;
    const timer = window.setTimeout(() => void loadPreview(token, user.id).catch(() => undefined), 0);
    return () => window.clearTimeout(timer);
  }, [loadPreview, token, user.id]);

  const openTaskCount = summary?.openTaskCount ?? 0;
  const inProgressCount = summary?.inProgressProcessCount ?? 0;
  const completedCount = summary?.completedProcessCount ?? 0;
  const recentOpenTasks = summary?.recentOpenTasks ?? [];
  const recentProcesses = summary?.recentProcesses ?? [];
  const chartTotal = openTaskCount + inProgressCount + completedCount;
  const chartSegments = useMemo(() => {
    const circumference = 2 * Math.PI * 44;
    const total = Math.max(1, chartTotal);
    let offset = 0;
    return [
      { key: "open", label: t("dashboard.pendingTasks"), value: openTaskCount, className: "chart-segment-open", viewId: "tasks" as ViewId },
      { key: "progress", label: t("dashboard.inProgress"), value: inProgressCount, className: "chart-segment-progress", viewId: "processes" as ViewId },
      { key: "completed", label: t("dashboard.completed"), value: completedCount, className: "chart-segment-completed", viewId: "processes" as ViewId },
    ].map((segment) => {
      const length = (segment.value / total) * circumference;
      const result = {
        ...segment,
        percentage: chartTotal === 0 ? 0 : Math.round((segment.value / total) * 100),
        dashArray: `${length} ${circumference - length}`,
        dashOffset: -offset,
      };
      offset += length;
      return result;
    });
  }, [chartTotal, completedCount, inProgressCount, openTaskCount, t]);
  const canOpen = useCallback((viewId: ViewId) => visibleViewIds.includes(viewId), [visibleViewIds]);
  const shouldShowMetricLoader = status === "loading" && !summary;
  const activeChartSegment = chartSegments.find((segment) => segment.key === hoveredChartSegment);
  const currentAccessLabel = user.communityRoleName || (user.role === "SuperAdmin" ? "SuperAdmin" : "Atanmadi");
  const normalizedCommunityRole = user.communityRoleName.trim().toLocaleLowerCase("tr-TR");
  const hasUnassignedCommunityRole = user.role !== "SuperAdmin"
    && Boolean(user.communityId)
    && (!normalizedCommunityRole || ["atanmadi", "atanmadı", "unassigned"].includes(normalizedCommunityRole));
  const showTaskFocus = canOpen("tasks");

  async function openNotification(notification: NotificationItem) {
    if (token && !token.startsWith("demo-") && !notification.readAt) {
      await setReadState(token, notification.id, true).catch(() => undefined);
    }
    router.push(getNotificationTarget(notification) ?? "/inbox");
  }

  return (
    <div className="view-panel">
      <section className="workspace-header dashboard-header">
        <div>
          <span className="eyebrow">{t("dashboard.eyebrow")}</span>
          <h1>{t("dashboard.welcome", { name: user.displayName })}</h1>
          {user.communityName ? <p className="dashboard-community-label"><span>{t("dashboard.communityLabel")}</span><strong>{user.communityName}</strong></p> : null}
        </div>
        <div className="dashboard-header-side">
          <p>
            {status === "error"
              ? t("dashboard.error")
              : status === "loading"
                ? t("dashboard.loading")
                : `${user.communityName || "Platform"} / ${currentAccessLabel}`}
          </p>
          <div className="dashboard-header-actions">
            {canOpen("runner") ? (
              <button className="primary-button" onClick={() => onNavigate("runner")} type="button">
                <FilePlay size={17} /> {t("dashboard.quick.start")}
              </button>
            ) : null}
            {canOpen("forms") ? (
              <button className="secondary-button" onClick={() => onNavigate("forms")} type="button">
                <FilePlus2 size={17} /> {t("dashboard.quick.design")}
              </button>
            ) : null}
          </div>
        </div>
      </section>

      {user.communityId && user.isCommunityActive === false ? (
        <p className="dashboard-community-inactive" role="status">{t("dashboard.communityInactive")}</p>
      ) : null}
      {hasUnassignedCommunityRole ? (
        <p className="dashboard-role-unassigned" role="status">{t("dashboard.roleUnassigned")}</p>
      ) : null}

      <section className="dashboard-focus-grid">
        <article className="dashboard-chart-card dashboard-chart-card-prominent">
          <div className="dashboard-chart-copy">
            <span className="eyebrow">{t("dashboard.distributionEyebrow")}</span>
            <h3>{t("dashboard.distributionTitle")}</h3>
            <div className="chart-legend dashboard-metric-legend">
              {chartSegments.map((segment) => (
                <button
                  disabled={!canOpen(segment.viewId)}
                  key={segment.key}
                  onClick={() => onNavigate(segment.viewId)}
                  type="button"
                >
                  <span><i className={`legend-${segment.key}`} /> {segment.label}</span>
                  {shouldShowMetricLoader ? <InlineValueLoader label={t("common.loading")} /> : <strong>{segment.value}</strong>}
                </button>
              ))}
            </div>
          </div>
          <div
            className={shouldShowMetricLoader ? "dashboard-donut is-loading" : "dashboard-donut"}
            aria-label={activeChartSegment ? `${activeChartSegment.label}: %${activeChartSegment.percentage}` : t("dashboard.distributionTitle")}
          >
            <svg className="dashboard-donut-svg" role="img" viewBox="0 0 112 112">
              <circle className="chart-track" cx="56" cy="56" r="44" />
              {!shouldShowMetricLoader ? chartSegments.map((segment) => (
                <circle
                  aria-label={`${segment.label}: %${segment.percentage}`}
                  className={segment.className}
                  cx="56"
                  cy="56"
                  data-active={activeChartSegment ? activeChartSegment.key === segment.key : undefined}
                  key={segment.key}
                  onBlur={() => { setHoveredChartSegment(null); setChartTooltipPosition(null); }}
                  onFocus={() => setHoveredChartSegment(segment.key)}
                  onMouseEnter={() => setHoveredChartSegment(segment.key)}
                  onMouseLeave={() => { setHoveredChartSegment(null); setChartTooltipPosition(null); }}
                  onMouseMove={(event) => {
                    const bounds = event.currentTarget.ownerSVGElement?.getBoundingClientRect();
                    if (bounds) setChartTooltipPosition({ x: event.clientX - bounds.left, y: event.clientY - bounds.top });
                  }}
                  r="44"
                  strokeDasharray={segment.dashArray}
                  strokeDashoffset={segment.dashOffset}
                  tabIndex={0}
                />
              )) : null}
            </svg>
            <span className="dashboard-donut-value" aria-live="polite">
              {shouldShowMetricLoader ? <InlineValueLoader label={t("common.loading")} /> : activeChartSegment?.value ?? chartTotal}
            </span>
            {activeChartSegment && chartTooltipPosition ? (
              <span className="chart-hover-tooltip" role="tooltip" style={{ left: chartTooltipPosition.x, top: chartTooltipPosition.y }}>
                %{activeChartSegment.percentage}
              </span>
            ) : null}
          </div>
        </article>

        <article className="dashboard-work-card dashboard-priority-card">
          <div className="dashboard-card-heading">
            <div><span className="eyebrow">{t("dashboard.priorityEyebrow")}</span><h3>{t("dashboard.priorityTitle")}</h3></div>
            <button className="dashboard-heading-action" onClick={() => onNavigate(showTaskFocus ? "tasks" : "processes")} type="button">
              {t("dashboard.viewAll")} <ArrowRight size={15} />
            </button>
          </div>
          {status === "loading" && !summary ? <DashboardListSkeleton /> : showTaskFocus && recentOpenTasks.length > 0 ? (
            <div className="dashboard-activity-list">
              {recentOpenTasks.map((task) => (
                <button className="dashboard-activity-item" key={task.id} onClick={() => onNavigate("tasks")} type="button">
                  <span className="dashboard-activity-icon"><ListTodo size={17} /></span>
                  <span className="dashboard-activity-copy"><strong>{task.formName}</strong><small><Clock3 size={13} /> {formatApiDateTime(task.createdAt, language)}</small></span>
                  <ArrowRight size={16} aria-hidden="true" />
                </button>
              ))}
            </div>
          ) : !showTaskFocus && recentProcesses.length > 0 ? (
            <div className="dashboard-activity-list">
              {recentProcesses.map((process) => (
                <button className="dashboard-activity-item" key={process.id} onClick={() => onNavigate("processes")} type="button">
                  <span className="dashboard-activity-icon"><Workflow size={17} /></span>
                  <span className="dashboard-activity-copy"><strong>{process.formName}</strong><small>{formatApiDateTime(process.startedAt, language)}</small></span>
                  <span className="dashboard-activity-status"><StatusBadge language={language} status={process.status} /></span>
                </button>
              ))}
            </div>
          ) : (
            <EmptyState description={t("dashboard.priorityEmptyDescription")} icon={<CircleCheckBig size={20} />} title={t("dashboard.priorityEmptyTitle")} />
          )}
        </article>
      </section>

      <section className="dashboard-work-card dashboard-notification-card">
        <div className="dashboard-card-heading">
          <div>
            <span className="eyebrow">{t("dashboard.activityEyebrow")}</span>
            <h3>
              {t("dashboard.activityTitle")}
              {isPreviewRefreshing ? <span aria-label={t("common.refreshing")} className="dashboard-heading-refresh button-spinner" role="status" /> : null}
            </h3>
          </div>
          <button className="dashboard-heading-action" onClick={() => onNavigate("inbox")} type="button">
            {t("dashboard.viewAll")} <ArrowRight size={15} />
          </button>
        </div>
        {notificationsLoading && !previewItems.length ? <DashboardListSkeleton /> : previewItems.length > 0 ? (
          <div className="dashboard-activity-list dashboard-notification-list">
            {previewItems.slice(0, 4).map((notification) => (
              <button className={notification.readAt ? "dashboard-activity-item" : "dashboard-activity-item is-unread"} key={notification.id} onClick={() => void openNotification(notification)} type="button">
                <span className="dashboard-activity-icon"><Bell size={17} /></span>
                <span className="dashboard-activity-copy"><strong>{notification.title}</strong><small>{notification.message} · {formatApiDateTime(notification.createdAt, language)}</small></span>
                <ArrowRight size={16} aria-hidden="true" />
              </button>
            ))}
          </div>
        ) : (
          <EmptyState description={t("inbox.emptyDescription")} icon={<Bell size={20} />} title={t("inbox.emptyTitle")} />
        )}
      </section>
    </div>
  );
}

function DashboardListSkeleton() {
  return <div className="dashboard-list-skeleton" aria-hidden="true">{[0, 1, 2].map((item) => <span key={item} />)}</div>;
}
