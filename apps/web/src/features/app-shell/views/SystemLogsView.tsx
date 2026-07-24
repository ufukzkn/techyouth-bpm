import {
  Archive,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  FileText,
  Fingerprint,
  History,
  ListChecks,
  RefreshCw,
  ScrollText,
  Search,
  UserCog,
  Workflow,
  type LucideIcon,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { auditCategories, type AuditCategory, type AuditHistoryMode, type SelectedAuditHistory } from "@/features/app-shell/types";
import { getAuditCategory, getAuditHistoryTitle, getAuditTargetLabel, getFocusedAuditLogs, formatAuditAction } from "@/features/app-shell/auditUtils";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { SystemAuditTimeline } from "@/features/app-shell/components/SystemAuditTimeline";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { formatSessionExpiry } from "@/features/app-shell/sessionFormatters";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { ArchivedAuditEvent, CommunityDeletionArchive, Language, SystemAuditLog, User } from "@/lib/types";
import { SlidingSegmentedControl } from "@/features/ui/SlidingSegmentedControl";

const minimumRefreshDelayMs = 500;
const auditCategoryIcons: Record<AuditCategory, LucideIcon> = {
  all: ScrollText,
  identity: Fingerprint,
  access: UserCog,
  forms: FileText,
  processes: Workflow,
  tasks: ListChecks,
};

export function SystemLogsView({ activeUser, language, token }: { activeUser: User; language: Language; token: string | null }) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [auditMode, setAuditMode] = useState<"active" | "archive">("active");
  const [archives, setArchives] = useState<CommunityDeletionArchive[]>([]);
  const [selectedArchiveId, setSelectedArchiveId] = useState("");
  const [isArchiveListLoading, setIsArchiveListLoading] = useState(false);
  const [totalLogs, setTotalLogs] = useState(0);
  const [categoryCounts, setCategoryCounts] = useState<Record<AuditCategory, number | null>>({
    all: null,
    identity: null,
    access: null,
    forms: null,
    processes: null,
    tasks: null,
  });
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<AuditCategory>("all");
  const [sortBy, setSortBy] = useState<"createdAt" | "action" | "actor">("createdAt");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [selectedHistory, setSelectedHistory] = useState<SelectedAuditHistory | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const pageSize = 5;
  const trimmedQuery = searchQuery.trim().toLowerCase();
  const auditCountCache = useRef(new Map<string, Record<AuditCategory, number>>());
  const shouldQueryLogs = trimmedQuery.length >= 2 || selectedCategory !== "all";
  const selectedArchive = archives.find((archive) => archive.id === selectedArchiveId) ?? null;
  const isArchiveMode = auditMode === "archive";

  const loadArchives = useCallback(async () => {
    if (!token || token.startsWith("demo-") || activeUser.role !== "SuperAdmin") {
      return;
    }

    setIsArchiveListLoading(true);
    try {
      const result = await api.listAuditArchives(token);
      setArchives(result);
      setSelectedArchiveId((current) =>
        current && result.some((archive) => archive.id === current)
          ? current
          : result[0]?.id ?? "",
      );
    } catch (error) {
      setToast({
        kind: "error",
        text: localizeApiError(error, language, "Silinen topluluk arşivleri yüklenemedi."),
      });
    } finally {
      setIsArchiveListLoading(false);
    }
  }, [activeUser.role, language, token]);

  const loadAuditCounts = useCallback(
    async (options: { force?: boolean } = {}) => {
      if (!token || token.startsWith("demo-")) {
        return;
      }
      if (isArchiveMode && !selectedArchiveId) {
        setCategoryCounts({ all: 0, identity: 0, access: 0, forms: 0, processes: 0, tasks: 0 });
        return;
      }

      const cacheKey = isArchiveMode ? `archive:${selectedArchiveId}` : "active:global";
      const cachedCounts = options.force ? undefined : auditCountCache.current.get(cacheKey);
      if (cachedCounts) {
        setCategoryCounts(cachedCounts);
        return;
      }

      try {
        const counts = isArchiveMode
          ? await api.listArchivedAuditCounts(token, selectedArchiveId)
          : await api.listSystemAuditCounts(token);
        const nextCounts = {
          all: counts.all,
          identity: counts.identity,
          access: counts.access,
          forms: counts.forms,
          processes: counts.processes,
          tasks: counts.tasks,
        };
        auditCountCache.current.set(cacheKey, nextCounts);
        setCategoryCounts(nextCounts);
      } catch {
        if (options.force) {
          setCategoryCounts({
            all: null,
            identity: null,
            access: null,
            forms: null,
            processes: null,
            tasks: null,
          });
        }
      }
    },
    [isArchiveMode, selectedArchiveId, token],
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadArchives();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [loadArchives]);

  useEffect(() => {
    if (!toast) {
      return;
    }

    const timer = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const loadLogs = useCallback(
    async (options: { manual?: boolean } = {}) => {
      if (!token || token.startsWith("demo-")) {
        return;
      }

      const isManualRefresh = options.manual === true;
      const refreshStartedAt = Date.now();

      if (!shouldQueryLogs) {
        if (isManualRefresh) {
          setIsRefreshing(true);
          await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
          setIsRefreshing(false);
          setToast({ kind: "success", text: t("common.refreshed") });
        }
        setLogs([]);
        setTotalLogs(0);
        setSelectedHistory(null);
        return;
      }

      const query = searchQuery.trim();
      if (isManualRefresh) {
        setIsRefreshing(true);
      } else {
        setIsLoading(true);
      }

      try {
        const requestParams = {
          query,
          category: selectedCategory,
          page,
          pageSize,
          sortBy,
          sortDirection,
        };
        const auditResult = isArchiveMode
          ? await api.listArchivedAuditLogs(token, selectedArchiveId, requestParams)
          : await api.listSystemAuditLogs(token, requestParams);
        setLogs(
          isArchiveMode
            ? (auditResult.items as ArchivedAuditEvent[]).map((item) => archivedEventToSystemLog(item, selectedArchive))
            : (auditResult.items as SystemAuditLog[] ?? []),
        );
        setTotalLogs(auditResult.totalCount ?? 0);
        if (options.manual) {
          void loadAuditCounts({ force: true });
        }
        if (isManualRefresh) {
          await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
          setToast({ kind: "success", text: t("common.refreshed") });
        }
      } catch (error) {
        if (isManualRefresh) {
          await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
          setToast({ kind: "error", text: localizeApiError(error, language, t("common.refreshFailed")) });
        }
      } finally {
        if (isManualRefresh) {
          setIsRefreshing(false);
        } else {
          setIsLoading(false);
        }
      }
    },
    [isArchiveMode, language, loadAuditCounts, page, pageSize, searchQuery, selectedArchive, selectedArchiveId, selectedCategory, shouldQueryLogs, sortBy, sortDirection, t, token],
  );

  function refreshLogs() {
    auditCountCache.current.clear();
    if (isArchiveMode) {
      void loadArchives();
    }
    void loadLogs({ manual: true });
    void loadAuditCounts({ force: true });
  }

  function changeAuditMode(mode: "active" | "archive") {
    setAuditMode(mode);
    setLogs([]);
    setTotalLogs(0);
    setSelectedHistory(null);
    setPage(1);
    setCategoryCounts(emptyAuditCounts());
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadLogs();
    }, 250);

    return () => window.clearTimeout(timer);
  }, [loadLogs]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadAuditCounts();
    }, 250);

    return () => window.clearTimeout(timer);
  }, [loadAuditCounts]);
  const totalPages = Math.max(1, Math.ceil(totalLogs / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleLogs = shouldQueryLogs ? logs : [];
  const resultSummaryCount = trimmedQuery ? totalLogs : categoryCounts[selectedCategory] ?? totalLogs;
  const selectedLog = selectedHistory ? logs.find((log) => log.id === selectedHistory.logId) ?? null : null;
  const selectedLogCategory = selectedLog ? getAuditCategory(selectedLog) : null;
  const selectedHistoryTitle =
    selectedLog && selectedHistory
      ? getAuditHistoryTitle(selectedLog, selectedHistory.mode, language)
      : t("logs.noSelection");
  const historyFilterOptions = useMemo(() => {
    if (!selectedLog) {
      return [];
    }

    const options: Array<{ mode: AuditHistoryMode; label: string }> = [
      { mode: "related", label: t("logs.historyFilter.related") },
      { mode: "actor", label: t("logs.historyFilter.actor", { value: selectedLog.actorUsername }) },
    ];

    if (selectedLog.entityType === "User" && selectedLog.entityId) {
      options.push({
        mode: "target",
        label: t("logs.historyFilter.target", { value: getAuditTargetLabel(selectedLog) }),
      });
    }

    return options;
  }, [selectedLog, t]);
  const relatedLogs = useMemo(() => {
    if (!selectedLog || !selectedLogCategory || !selectedHistory) {
      return [];
    }

    return getFocusedAuditLogs(logs, selectedLog, selectedLogCategory, selectedHistory.mode);
  }, [logs, selectedHistory, selectedLog, selectedLogCategory]);
  const shouldShowLogSkeleton = isLoading && shouldQueryLogs;

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("logs.eyebrow")}</span>
          <h2>{t("logs.title")}</h2>
        </div>
        <div className="section-heading-actions">
          <p>{t("logs.description")}</p>
          {activeUser.role === "SuperAdmin" ? (
            <SlidingSegmentedControl
              ariaLabel={language === "tr" ? "Log kaynağı" : "Log source"}
              name="audit-source"
              onChange={changeAuditMode}
              options={[
                { value: "active", label: language === "tr" ? "Aktif" : "Active" },
                { value: "archive", label: language === "tr" ? "Arşiv" : "Archive" },
              ]}
              value={auditMode}
            />
          ) : null}
          <button
            className="secondary-button refresh-button"
            disabled={isRefreshing}
            type="button"
            onClick={refreshLogs}
          >
            <RefreshCw className={isRefreshing ? "spin-icon" : undefined} size={17} />
            {isRefreshing ? t("common.refreshing") : t("common.refresh")}
          </button>
        </div>
      </div>

      <section className="identity-section">
        {isArchiveMode ? (
          <div className="audit-archive-context">
            <label className="filter-select-field compact-filter-field">
              <Archive size={16} />
              <select
                disabled={isArchiveListLoading || !archives.length}
                onChange={(event) => {
                  setSelectedArchiveId(event.target.value);
                  setPage(1);
                  setSelectedHistory(null);
                  setCategoryCounts(emptyAuditCounts());
                }}
                value={selectedArchiveId}
              >
                {!archives.length ? <option value="">Silinen topluluk arşivi yok</option> : null}
                {archives.map((archive) => (
                  <option key={archive.id} value={archive.id}>
                    {archive.communityName} · {formatSessionExpiry(archive.deletedAt, language)}
                  </option>
                ))}
              </select>
            </label>
            {selectedArchive ? (
              <div className="audit-archive-summary">
                <strong>{selectedArchive.communityName}</strong>
                <span>{selectedArchive.userCount} kullanıcı</span>
                <span>{selectedArchive.processCount} süreç</span>
                <span>{selectedArchive.taskCount} iş</span>
                <small>{selectedArchive.deletedByDisplayName} tarafından silindi</small>
              </div>
            ) : null}
          </div>
        ) : null}
        <div className="audit-category-grid">
          {auditCategories.map((category) => {
            const CategoryIcon = auditCategoryIcons[category];
            return (
              <button
                className={`audit-category-card ${selectedCategory === category ? "is-active" : ""}`}
                key={category}
                type="button"
                onClick={() => {
                  setSelectedCategory(category);
                  setPage(1);
                  setSelectedHistory(null);
                }}
              >
                <span className="audit-category-title">
                  <CategoryIcon aria-hidden="true" size={17} />
                  <span>{t(`logs.category.${category}` as TranslationKey)}</span>
                </span>
                {categoryCounts[category] === null ? (
                  <span className="metric-inline-loader audit-count-loader" aria-label={t("common.loading")}>
                    <span className="button-spinner" aria-hidden="true" />
                  </span>
                ) : (
                  <strong>{categoryCounts[category]}</strong>
                )}
                <small>{t(`logs.categoryHelp.${category}` as TranslationKey)}</small>
              </button>
            );
          })}
        </div>
        <label className="search-field">
          <Search size={16} />
          <input
            value={searchQuery}
            onChange={(event) => {
              setSearchQuery(event.target.value);
              setPage(1);
            }}
            placeholder={t("logs.searchPlaceholder")}
          />
        </label>
        <div className="audit-sort-controls">
          <label className="filter-select-field compact-filter-field">
            <ArrowUpDown size={16} />
            <select value={sortBy} onChange={(event) => { setSortBy(event.target.value as "createdAt" | "action" | "actor"); setPage(1); }}>
              <option value="createdAt">{t("logs.sort.createdAt")}</option>
              <option value="action">{t("logs.sort.action")}</option>
              <option value="actor">{t("logs.sort.actor")}</option>
            </select>
          </label>
          <button className="icon-button" type="button" onClick={() => { setSortDirection((value) => value === "asc" ? "desc" : "asc"); setPage(1); }} title={t("logs.sort.direction")}>{sortDirection === "asc" ? <ArrowUp size={17} /> : <ArrowDown size={17} />}</button>
        </div>
        {trimmedQuery.length < 2 && selectedCategory === "all" ? (
          <p className="helper-copy">{t("logs.searchFirst")}</p>
        ) : null}
        {trimmedQuery.length >= 2 || selectedCategory !== "all" ? (
          <p className="helper-copy">
            {t("logs.resultSummary", {
              count: resultSummaryCount,
              category: t(`logs.category.${selectedCategory}` as TranslationKey),
            })}
          </p>
        ) : null}
      </section>

      <div className="management-layout">
        <section className="identity-section">
          <div className="system-audit-list">
            {shouldShowLogSkeleton ? <SystemAuditSkeleton /> : null}
            {!shouldShowLogSkeleton ? visibleLogs.map((log) => (
              <article className="settings-row system-audit-row" key={log.id}>
                <div className="system-audit-content">
                  <div className="audit-label-row">
                    <span className={`audit-category-pill audit-category-${getAuditCategory(log)}`}>
                      {t(`logs.category.${getAuditCategory(log)}` as TranslationKey)}
                    </span>
                    <span>{formatAuditAction(log.action, language)}</span>
                  </div>
                  <strong>{log.description}</strong>
                  <small>
                    {log.actorDisplayName} / {log.actorUsername}
                  </small>
                </div>
                <div className="system-audit-meta">
                  <strong>{log.entityType}</strong>
                  <small>{formatSessionExpiry(log.createdAt, language)}</small>
                  <button
                    className={`secondary-button context-button ${selectedHistory?.logId === log.id ? "is-active" : ""}`}
                    type="button"
                    onClick={() => setSelectedHistory({ logId: log.id, mode: "related" })}
                  >
                    {t("logs.viewRelated")}
                  </button>
                </div>
              </article>
            )) : null}
            {trimmedQuery.length >= 2 && !visibleLogs.length && !shouldShowLogSkeleton ? (
              <p className="status-line">{t("logs.empty")}</p>
            ) : null}
          </div>
          <PaginationControls
            currentPage={currentPage}
            language={language}
            onNext={() => setPage((value) => Math.min(value + 1, totalPages))}
            onPageChange={setPage}
            onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
            totalPages={totalPages}
          />
        </section>

        <section className="identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("logs.relatedEyebrow")}</span>
              <h3>{selectedHistoryTitle}</h3>
            </div>
            <History size={22} />
          </div>
          {selectedLog && selectedHistory ? (
            <SlidingSegmentedControl
              ariaLabel={t("logs.historyFilterLabel")}
              name={`audit-history-${selectedLog.id}`}
              onChange={(mode) => setSelectedHistory({ logId: selectedLog.id, mode })}
              options={historyFilterOptions.map((option) => ({ value: option.mode, label: option.label }))}
              value={selectedHistory.mode}
            />
          ) : null}
          <SystemAuditTimeline
            key={selectedHistory ? `${selectedHistory.logId}-${selectedHistory.mode}` : "no-related-log"}
            logs={relatedLogs}
            language={language}
            emptyText={t("logs.noRelated")}
            searchable
          />
        </section>
      </div>
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}

