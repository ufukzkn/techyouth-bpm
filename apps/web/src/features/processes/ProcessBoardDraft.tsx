"use client";

import { CheckCircle2, CircleDot, XCircle } from "lucide-react";
import { useMemo, useState } from "react";
import type { Role } from "@/lib/types";

type ProcessStatus = "InProgress" | "Completed" | "Rejected";
type TaskStatus = "Open" | "Completed";

type ProcessTask = {
  id: string;
  title: string;
  assignedRole: Role;
  status: TaskStatus;
};

type ProcessDraft = {
  id: string;
  formName: string;
  status: ProcessStatus;
  startedAt: string;
  formData: Record<string, string | boolean>;
  tasks: ProcessTask[];
  audit: string[];
};

const initialProcess: ProcessDraft = {
  id: "PRC-2026-0001",
  formName: "Demo Surec Formu",
  status: "InProgress",
  startedAt: "2026-06-21 02:45",
  formData: {
    customerName: "Eczacibasi Demo",
    requestType: "Satinalma",
    approvalNote: "Satinalma talebi icin onay bekleniyor.",
  },
  tasks: [
    {
      id: "TSK-001",
      title: "Talep onayi",
      assignedRole: "Approver",
      status: "Open",
    },
  ],
  audit: ["Process started: Pending -> InProgress"],
};

type ProcessBoardDraftProps = {
  role: Role;
};

export function ProcessBoardDraft({ role }: ProcessBoardDraftProps) {
  const [process, setProcess] = useState<ProcessDraft>(initialProcess);

  const openTasks = useMemo(
    () => process.tasks.filter((task) => task.status === "Open" && (task.assignedRole === role || role === "Admin")),
    [process.tasks, role],
  );

  function completeTask(action: "Approve" | "Reject") {
    setProcess((current) => {
      const nextStatus: ProcessStatus = action === "Approve" ? "Completed" : "Rejected";
      return {
        ...current,
        status: nextStatus,
        tasks: current.tasks.map((task) => (task.status === "Open" ? { ...task, status: "Completed" } : task)),
        audit: [...current.audit, `${action}: InProgress -> ${nextStatus}`],
      };
    });
  }

  return (
    <section className="process-section" id="processes">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Surecler / Islerim</span>
          <h2>Task odakli surec takibi</h2>
        </div>
        <p>Bu taslak, backend state machine akisini UI tarafinda okunabilir bir surec kartina cevirir.</p>
      </div>

      <div className="process-grid">
        <article className="process-card">
          <div className="process-card-header">
            <div>
              <span className="eyebrow">Surec ID</span>
              <strong>{process.id}</strong>
            </div>
            <StatusBadge status={process.status} />
          </div>
          <dl className="detail-list">
            <div>
              <dt>Form</dt>
              <dd>{process.formName}</dd>
            </div>
            <div>
              <dt>Baslangic</dt>
              <dd>{process.startedAt}</dd>
            </div>
          </dl>
          <pre className="json-preview compact-json">{JSON.stringify(process.formData, null, 2)}</pre>
        </article>

        <article className="process-card" id="tasks">
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
                    <strong>{task.title}</strong>
                    <span>
                      {task.id} · {task.assignedRole}
                    </span>
                  </div>
                  <div className="task-actions">
                    <button className="success-button" onClick={() => completeTask("Approve")}>
                      <CheckCircle2 size={17} />
                      Onayla
                    </button>
                    <button className="danger-button" onClick={() => completeTask("Reject")}>
                      <XCircle size={17} />
                      Reddet
                    </button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="empty-state">Bu rol icin acik is bulunmuyor.</p>
          )}
        </article>

        <article className="process-card audit-card">
          <span className="eyebrow">Audit Log</span>
          <ol>
            {process.audit.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ol>
        </article>
      </div>
    </section>
  );
}

function StatusBadge({ status }: { status: ProcessStatus }) {
  return <span className={`status-badge status-${status.toLowerCase()}`}>{status}</span>;
}
