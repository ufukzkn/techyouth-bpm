"use client";

import { CheckCircle2, CircleDot, RefreshCw, XCircle } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
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

  const openTasks = useMemo(() => tasks.filter((task) => task.status === "Open"), [tasks]);

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
      setMessage(processResult.length > 0 ? "Surecler SQLite veritabanindan yuklendi." : "Henuz surec yok.");
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

  async function executeTask(taskId: string, action: Exclude<WorkflowAction, "Start">) {
    if (!token) {
      return;
    }

    try {
      setStatus("acting");
      const updated = await api.executeTaskAction(token, taskId, {
        action,
        note: action === "Approve" ? "UI uzerinden onaylandi." : "UI uzerinden reddedildi.",
      });
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
        <p>Surecler, tasklar ve audit log backend state machine uzerinden SQLite veritabanina kaydedilir.</p>
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
          <article className="process-card">
            <div className="process-card-header">
              <div>
                <span className="eyebrow">Surec listesi</span>
                <strong>{processes.length} kayit</strong>
              </div>
              <CircleDot size={22} />
            </div>

            {processes.length === 0 ? (
              <p className="empty-state">Henuz baslatilmis surec yok.</p>
            ) : (
              <div className="process-list">
                {processes.map((process) => (
                  <button
                    className={process.id === selectedProcessId ? "process-list-item active" : "process-list-item"}
                    key={process.id}
                    onClick={() => selectProcess(process.id)}
                    type="button"
                  >
                    <span>
                      <strong>{process.formName}</strong>
                      <small>{new Date(process.startedAt).toLocaleString("tr-TR")}</small>
                    </span>
                    <StatusBadge status={process.status} />
                  </button>
                ))}
              </div>
            )}
          </article>
        ) : null}

        <article className="process-card">
          <div className="process-card-header">
            <div>
              <span className="eyebrow">Islerim</span>
              <strong>{openTasks.length} acik is</strong>
            </div>
            <CircleDot size={22} />
          </div>

          {openTasks.length > 0 ? (
            <div className="task-list">
              {openTasks.map((task) => (
                <div className="task-item" key={task.id}>
                  <div>
                    <strong>Surec onayi</strong>
                    <span>
                      {task.id.slice(0, 8)} - {task.assignedRole}
                    </span>
                  </div>
                  <div className="task-actions">
                    <button
                      className="success-button"
                      disabled={status === "acting"}
                      onClick={() => executeTask(task.id, "Approve")}
                      type="button"
                    >
                      <CheckCircle2 size={17} />
                      Onayla
                    </button>
                    <button
                      className="danger-button"
                      disabled={status === "acting"}
                      onClick={() => executeTask(task.id, "Reject")}
                      type="button"
                    >
                      <XCircle size={17} />
                      Reddet
                    </button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="empty-state">{role} rolu icin acik is bulunmuyor.</p>
          )}
        </article>

        <article className="process-card">
          <div className="process-card-header">
            <div>
              <span className="eyebrow">Surec detayi</span>
              <strong>{detail?.formName ?? "Secili surec yok"}</strong>
            </div>
            {detail ? <StatusBadge status={detail.status} /> : null}
          </div>
          {detail ? (
            <>
              <dl className="detail-list">
                <div>
                  <dt>Baslangic</dt>
                  <dd>{new Date(detail.startedAt).toLocaleString("tr-TR")}</dd>
                </div>
                <div>
                  <dt>Durum</dt>
                  <dd>{detail.status}</dd>
                </div>
              </dl>
              <pre className="json-preview compact-json">{JSON.stringify(detail.formData, null, 2)}</pre>
            </>
          ) : (
            <p className="empty-state">Detay goruntulemek icin bir surec sec.</p>
          )}
        </article>

        <article className="process-card audit-card">
          <span className="eyebrow">Audit Log</span>
          {detail && detail.auditLogs.length > 0 ? (
            <ol>
              {detail.auditLogs.map((item) => (
                <li key={item.id}>
                  {item.action}: {item.fromStatus} - {item.toStatus}
                </li>
              ))}
            </ol>
          ) : (
            <p className="empty-state">Audit kaydi yok.</p>
          )}
        </article>
      </div>
    </section>
  );
}

function StatusBadge({ status }: { status: ProcessSummary["status"] }) {
  return <span className={`status-badge status-${status.toLowerCase()}`}>{status}</span>;
}
