"use client";

import {
  ArrowDown,
  ArrowUp,
  ArrowUpRight,
  CheckCheck,
  CheckCircle2,
  CircleDot,
  Clock3,
  Hand,
  Crown,
  RefreshCw,
  Undo2,
  UserRoundCheck,
  XCircle,
} from "lucide-react";
import { useState } from "react";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { TaskActionDialog } from "@/features/processes/TaskActionDialog";
import { SlidingSegmentedControl } from "@/features/ui/SlidingSegmentedControl";
import { formatApiDateTime } from "@/lib/dateTime";
import type {
  Language,
  PagedResult,
  ProcessTask,
  TaskListParams,
  TaskPriority,
  WorkflowAction,
} from "@/lib/types";

type MyTasksViewProps = {
  result: PagedResult<ProcessTask>;
  activeUserId: string;
  language: Language;
  selectedTaskId: string;
  priorityFilter: TaskPriority | "all";
  sortBy: NonNullable<TaskListParams["sortBy"]>;
  sortDirection: "asc" | "desc";
  status: "loading" | "refreshing" | "idle" | "acting" | "error";
  isListLoading: boolean;
  showListSkeleton: boolean;
  listError: string | null;
  taskView: NonNullable<TaskListParams["view"]>;
  onClaimTask: (taskId: string, claimVersion?: string | null) => void;
  onReleaseTask: (taskId: string, claimVersion?: string | null) => void;
  onSelectTask: (task: ProcessTask) => void;
  onPriorityChange: (value: TaskPriority | "all") => void;
  onSortByChange: (value: NonNullable<TaskListParams["sortBy"]>) => void;
  onSortDirectionChange: (value: "asc" | "desc") => void;
  onNextPage: () => void;
  onPageChange: (page: number) => void;
  onPreviousPage: () => void;
  onExecuteTask: (
    taskId: string,
    action: Exclude<WorkflowAction, "Start">,
    note: string,
    formData?: Record<string, unknown>,
  ) => Promise<boolean>;
  onLoadTaskDetail: (taskId: string) => Promise<ProcessTask | null>;
  onTaskViewChange: (view: NonNullable<TaskListParams["view"]>) => void;
  onRetry: () => void;
};

const priorities: Array<TaskPriority | "all"> = ["all", "Critical", "High", "Normal", "Low"];

