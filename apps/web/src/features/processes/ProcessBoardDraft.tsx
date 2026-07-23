"use client";

import { RefreshCw } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { MyTasksView } from "@/features/processes/MyTasksView";
import { ProcessDetailPanel } from "@/features/processes/ProcessDetailPanel";
import { ProcessListView } from "@/features/processes/ProcessListView";
import {
  createProcessCacheKey,
  invalidateProcessCaches,
  processDetailCache,
  processPageCache,
  taskPageCache,
} from "@/features/processes/processBoardCache";
import { getAvailableWorkflowScopes, resolveWorkflowScope } from "@/features/processes/workflowVisibility";
import { useSessionStore } from "@/features/session/sessionStore";
import { SlidingSegmentedControl } from "@/features/ui/SlidingSegmentedControl";
import { actionLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { api, ApiError } from "@/lib/api";
import type {
  PagedResult,
  ProcessDetail,
  ProcessListParams,
  ProcessStatus,
  ProcessSummary,
  ProcessTask,
  TaskListParams,
  TaskPriority,
  WorkflowVisibilityScope,
  WorkflowAction,
} from "@/lib/types";

type ProcessBoardDraftProps = {
  mode: "processes" | "tasks";
};

type BoardStatus = "loading" | "refreshing" | "idle" | "acting" | "error";

const pageSize = 10;
const minimumRefreshDelayMs = 500;
const emptyProcessItems: ProcessSummary[] = [];
export function ProcessBoardDraft({ mode }: ProcessBoardDraftProps) {
  const token = useSessionStore((state) => state.token);
  const activeUser = useSessionStore((state) => state.user);
  const activeUserId = activeUser?.id ?? "";
  const language = useSessionStore((state) => state.language);
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [processResult, setProcessResult] = useState<PagedResult<ProcessSummary> | null>(null);
  const [taskResult, setTaskResult] = useState<PagedResult<ProcessTask> | null>(null);
  const [processPage, setProcessPage] = useState(1);
  const [taskPage, setTaskPage] = useState(1);
  const requestedStatus = searchParams.get("status");
  const initialProcessStatus = isProcessStatus(requestedStatus) ? requestedStatus : "all";
  const requestedTaskView = searchParams.get("view");
  const urlTaskView: NonNullable<TaskListParams["view"]> = requestedTaskView === "history" ? "history" : "active";
  const [processStatus, setProcessStatus] = useState<ProcessStatus | "all">(initialProcessStatus);
  const [taskView, setTaskView] = useState<NonNullable<TaskListParams["view"]>>(urlTaskView);
  const [processSortBy, setProcessSortBy] = useState<NonNullable<ProcessListParams["sortBy"]>>("startedAt");
  const [processSortDirection, setProcessSortDirection] = useState<"asc" | "desc">("desc");
  const [taskPriority, setTaskPriority] = useState<TaskPriority | "all">("all");
  const [taskSortBy, setTaskSortBy] = useState<NonNullable<TaskListParams["sortBy"]>>("dueAt");
  const [taskSortDirection, setTaskSortDirection] = useState<"asc" | "desc">("asc");
  const [selectedProcessId, setSelectedProcessId] = useState("");
  const [selectedTaskId, setSelectedTaskId] = useState("");
  const [detail, setDetail] = useState<ProcessDetail | null>(null);
  const [status, setStatus] = useState<BoardStatus>("loading");
  const [taskListState, setTaskListState] = useState<"loading" | "refreshing" | "idle" | "error">("loading");
  const [taskListError, setTaskListError] = useState<string | null>(null);
  const [hasTaskTargetResult, setHasTaskTargetResult] = useState(false);
  const [message, setMessage] = useState(() => t("process.loading"));
  const [isManualRefreshing, setIsManualRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const latestRequestRef = useRef(0);

  const availableScopes = activeUser ? getAvailableWorkflowScopes(activeUser) : ["personal"] as WorkflowVisibilityScope[];
  const processScope = resolveWorkflowScope(searchParams.get("scope"), availableScopes);

  const processParams: ProcessListParams = {
    page: processPage,
    pageSize,
    status: processStatus,
    scope: processScope,
    sortBy: processSortBy,
    sortDirection: processSortDirection,
  };
  const taskParams: TaskListParams = {
    page: taskPage,
    pageSize,
    priority: taskPriority,
    sortBy: taskSortBy,
    sortDirection: taskSortDirection,
    view: taskView,
  };
  const processCacheKey = createProcessCacheKey(activeUserId, processParams);
  const taskCacheKey = createProcessCacheKey(activeUserId, taskParams);
  const requestedProcessId = searchParams.get("processId") ?? "";
  const requestedTaskId = searchParams.get("taskId") ?? "";

  function prepareProcessQueryTransition(overrides: Partial<ProcessListParams>) {
    const nextParams = { ...processParams, ...overrides };
    const cached = processPageCache.get(createProcessCacheKey(activeUserId, nextParams));
    setProcessResult(cached ?? null);
    setStatus(cached ? "refreshing" : "loading");
    setMessage(t(cached ? "process.refreshing" : "process.loading"));
  }

  function prepareTaskQueryTransition(overrides: Partial<TaskListParams>) {
    latestRequestRef.current += 1;
    setIsManualRefreshing(false);
    const nextParams = { ...taskParams, ...overrides };
    const cached = taskPageCache.get(createProcessCacheKey(activeUserId, nextParams));
    if (cached) setTaskResult(cached);
    setHasTaskTargetResult(Boolean(cached));
    setTaskListState(cached ? "refreshing" : "loading");
    setTaskListError(null);
    setStatus(cached ? "refreshing" : "loading");
    setMessage(t(cached ? "process.refreshing" : "process.loading"));
  }

  function changeProcessScope(nextScope: WorkflowVisibilityScope) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("scope", nextScope);
    params.delete("processId");
    prepareProcessQueryTransition({ page: 1, scope: nextScope });
    setProcessPage(1);
    setSelectedProcessId("");
    setDetail(null);
    router.replace(`/processes?${params.toString()}`);
  }

  function changeProcessPage(nextPage: number) {
    const boundedPage = Math.max(1, Math.min(totalPages, nextPage));
    prepareProcessQueryTransition({ page: boundedPage });
    setProcessPage(boundedPage);
  }

  function changeTaskPage(nextPage: number) {
    const boundedPage = Math.max(1, Math.min(totalPages, nextPage));
    prepareTaskQueryTransition({ page: boundedPage });
    setTaskPage(boundedPage);
  }

  function changeTaskView(nextView: NonNullable<TaskListParams["view"]>) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("view", nextView);
    params.delete("taskId");
    prepareTaskQueryTransition({ page: 1, view: nextView });
    setTaskView(nextView);
    setTaskPage(1);
    setSelectedTaskId("");
    setDetail(null);
    window.history.replaceState(window.history.state, "", `/tasks?${params.toString()}`);
  }

  function changeProcessStatus(nextStatus: ProcessStatus | "all") {
    const params = new URLSearchParams(searchParams.toString());
    if (nextStatus === "all") params.delete("status");
    else params.set("status", nextStatus);
    params.delete("processId");
    prepareProcessQueryTransition({ page: 1, status: nextStatus });
    setProcessStatus(nextStatus);
    setProcessPage(1);
    setSelectedProcessId("");
    setDetail(null);
    router.replace(`/processes${params.size ? `?${params.toString()}` : ""}`);
  }

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function loadProcessDetail(processId: string, force = false) {
    if (!token || !processId) {
      setDetail(null);
      return;
    }
    const cached = processDetailCache.get(processId);
    if (cached && !force) {
      setDetail(cached);
      return;
    }
    const loaded = await api.getProcess(token, processId);
    processDetailCache.set(processId, loaded);
    setDetail(loaded);
  }

  async function refreshData(options: { manual?: boolean; force?: boolean; startedAt?: number } = {}) {
    const requestId = ++latestRequestRef.current;
    const isLatestRequest = () => latestRequestRef.current === requestId;
    if (!token) {
      if (!isLatestRequest()) return;
      setStatus("error");
      setMessage(t("process.sessionRequired"));
      setToast({ kind: "error", text: t("process.toastSessionRequired") });
      return;
    }

    const refreshStartedAt = options.startedAt ?? 0;
    const isManual = options.manual === true;
    const activeCache = mode === "processes"
      ? processPageCache.get(processCacheKey)
      : taskPageCache.get(taskCacheKey);
    if (activeCache) {
      if (mode === "processes") setProcessResult(activeCache as PagedResult<ProcessSummary>);
      else setTaskResult(activeCache as PagedResult<ProcessTask>);
    }
    if (mode === "tasks") {
      const hasCachedResult = Boolean(activeCache);
      setHasTaskTargetResult(hasCachedResult);
      setTaskListState(hasCachedResult ? "refreshing" : "loading");
      setTaskListError(null);
    }
    setStatus(activeCache ? "refreshing" : "loading");
    setMessage(t(activeCache ? "process.refreshing" : "process.loading"));
    if (isManual) setIsManualRefreshing(true);
    else setIsManualRefreshing(false);

    try {
      if (mode === "processes") {
        const result = await api.listProcesses(token, processParams);
        if (!isLatestRequest()) return;
        processPageCache.set(processCacheKey, result);
        setProcessResult(result);
        if (requestedProcessId || selectedProcessId) {
          const targetId = requestedProcessId || selectedProcessId;
          setSelectedProcessId(targetId);
          await loadProcessDetail(targetId, options.force);
        } else {
          setDetail(null);
        }
        setMessage(result.totalCount > 0 ? t("process.loaded") : t("process.empty"));
      } else {
        const [result, requestedTaskResult] = await Promise.all([
          api.listMyTasks(token, taskParams),
          requestedTaskId
            ? api.listMyTasks(token, { page: 1, pageSize: 1, taskId: requestedTaskId, view: taskView })
            : Promise.resolve(null),
        ]);
        if (!isLatestRequest()) return;
        taskPageCache.set(taskCacheKey, result);
        setTaskResult(result);
        setHasTaskTargetResult(true);
        setTaskListState("idle");
        const targetTask = requestedTaskResult?.items[0]
          ?? result.items.find((task) => task.id === selectedTaskId)
          ?? null;
        if (targetTask) {
          setSelectedTaskId(targetTask.id);
          setSelectedProcessId(targetTask.processInstanceId);
          await loadProcessDetail(targetTask.processInstanceId, options.force);
        } else if (!selectedTaskId) {
          setDetail(null);
        }
        setMessage(result.totalCount > 0 ? t("process.loaded") : t("process.empty"));
      }

      if (!isLatestRequest()) return;
      setStatus("idle");
      if (isManual) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setToast({ kind: "success", text: t("process.toastRefreshed") });
      }
    } catch (error) {
      if (!isLatestRequest()) return;
      const errorMessage = error instanceof ApiError ? error.errors.join(" ") : t("process.loadFailed");
      if (mode === "tasks") {
        setTaskListError(errorMessage);
        setTaskListState("error");
      }
      setStatus("error");
      setMessage(errorMessage);
      setToast({
        kind: "error",
        text: error instanceof ApiError ? t("process.toastRefreshFailed") : t("process.toastUnexpectedRefreshFailed"),
      });
    } finally {
      if (!isLatestRequest()) return;
      if (isManual) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setIsManualRefreshing(false);
      }
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => void refreshData(), 0);
    return () => window.clearTimeout(timer);
    // Current route parameters intentionally define the server-side page cache key.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token, language, mode, processCacheKey, taskCacheKey, requestedProcessId, requestedTaskId]);

  async function selectProcess(id: string) {
    setSelectedProcessId(id);
    setSelectedTaskId("");
    try {
      setStatus("refreshing");
      await loadProcessDetail(id);
      setStatus("idle");
      setMessage(t("process.detailLoaded"));
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.detailFailed"));
    }
  }

  async function selectTask(task: ProcessTask) {
    setSelectedTaskId(task.id);
    setSelectedProcessId(task.processInstanceId);
    try {
      setStatus("refreshing");
      await loadProcessDetail(task.processInstanceId);
      setStatus("idle");
      setMessage(t("process.detailLoaded"));
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.detailFailed"));
    }
  }

  async function loadTaskForAction(taskId: string) {
    if (!token) return null;
    try {
      return await api.getTask(token, taskId);
    } catch (error) {
      setToast({ kind: "error", text: error instanceof ApiError ? error.errors.join(" ") : t("process.detailFailed") });
      return null;
    }
  }

  async function executeTask(
    taskId: string,
    action: Exclude<WorkflowAction, "Start">,
    note: string,
    formData?: Record<string, unknown>,
  ) {
    if (!token) return false;
    try {
      setStatus("acting");
      const updated = await api.executeTaskAction(token, taskId, { action, note, formData });
      invalidateProcessCaches(updated.id);
      processDetailCache.set(updated.id, updated);
      setDetail(updated);
      await refreshData({ force: true });
      setMessage(t("process.actionSaved", { action: actionLabel(language, action) }));
      setToast({ kind: "success", text: t("process.toastActionSaved") });
      return true;
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.taskActionFailed"));
      setToast({ kind: "error", text: t("process.taskActionFailed") });
      return false;
    }
  }

  async function updateTaskClaim(taskId: string, claimMode: "claim" | "release", claimVersion?: string | null) {
    if (!token) return;
    try {
      setStatus("acting");
      if (claimMode === "claim") await api.claimTask(token, taskId, { claimVersion });
      else await api.releaseTask(token, taskId, { claimVersion });
      taskPageCache.clear();
      if (selectedProcessId) processDetailCache.delete(selectedProcessId);
      await refreshData({ force: true });
      setToast({ kind: "success", text: t(claimMode === "claim" ? "process.claimed" : "process.released") });
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.claimFailed"));
      setToast({ kind: "error", text: t("process.claimFailed") });
    }
  }

  function refreshManually() {
    void refreshData({ manual: true, force: true, startedAt: Date.now() });
  }

  const activeResult = mode === "processes" ? processResult : taskResult;
  const isInitialLoading = mode === "processes" && status === "loading" && !activeResult;
  const isListLoading = mode === "tasks"
    ? taskListState === "loading" || taskListState === "refreshing"
    : status === "loading" || status === "refreshing";
  const totalPages = Math.max(1, Math.ceil((activeResult?.totalCount ?? 0) / pageSize));
  const visibleProcessResult = processResult ?? { items: emptyProcessItems, page: processPage, pageSize, totalCount: 0 };
  const visibleTaskResult = taskResult ?? { items: [], page: taskPage, pageSize, totalCount: 0 };

  return (
    <section className="process-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{mode === "tasks" ? t("process.tasksEyebrow") : t("process.processesEyebrow")}</span>
          <h2>{mode === "tasks" ? t("process.tasksTitle") : t("process.processesTitle")}</h2>
        </div>
        <p>{t("process.description")}</p>
      </div>

      {mode === "processes" && availableScopes.length > 1 ? (
        <div className="workflow-scope-toolbar process-scope-toolbar">
          <div>
            <span className="eyebrow">{t("workflowScope.eyebrow")}</span>
            <strong>{t(`workflowScope.${processScope}` as TranslationKey)}</strong>
            <small>{t(`workflowScope.${processScope}Description` as TranslationKey)}</small>
          </div>
          <SlidingSegmentedControl
            ariaLabel={t("workflowScope.ariaLabel")}
            name="process-workflow-scope"
            onChange={changeProcessScope}
            options={availableScopes.map((item) => ({ value: item, label: t(`workflowScope.${item}` as TranslationKey) }))}
            value={processScope}
          />
        </div>
      ) : null}

      <div className="section-toolbar">
        <p className={`status-line status-line-${status}`} aria-live="polite">
          {isListLoading ? (
            <>
              <InlineValueLoader label={t(status === "refreshing" ? "process.refreshing" : "process.loading")} />
              <span>{t(status === "refreshing" ? "process.refreshing" : "process.loading")}</span>
            </>
          ) : mode === "tasks" && taskListError ? null : message}
        </p>
        <button
          className="secondary-button refresh-button"
          disabled={isInitialLoading || isManualRefreshing || status === "acting"}
          type="button"
          onClick={refreshManually}
        >
          <RefreshCw className={isManualRefreshing ? "spin-icon" : undefined} size={17} />
          {isManualRefreshing ? t("common.refreshing") : t("common.refresh")}
        </button>
      </div>

      <div className={status === "refreshing" ? "process-grid is-refreshing" : "process-grid"}>
        {mode === "tasks" && isInitialLoading ? <ProcessBoardSkeleton mode={mode} /> : (
          <>
            {mode === "processes" ? (
              <ProcessListView
                cacheScope={`${activeUserId}:${processScope}:${processPage}:${processStatus}:${processSortBy}:${processSortDirection}`}
                language={language}
                onNextPage={() => changeProcessPage(processPage + 1)}
                onPageChange={changeProcessPage}
                onPreviousPage={() => changeProcessPage(processPage - 1)}
                onSelectProcess={(id) => void selectProcess(id)}
                onSortByChange={(value) => { prepareProcessQueryTransition({ page: 1, sortBy: value }); setProcessSortBy(value); setProcessPage(1); }}
                onSortDirectionChange={(value) => { prepareProcessQueryTransition({ page: 1, sortDirection: value }); setProcessSortDirection(value); setProcessPage(1); }}
                onStatusChange={changeProcessStatus}
                result={visibleProcessResult}
                selectedProcessId={selectedProcessId}
                showListSkeleton={isInitialLoading}
                sortBy={processSortBy}
                sortDirection={processSortDirection}
                statusFilter={processStatus}
              />
            ) : null}

            {mode === "tasks" ? (
              <MyTasksView
                activeUserId={activeUserId}
                language={language}
                onClaimTask={(taskId, claimVersion) => void updateTaskClaim(taskId, "claim", claimVersion)}
                onExecuteTask={executeTask}
                onLoadTaskDetail={loadTaskForAction}
                onNextPage={() => changeTaskPage(taskPage + 1)}
                onPageChange={changeTaskPage}
                onPreviousPage={() => changeTaskPage(taskPage - 1)}
                onPriorityChange={(value) => { prepareTaskQueryTransition({ page: 1, priority: value }); setTaskPriority(value); setTaskPage(1); }}
                onReleaseTask={(taskId, claimVersion) => void updateTaskClaim(taskId, "release", claimVersion)}
                onSelectTask={(task) => void selectTask(task)}
                onSortByChange={(value) => {
                  const direction = value === "newest" || value === "priority" ? "desc" : "asc";
                  prepareTaskQueryTransition({ page: 1, sortBy: value, sortDirection: direction });
                  setTaskSortBy(value);
                  setTaskSortDirection(direction);
                  setTaskPage(1);
                }}
                onSortDirectionChange={(value) => { prepareTaskQueryTransition({ page: 1, sortDirection: value }); setTaskSortDirection(value); setTaskPage(1); }}
                priorityFilter={taskPriority}
                result={visibleTaskResult}
                selectedTaskId={selectedTaskId}
                sortBy={taskSortBy}
                sortDirection={taskSortDirection}
                status={status}
                isListLoading={isListLoading}
                listError={taskListError}
                showListSkeleton={!hasTaskTargetResult && taskListState === "loading"}
                onRetry={() => void refreshData({ force: true })}
                taskView={taskView}
                onTaskViewChange={changeTaskView}
              />
            ) : null}

            <ProcessDetailPanel detail={detail} language={language} />
          </>
        )}
      </div>

      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}

function isProcessStatus(value: string | null): value is ProcessStatus {
  return value === "Pending"
    || value === "InProgress"
    || value === "Completed"
    || value === "Rejected"
    || value === "Escalated";
}

function waitForMinimumDelay(startedAt: number, minimumDelayMs: number) {
  const remainingMs = minimumDelayMs - (Date.now() - startedAt);
  return remainingMs > 0 ? new Promise((resolve) => window.setTimeout(resolve, remainingMs)) : Promise.resolve();
}

function ProcessBoardSkeleton({ mode }: { mode: "processes" | "tasks" }) {
  const language = useSessionStore((state) => state.language);
  const label = translate(language, mode === "tasks" ? "process.skeletonTasks" : "process.skeletonProcesses");
  return (
    <>
      <article className="process-card process-skeleton" aria-label={label}><span /><span /><span /><span /></article>
      <article className="process-card process-skeleton" aria-label={translate(language, "process.skeletonDetail")}><span /><span /><span /><span /></article>
    </>
  );
}
