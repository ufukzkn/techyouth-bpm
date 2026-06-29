"use client";

import { CheckCircle2, CircleDot, XCircle } from "lucide-react";
import { useState } from "react";
import { TaskActionDialog } from "@/features/processes/TaskActionDialog";
import type { ProcessTask, Role, WorkflowAction } from "@/lib/types";

type MyTasksViewProps = {
  tasks: ProcessTask[];
  role: Role;
  status: "loading" | "idle" | "acting" | "error";
  onExecuteTask: (taskId: string, action: Exclude<WorkflowAction, "Start">, note: string) => void;
};

export function MyTasksView({ tasks, role, status, onExecuteTask }: MyTasksViewProps) {
  const openTasks = tasks.filter((task) => task.status === "Open");
  const [pendingAction, setPendingAction] = useState<{
    taskId: string;
    action: Exclude<WorkflowAction, "Start">;
  } | null>(null);

  function handleConfirm(note: string) {
    if (pendingAction) {
      onExecuteTask(pendingAction.taskId, pendingAction.action, note);
      setPendingAction(null);
    }
  }

  return (
    <>
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
                    {task.id.slice(0, 8)} — {task.assignedRole}
                  </span>
                  <small className="task-date">
                    {new Date(task.createdAt).toLocaleString("tr-TR")}
                  </small>
                </div>
                <div className="task-actions">
                  <button
                    className="success-button"
                    disabled={status === "acting"}
                    onClick={() => setPendingAction({ taskId: task.id, action: "Approve" })}
                    type="button"
                  >
                    <CheckCircle2 size={17} />
                    Onayla
                  </button>
                  <button
                    className="danger-button"
                    disabled={status === "acting"}
                    onClick={() => setPendingAction({ taskId: task.id, action: "Reject" })}
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

      {pendingAction ? (
        <TaskActionDialog
          action={pendingAction.action}
          disabled={status === "acting"}
          onCancel={() => setPendingAction(null)}
          onConfirm={handleConfirm}
        />
      ) : null}
    </>
  );
}
