"use client";

import { CheckCircle2, CircleDot, XCircle } from "lucide-react";
import { useState } from "react";
import { roleLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { TaskActionDialog } from "@/features/processes/TaskActionDialog";
import type { Language, ProcessTask, Role, WorkflowAction } from "@/lib/types";

type MyTasksViewProps = {
  tasks: ProcessTask[];
  language: Language;
  role: Role;
  status: "loading" | "refreshing" | "idle" | "acting" | "error";
  onExecuteTask: (taskId: string, action: Exclude<WorkflowAction, "Start">, note: string) => void;
};

export function MyTasksView({ tasks, language, role, status, onExecuteTask }: MyTasksViewProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
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
            <span className="eyebrow">{t("process.myTasks")}</span>
            <strong>{t("process.openTaskCount", { count: openTasks.length })}</strong>
          </div>
          <CircleDot size={22} />
        </div>

        {openTasks.length > 0 ? (
          <div className="task-list">
            {openTasks.map((task) => (
              <div className="task-item" key={task.id}>
                <div>
                  <strong>{t("process.approvalTask")}</strong>
                  <span>
                    {task.id.slice(0, 8)} - {roleLabel(language, task.assignedRole)}
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
                    {t("process.approve")}
                  </button>
                  <button
                    className="danger-button"
                    disabled={status === "acting"}
                    onClick={() => setPendingAction({ taskId: task.id, action: "Reject" })}
                    type="button"
                  >
                    <XCircle size={17} />
                    {t("process.reject")}
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="empty-state">{t("process.noOpenTasks", { role: roleLabel(language, role) })}</p>
        )}
      </article>

      {pendingAction ? (
        <TaskActionDialog
          action={pendingAction.action}
          language={language}
          disabled={status === "acting"}
          onCancel={() => setPendingAction(null)}
          onConfirm={handleConfirm}
        />
      ) : null}
    </>
  );
}
