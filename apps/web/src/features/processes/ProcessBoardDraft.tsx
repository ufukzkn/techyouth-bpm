"use client";

import { RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
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
  const [processStatus, setProcessStatus] = useState<ProcessStatus | "all">("all");
  const [processSortBy, setProcessSortBy] = useState<NonNullable<ProcessListParams["sortBy"]>>("startedAt");
  const [processSortDirection, setProcessSortDirection] = useState<"asc" | "desc">("desc");
  const [taskPriority, setTaskPriority] = useState<TaskPriority | "all">("all");
  const [taskSortBy, setTaskSortBy] = useState<NonNullable<TaskListParams["sortBy"]>>("dueAt");
  const [taskSortDirection, setTaskSortDirection] = useState<"asc" | "desc">("asc");
  const [selectedProcessId, setSelectedProcessId] = useState("");
  const [selectedTaskId, setSelectedTaskId] = useState("");
  const [detail, setDetail] = useState<ProcessDetail | null>(null);
  const [status, setStatus] = useState<BoardStatus>("loading");
  const [message, setMessage] = useState(() => t("process.loading"));
  const [isManualRefreshing, setIsManualRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);

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
  };
  const processCacheKey = createProcessCacheKey(activeUserId, processParams);
  const taskCacheKey = createProcessCacheKey(activeUserId, taskParams);
  const requestedProcessId = searchParams.get("processId") ?? "";
  const requestedTaskId = searchParams.get("taskId") ?? "";

  function changeProcessScope(nextScope: WorkflowVisibilityScope) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("scope", nextScope);
    params.delete("processId");
    setProcessPage(1);
    setSelectedProcessId("");
    setDetail(null);
    router.replace(`/processes?${params.toString()}`);
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
    if (!token) {
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
    if (activeCache && !options.force) {
      if (mode === "processes") setProcessResult(activeCache as PagedResult<ProcessSummary>);
      else setTaskResult(activeCache as PagedResult<ProcessTask>);
    }
    setStatus(activeCache ? "refreshing" : "loading");
    if (isManual) setIsManualRefreshing(true);

    try {
      if (mode === "processes") {
        const result = await api.listProcesses(token, processParams);
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
            ? api.listMyTasks(token, { page: 1, pageSize: 1, taskId: requestedTaskId })
            : Promise.resolve(null),
        ]);
        taskPageCache.set(taskCacheKey, result);
        setTaskResult(result);
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

      setStatus("idle");
      if (isManual) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setToast({ kind: "success", text: t("process.toastRefreshed") });
      }
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.loadFailed"));
      setToast({
        kind: "error",
        text: error instanceof ApiError ? t("process.toastRefreshFailed") : t("process.toastUnexpectedRefreshFailed"),
      });
    } finally {
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
  const isInitialLoading = status === "loading" && !activeResult;
  const totalPages = Math.max(1, Math.ceil((activeResult?.totalCount ?? 0) / pageSize));

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
        <p className={`status-line status-line-${status}`} aria-live="polite">{message}</p>
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
        {isInitialLoading ? <ProcessBoardSkeleton mode={mode} /> : (
          <>
            {mode === "processes" && processResult ? (
              <ProcessListView
                cacheScope={`${activeUserId}:${processScope}:${processPage}:${processStatus}:${processSortBy}:${processSortDirection}`}
                language={language}
                onNextPage={() => setProcessPage((value) => Math.min(totalPages, value + 1))}
                onPageChange={setProcessPage}
                onPreviousPage={() => setProcessPage((value) => Math.max(1, value - 1))}
                onSelectProcess={(id) => void selectProcess(id)}
                onSortByChange={(value) => { setProcessSortBy(value); setProcessPage(1); }}
                onSortDirectionChange={(value) => { setProcessSortDirection(value); setProcessPage(1); }}
                onStatusChange={(value) => { setProcessStatus(value); setProcessPage(1); }}
                result={processResult}
                selectedProcessId={selectedProcessId}
                sortBy={processSortBy}
                sortDirection={processSortDirection}
                statusFilter={processStatus}
              />
            ) : null}

            {mode === "tasks" && taskResult ? (
              <MyTasksView
                activeUserId={activeUserId}
                language={language}
                onClaimTask={(taskId, claimVersion) => void updateTaskClaim(taskId, "claim", claimVersion)}
                onExecuteTask={executeTask}
                onNextPage={() => setTaskPage((value) => Math.min(totalPages, value + 1))}
                onPageChange={setTaskPage}
                onPreviousPage={() => setTaskPage((value) => Math.max(1, value - 1))}
                onPriorityChange={(value) => { setTaskPriority(value); setTaskPage(1); }}
                onReleaseTask={(taskId, claimVersion) => void updateTaskClaim(taskId, "release", claimVersion)}
                onSelectTask={(task) => void selectTask(task)}
                onSortByChange={(value) => { setTaskSortBy(value); setTaskPage(1); }}
                onSortDirectionChange={(value) => { setTaskSortDirection(value); setTaskPage(1); }}
                priorityFilter={taskPriority}
                result={taskResult}
                selectedTaskId={selectedTaskId}
                sortBy={taskSortBy}
                sortDirection={taskSortDirection}
                status={status}
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