export function MyTasksView({
  result,
  activeUserId,
  language,
  selectedTaskId,
  priorityFilter,
  sortBy,
  sortDirection,
  status,
  isListLoading,
  showListSkeleton,
  listError,
  taskView,
  onClaimTask,
  onReleaseTask,
  onSelectTask,
  onPriorityChange,
  onSortByChange,
  onSortDirectionChange,
  onNextPage,
  onPageChange,
  onPreviousPage,
  onExecuteTask,
  onLoadTaskDetail,
  onTaskViewChange,
  onRetry,
}: MyTasksViewProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const isTr = language === "tr";
  const tasks = result.items;
  const [renderedAt] = useState(() => Date.now());
  const [pendingAction, setPendingAction] = useState<{
    task: ProcessTask;
    action: Exclude<WorkflowAction, "Start">;
  } | null>(null);
  const [loadingTaskId, setLoadingTaskId] = useState<string | null>(null);
  const totalPages = Math.max(1, Math.ceil(result.totalCount / result.pageSize));

  async function handleConfirm(note: string, formData?: Record<string, unknown>) {
    if (!pendingAction) return false;
    const succeeded = await onExecuteTask(pendingAction.task.id, pendingAction.action, note, formData);
    if (succeeded) setPendingAction(null);
    return succeeded;
  }

  async function openTaskAction(task: ProcessTask, action: Exclude<WorkflowAction, "Start">) {
    if (!task.formDefinitionVersionId || task.taskForm) {
      setPendingAction({ task, action });
      return;
    }

    setLoadingTaskId(task.id);
    try {
      const detailedTask = await onLoadTaskDetail(task.id);
      if (detailedTask) setPendingAction({ task: detailedTask, action });
    } finally {
      setLoadingTaskId(null);
    }
  }

  return (
    <>
      <article className="process-card">
        <div className="process-card-header">
          <div>
            <span className="eyebrow">{t("process.myTasks")}</span>
            <strong>
              {showListSkeleton
                ? t("common.loading")
                : t(taskView === "history" ? "process.historyTaskCount" : "process.openTaskCount", { count: result.totalCount })}
              {isListLoading && !showListSkeleton ? <RefreshCw aria-label={t("process.refreshing")} className="inline-refresh-icon spin-icon" size={14} /> : null}
            </strong>
          </div>
          <CircleDot size={22} />
        </div>

        <div className="task-query-toolbar">
          <SlidingSegmentedControl
            ariaLabel={isTr ? "İş görünümü" : "Task view"}
            name="task-list-view"
            onChange={onTaskViewChange}
            options={[
              { value: "active", label: t("process.taskViewActive") },
              { value: "history", label: t("process.taskViewHistory") },
            ]}
            value={taskView}
          />
          <label>
            <span>{isTr ? "Öncelik" : "Priority"}</span>
            <select value={priorityFilter} onChange={(event) => onPriorityChange(event.target.value as TaskPriority | "all")}>
              {priorities.map((priority) => (
                <option key={priority} value={priority}>
                  {priority === "all" ? (isTr ? "Tüm öncelikler" : "All priorities") : t(`process.priority.${priority}` as TranslationKey)}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>{isTr ? "Sırala" : "Sort"}</span>
            <select value={sortBy} onChange={(event) => onSortByChange(event.target.value as NonNullable<TaskListParams["sortBy"]>)}>
              <option value="dueAt">{isTr ? "En yakın son tarih" : "Nearest deadline"}</option>
              <option value="priority">{isTr ? "Öncelik" : "Priority"}</option>
              <option value="newest">{isTr ? "En yeni" : "Newest"}</option>
              <option value="oldest">{isTr ? "En eski" : "Oldest"}</option>
            </select>
          </label>
          <button
            aria-label={isTr ? "Sıralama yönünü değiştir" : "Change sort direction"}
            className="icon-button"
            onClick={() => onSortDirectionChange(sortDirection === "asc" ? "desc" : "asc")}
            type="button"
          >
            {sortDirection === "asc" ? <ArrowUp size={16} /> : <ArrowDown size={16} />}
          </button>
        </div>

        {listError ? (
          <div className="process-list-load-error" role="alert">
            <p>{listError}</p>
            <button className="secondary-button" onClick={onRetry} type="button"><RefreshCw size={16} />{t("common.refresh")}</button>
          </div>
        ) : showListSkeleton ? (
          <TaskListRegionSkeleton label={t("process.skeletonTasks")} />
        ) : tasks.length > 0 ? (
          <div className="task-list">
            {tasks.map((task) => {
              const isOverdue = Boolean(task.dueAt && Date.parse(task.dueAt) < renderedAt);
              return (
                <div
                  className={`${selectedTaskId === task.id ? "task-item is-selected" : "task-item"}${isOverdue ? " is-overdue" : ""}`}
                  key={task.id}
                >
                  <button className="task-item-select" onClick={() => onSelectTask(task)} type="button">
                    <div>
                      <div className="task-title-line">
                        <strong>{task.title || t("process.approvalTask")}</strong>
                        {task.priority ? <span className={`task-priority priority-${task.priority.toLowerCase()}`}>{t(`process.priority.${task.priority}` as TranslationKey)}</span> : null}
                        {task.requiresTeamLead ? <span className="task-lead-badge"><Crown size={13} />{t("process.teamLeadOnly")}</span> : null}
                      </div>
                      <span>{task.workflowName || task.formName || task.id.slice(0, 8)} · {task.communityName || task.assignedCommunityRoleName || (isTr ? "Topluluk yetkisi" : "Community access")}</span>
                      {task.formName ? <small>{task.formName}</small> : null}
                      <TaskAssignmentContext language={language} task={task} />
                      <small className="task-date"><Clock3 size={13} /> {formatApiDateTime(task.createdAt, language)}</small>
                      {task.dueAt ? (
                        <small className={isOverdue ? "task-due-date is-overdue" : "task-due-date"}>
                          <Clock3 size={13} /> {isOverdue ? (isTr ? "Gecikti" : "Overdue") : (isTr ? "Son tarih" : "Due")} · {formatApiDateTime(task.dueAt, language)}
                        </small>
                      ) : null}
                      {taskView === "history" ? <TaskCompletionContext language={language} task={task} /> : null}
                    </div>
                  </button>
                  {taskView === "active" ? <div>
                    <TaskControls
                      activeUserId={activeUserId}
                      disabled={status === "acting" || loadingTaskId === task.id}
                      language={language}
                      onAction={(action) => void openTaskAction(task, action)}
                      onClaim={() => onClaimTask(task.id, task.claimVersion)}
                      onRelease={() => onReleaseTask(task.id, task.claimVersion)}
                      task={task}
                    />
                  </div> : null}
                </div>
              );
            })}
          </div>
        ) : (
          <p className="empty-state">
            {taskView === "history"
              ? (isTr ? "Henüz tamamladığınız bir iş bulunmuyor." : "You have not completed a task yet.")
              : t("process.noOpenTasks", { role: isTr ? "topluluk rolünüz" : "your community role" })}
          </p>
        )}

        {totalPages > 1 ? (
          <PaginationControls
            currentPage={result.page}
            language={language}
            onNext={onNextPage}
            onPageChange={onPageChange}
            onPrevious={onPreviousPage}
            totalPages={totalPages}
          />
        ) : null}
      </article>

      {pendingAction ? (
        <TaskActionDialog
          action={pendingAction.action}
          disabled={status === "acting"}
          language={language}
          onCancel={() => setPendingAction(null)}
          onConfirm={handleConfirm}
          taskForm={pendingAction.task.taskForm}
        />
      ) : null}
    </>
  );
}

function TaskListRegionSkeleton({ label }: { label: string }) {
  return (
    <div aria-label={label} className="task-list task-list-skeleton" role="status">
      {[0, 1, 2].map((item) => <span key={item} />)}
    </div>
  );
}

function TaskAssignmentContext({ language, task }: { language: Language; task: ProcessTask }) {
  const t = (key: TranslationKey) => translate(language, key);
  const entries = [
    task.candidateTeamName ? [t("process.assignmentTeam"), task.candidateTeamName] : null,
    task.candidateCommunityRoleName ? [t("process.assignmentRole"), task.candidateCommunityRoleName] : null,
    task.assignedUserDisplayName ? [t("process.assignmentUser"), task.assignedUserDisplayName] : null,
    task.claimedByUserDisplayName ? [t("process.claimOwner"), task.claimedByUserDisplayName] : null,
  ].filter((entry): entry is string[] => entry !== null);

  if (entries.length === 0) return null;
  return (
    <dl className="task-assignment-context">
      {entries.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
    </dl>
  );
}

function TaskCompletionContext({ language, task }: { language: Language; task: ProcessTask }) {
  const t = (key: TranslationKey) => translate(language, key);
  return (
    <div className="task-completion-context">
      {task.completedByUserDisplayName ? <small><strong>{t("process.completedByLabel")}:</strong> {task.completedByUserDisplayName}</small> : null}
      {task.completedAction ? <small><strong>{translate(language, `action.${task.completedAction}` as TranslationKey)}</strong></small> : null}
      {task.completionNote ? <small><strong>{t("process.completionNote")}:</strong> {task.completionNote}</small> : null}
      {task.completedAt ? <small><Clock3 size={13} /> {formatApiDateTime(task.completedAt, language)}</small> : null}
    </div>
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
  const isTeamLeadBlocked = task.requiresTeamLead && task.canCurrentUserAct === false;
  const canAct = (!requiresClaim || isClaimedByCurrentUser || !task.assignmentType) && !isTeamLeadBlocked;

  if (isTeamLeadBlocked) {
    return (
      <div className="task-lead-restriction">
        <span><Crown size={16} />{t("process.teamLeadRequired")}</span>
        {requiresClaim && isClaimedByCurrentUser ? (
          <button className="secondary-button" disabled={disabled} onClick={onRelease} type="button">
            <Undo2 size={17} />{t("process.release")}
          </button>
        ) : null}
      </div>
    );
  }

  if (requiresClaim && !task.claimedByUserId) {
    return (
      <div className="task-actions">
        <button className="primary-button" disabled={disabled || task.canCurrentUserClaim === false} onClick={onClaim} type="button">
          <Hand size={17} />{t("process.claim")}
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
        return <button className={config.className} disabled={disabled || !canAct} key={action} onClick={() => onAction(action)} type="button"><ActionIcon size={17} />{translate(language, `action.${action}` as TranslationKey)}</button>;
      })}
      {requiresClaim && isClaimedByCurrentUser ? <button className="secondary-button" disabled={disabled} onClick={onRelease} type="button"><Undo2 size={17} />{t("process.release")}</button> : null}
    </div>
  );
}
