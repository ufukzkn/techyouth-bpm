"use client";

import { RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import { MyTasksView } from "@/features/processes/MyTasksView";
import { ProcessDetailPanel } from "@/features/processes/ProcessDetailPanel";
import { ProcessListView } from "@/features/processes/ProcessListView";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { ProcessDetail, ProcessSummary, ProcessTask, Role, WorkflowAction } from "@/lib/types";

type ProcessBoardDraftProps = {
  mode: "processes" | "tasks";
  role: Role;
};

export function ProcessBoardDraft({ mode, role }: ProcessBoardDraftProps) {
  const token = useSessionStore((state) => state.token);
  const [processes, setProcesses] = useState<ProcessSummary[]>([]);
  const [tasks, setTasks] = useState<ProcessTask[]>([]);
  const [selectedProcessId, setSelectedProcessId] = useState("");
  const [detail, setDetail] = useState<ProcessDetail | null>(null);
  const [status, setStatus] = useState<"loading" | "idle" | "acting" | "error">("loading");
  const [message, setMessage] = useState("Surec verileri yukleniyor.");

  async function refreshData(nextSelectedProcessId = selectedProcessId) {
    if (!token) {
      setStatus("error");
      setMessage("Surecleri listelemek icin API oturumu gerekli.");
      return;
    }

    try {
      await Promise.resolve();
      setStatus("loading");
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
      setMessage(processResult.length > 0 ? "Surecler veritabanindan yuklendi." : "Henuz surec yok.");
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Surecler yuklenemedi.");
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refreshData("");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  async function selectProcess(id: string) {
    if (!token) {
      return;
    }

    try {
      setSelectedProcessId(id);
      setDetail(await api.getProcess(token, id));
      setStatus("idle");
      setMessage("Surec detayi yuklendi.");
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Surec detayi yuklenemedi.");
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
      setMessage(`${action === "Approve" ? "Onay" : "Red"} aksiyonu kaydedildi.`);
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Task aksiyonu tamamlanamadi.");
    }
  }

  return (
    <section className="process-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{mode === "tasks" ? "Islerim" : "Surecler"}</span>
          <h2>{mode === "tasks" ? "Bekleyen task aksiyonlari" : "Surec takibi"}</h2>
        </div>
        <p>Surecler, tasklar ve audit log backend state machine uzerinden veritabanina kaydedilir.</p>
      </div>

      <div className="section-toolbar">
        <p className={`status-line status-line-${status}`}>{message}</p>
        <button className="secondary-button" disabled={status === "loading"} type="button" onClick={() => refreshData()}>
          <RefreshCw size={17} />
          Yenile
        </button>
      </div>

      <div className="process-grid">
        {mode === "processes" ? (
          <ProcessListView
            processes={processes}
            selectedProcessId={selectedProcessId}
            onSelectProcess={selectProcess}
          />
        ) : null}

        {mode === "tasks" ? (
          <MyTasksView
            tasks={tasks}
            role={role}
            status={status}
            onExecuteTask={executeTask}
          />
        ) : null}

        <ProcessDetailPanel detail={detail} />
      </div>
    </section>
  );
}
