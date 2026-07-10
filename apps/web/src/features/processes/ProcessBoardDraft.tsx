"use client";

import { RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { MyTasksView } from "@/features/processes/MyTasksView";
import { ProcessDetailPanel } from "@/features/processes/ProcessDetailPanel";
import { ProcessListView } from "@/features/processes/ProcessListView";
import { useSessionStore } from "@/features/session/sessionStore";
import { actionLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { api, ApiError } from "@/lib/api";
import type { ProcessDetail, ProcessSummary, ProcessTask, WorkflowAction } from "@/lib/types";

type ProcessBoardDraftProps = {
  mode: "processes" | "tasks";
};

const minimumRefreshDelayMs = 500;

export function ProcessBoardDraft({ mode }: ProcessBoardDraftProps) {
  const token = useSessionStore((state) => state.token);
  const language = useSessionStore((state) => state.language);
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [processes, setProcesses] = useState<ProcessSummary[]>([]);
  const [tasks, setTasks] = useState<ProcessTask[]>([]);
  const [selectedProcessId, setSelectedProcessId] = useState("");
  const [detail, setDetail] = useState<ProcessDetail | null>(null);
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "acting" | "error">("loading");
  const [message, setMessage] = useState(() => t("process.loading"));
  const [isManualRefreshing, setIsManualRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);

  useEffect(() => {
    if (!toast) {
      return;
    }

    const timer = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function refreshData(nextSelectedProcessId = selectedProcessId, options: { manual?: boolean } = {}) {
    if (!token) {
      setStatus("error");
      setMessage(t("process.sessionRequired"));
      setToast({ kind: "error", text: t("process.toastSessionRequired") });
      return;
    }

    const isManualRefresh = options.manual === true;
    const refreshStartedAt = Date.now();

    try {
      await Promise.resolve();
      const hasVisibleData = processes.length > 0 || tasks.length > 0 || detail !== null;

      if (isManualRefresh) {
        setIsManualRefreshing(true);
        setStatus("refreshing");
        setMessage(t("process.refreshing"));
      } else if (!hasVisibleData) {
        setStatus("loading");
        setMessage(t("process.loading"));
      }

      const [processResult, taskResult] = await Promise.all([api.listProcesses(token), api.listMyTasks(token)]);
      const nextSelected = nextSelectedProcessId || processResult[0]?.id || "";

      setProcesses(processResult);
      setTasks(taskResult);
      setSelectedProcessId(nextSelected);

      if (nextSelected) {
        setDetail(await api.getProcess(token, nextSelected));
      } else {
        setDetail(null);
      }

      setStatus("idle");
      setMessage(processResult.length > 0 ? t("process.loaded") : t("process.empty"));
      if (isManualRefresh) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setIsManualRefreshing(false);
        setToast({ kind: "success", text: t("process.toastRefreshed") });
      }
    } catch (error) {
      if (isManualRefresh) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setIsManualRefreshing(false);
      }
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.loadFailed"));
      setToast({
        kind: "error",
        text: error instanceof ApiError ? t("process.toastRefreshFailed") : t("process.toastUnexpectedRefreshFailed"),
      });
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refreshData("");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token, language]);

  async function selectProcess(id: string) {
    if (!token) {
      return;
    }

    try {
      setSelectedProcessId(id);
      setDetail(await api.getProcess(token, id));
      setStatus("idle");
      setMessage(t("process.detailLoaded"));
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.detailFailed"));
    }
  }

  async function executeTask(taskId: string, action: Exclude<WorkflowAction, "Start">, note: string) {
    if (!token) {
      return;
    }

    try {
      setStatus("acting");
      const updated = await api.executeTaskAction(token, taskId, { action, note });
      setDetail(updated);
      await refreshData(updated.id);
      setMessage(t("process.actionSaved", { action: actionLabel(language, action) }));
      setToast({ kind: "success", text: t("process.toastActionSaved") });
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("process.taskActionFailed"));
      setToast({ kind: "error", text: t("process.taskActionFailed") });
    }
  }

  const isRefreshing = isManualRefreshing;
  const isInitialLoading = status === "loading" && processes.length === 0 && tasks.length === 0 && !detail;
  const isRefreshButtonDisabled = isInitialLoading || isManualRefreshing || status === "acting";

  return (
    <section className="process-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{mode === "tasks" ? t("process.tasksEyebrow") : t("process.processesEyebrow")}</span>
          <h2>{mode === "tasks" ? t("process.tasksTitle") : t("process.processesTitle")}</h2>
        </div>
        <p>{t("process.description")}</p>
      </div>

      <div className="section-toolbar">
        <p className={`status-line status-line-${status}`} aria-live="polite">
          {message}
        </p>
        <button
          className="secondary-button refresh-button"
          disabled={isRefreshButtonDisabled}
          type="button"
          onClick={() => refreshData(selectedProcessId, { manual: true })}
        >
          <RefreshCw className={isRefreshing ? "spin-icon" : undefined} size={17} />
          {isRefreshing ? t("common.refreshing") : t("common.refresh")}
        </button>
      </div>

      <div className={isRefreshing ? "process-grid is-refreshing" : "process-grid"}>
        {isInitialLoading ? (
          <ProcessBoardSkeleton mode={mode} />
        ) : (
          <>
            {mode === "processes" ? (
              <ProcessListView
                processes={processes}
                language={language}
                selectedProcessId={selectedProcessId}
                onSelectProcess={selectProcess}
              />
            ) : null}

            {mode === "tasks" ? (
              <MyTasksView
                tasks={tasks}
                language={language}
                status={status}
                onExecuteTask={executeTask}
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
      <article className="process-card process-skeleton" aria-label={label}>
        <span />
        <span />
        <span />
        <span />
      </article>
      <article className="process-card process-skeleton" aria-label={translate(language, "process.skeletonDetail")}>
        <span />
        <span />
        <span />
        <span />
      </article>
    </>
  );
}