function SystemAuditSkeleton() {
  return <>{Array.from({ length: 5 }, (_, index) => <article className="settings-row system-audit-row system-audit-skeleton" key={index}><div className="system-audit-content"><SkeletonBlock /><SkeletonBlock /><SkeletonBlock /></div><div className="system-audit-meta"><SkeletonBlock /><SkeletonBlock /></div></article>)}</>;
}

function waitForMinimumDelay(startedAt: number, minimumDelayMs: number) {
  const remainingMs = minimumDelayMs - (Date.now() - startedAt);
  return remainingMs > 0 ? new Promise((resolve) => window.setTimeout(resolve, remainingMs)) : Promise.resolve();
}

function emptyAuditCounts(): Record<AuditCategory, null> {
  return {
    all: null,
    identity: null,
    access: null,
    forms: null,
    processes: null,
    tasks: null,
  };
}

function archivedEventToSystemLog(
  event: ArchivedAuditEvent,
  archive: CommunityDeletionArchive | null,
): SystemAuditLog {
  return {
    id: event.id,
    actorUserId: event.actorUserId,
    communityId: archive?.originalCommunityId,
    actorDisplayName: event.actorDisplayName,
    actorUsername: event.actorUsername,
    category: event.category,
    action: event.action,
    entityType: event.entityType,
    entityId: event.entityId,
    description: event.description,
    createdAt: event.occurredAt,
    entityDisplayName: event.entityDisplayName,
    entityUsername: event.entityUsername,
  };
}
