"use client";

import { ArrowUpRight, CheckCheck, CheckCircle2, CircleDot, Hand, Undo2, UserRoundCheck, XCircle } from "lucide-react";
import { useState } from "react";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { TaskActionDialog } from "@/features/processes/TaskActionDialog";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, ProcessTask, WorkflowAction } from "@/lib/types";

type MyTasksViewProps = {
  tasks: ProcessTask[];
  activeUserId: string;
  language: Language;
  status: "loading" | "refreshing" | "idle" | "acting" | "error";
  onClaimTask: (taskId: string, claimVersion?: string | null) => void;
  onReleaseTask: (taskId: string, claimVersion?: string | null) => void;
  onExecuteTask: (
    taskId: string,
    action: Exclude<WorkflowAction, "Start">,
    note: string,
    formData?: Record<string, unknown>,
  ) => Promise<boolean>;
};

export function MyTasksView({ tasks, activeUserId, language, status, onClaimTask, onReleaseTask, onExecuteTask }: MyTasksViewProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const openTasks = tasks.filter((task) => task.status === "Open" || task.status === "Claimed");
  const [pendingAction, setPendingAction] = useState<{
    taskId: string;
    action: Exclude<WorkflowAction, "Start">;
  } | null>(null);

  async function handleConfirm(note: string, formData?: Record<string, unknown>) {
    if (pendingAction) {
      const succeeded = await onExecuteTask(pendingAction.taskId, pendingAction.action, note, formData);
      if (succeeded) {
        setPendingAction(null);
      }
      return succeeded;
    }
    return false;
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
                  <div className="task-title-line">
                    <strong>{task.title || t("process.approvalTask")}</strong>
                    {task.priority ? <span className={`task-priority priority-${task.priority.toLowerCase()}`}>{t(`process.priority.${task.priority}` as TranslationKey)}</span> : null}
                  </div>
                  <span>
                    {task.id.slice(0, 8)} - {task.assignedCommunityRoleName || "Topluluk yetkisi"}
                  </span>
                  {task.nodeKey ? <small>{t("process.nodeKey")}: {task.nodeKey}</small> : null}
                  <small className="task-date">
                    {formatApiDateTime(task.createdAt, language)}
                  </small>
                </div>
                <TaskControls
                  activeUserId={activeUserId}
                  disabled={status === "acting"}
                  task={task}
                  language={language}
                  onAction={(action) => setPendingAction({ taskId: task.id, action })}
                  onClaim={() => onClaimTask(task.id, task.claimVersion)}
                  onRelease={() => onReleaseTask(task.id, task.claimVersion)}
                />
              </div>
            ))}
          </div>
        ) : (
          <p className="empty-state">{t("process.noOpenTasks", { role: language === "tr" ? "topluluk rolunuz" : "your community role" })}</p>
        )}
      </article>

      {pendingAction ? (
        <TaskActionDialog
          action={pendingAction.action}
          taskForm={tasks.find((task) => task.id === pendingAction.taskId)?.taskForm}
          language={language}
          disabled={status === "acting"}
          onCancel={() => setPendingAction(null)}
          onConfirm={handleConfirm}
        />
      ) : null}
    </>
  );
}

function TaskControls({
  activeUserId,
  disabled,
  language,
  onAction,
  onClaim,
  onRelease,
  task,
}: {
  activeUserId: string;
  disabled: boolean;
  language: Language;
  onAction: (action: Exclude<WorkflowAction, "Start">) => void;
  onClaim: () => void;
  onRelease: () => void;
  task: ProcessTask;
}) {
  const t = (key: TranslationKey) => translate(language, key);
  const requiresClaim = task.assignmentType === "Team" || task.assignmentType === "CommunityRole" || task.assignmentType === "TeamAndCommunityRole";
  const isClaimedByCurrentUser = task.claimedByUserId === activeUserId;
  const canAct = !requiresClaim || isClaimedByCurrentUser || !task.assignmentType;

  if (requiresClaim && !task.claimedByUserId) {
    return (
      <div className="task-actions">
        <button className="primary-button" disabled={disabled} onClick={onClaim} type="button">
          <Hand size={17} />
          {t("process.claim")}
        </button>
      </div>
    );
  }

  if (requiresClaim && !isClaimedByCurrentUser) {
    return <span className="task-claim-state"><UserRoundCheck size={16} />{t("process.claimedByAnother")}</span>;
  }

  return (
    <div className="task-actions">
      {task.availableActions.filter((action): action is Exclude<WorkflowAction, "Start"> => action !== "Start").map((action) => {
        const config = {
          Approve: { className: "success-button", icon: CheckCircle2 },
          Reject: { className: "danger-button", icon: XCircle },
          Complete: { className: "success-button", icon: CheckCheck },
          Escalate: { className: "escalate-button", icon: ArrowUpRight },
          SendBack: { className: "secondary-button", icon: Undo2 },
        }[action];
        const ActionIcon = config.icon;
        return (
          <button className={config.className} disabled={disabled || !canAct} key={action} onClick={() => onAction(action)} type="button">
            <ActionIcon size={17} />
            {translate(language, `action.${action}` as TranslationKey)}
          </button>
        );
      })}
      {requiresClaim && isClaimedByCurrentUser ? (
        <button className="secondary-button" disabled={disabled} onClick={onRelease} type="button">
          <Undo2 size={17} />
          {t("process.release")}
        </button>
      ) : null}
    </div>
  );
}
