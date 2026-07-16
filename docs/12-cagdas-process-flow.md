# Cagdas Process Flow

## Purpose

This document tracks Cagdas Kaplan's ownership area: Process, Tasks and Review Flow. The goal is to turn submitted form data into trackable process instances with assigned tasks, approve/reject actions, status transitions through a state machine and a full audit trail.

## Scope Boundary

This work covers the BPM workflow layer after a form is submitted. It does not own form modeling, form design or the authentication/shell experience, but it consumes form definitions to start processes and relies on role-based sessions for authorization.

The scope includes:

- Process instance creation from submitted form data.
- Process listing with status filtering.
- Process detail display with form data, tasks and audit history.
- Task listing for the current user/role.
- Task approve/reject actions with user note input.
- State machine transitions and validation.
- Audit log creation and timeline display.
- Backend test coverage for authorization and audit behavior.

## Dynamic Workflow Extension

The original one-step state-machine flow remains as a compatibility path, but the primary process path is now versioned and graph-driven:

- `ProcessDefinitionVersion` stores a published typed graph with Start, User Task, Exclusive Gateway and End nodes.
- `DynamicWorkflowEngine` routes the exact version pinned to the process instance.
- User tasks support priority, optional task form, action set and ProcessStarter/SpecificUser/Team/CommunityRole/TeamAndCommunityRole assignment.
- Task list/detail responses include the pinned published task-form snapshot; the action dialog submits its validated `formData` together with the selected workflow action and note.
- Candidate-pool tasks must be claimed. `ClaimVersion` optimistic concurrency allows only one stale client to win.
- `SendBack` creates a new node attempt; completed history is never reopened or rewritten, and invalidated downstream `steps.*` values are cleared before routing resumes.
- Gateway conditions can reference only fields produced before that gateway and the backend validates field/value/operator type compatibility before publish.
- `ProcessStepExecution` records node, attempt, actor, action, timestamps and task-form output.
- Start/action transactions include process state, task, variables, notification and both audit channels.
- `Complete` supports operational tasks in addition to approval-oriented actions.

The visual graph editor is implemented under `/workflows` with `@xyflow/react`; the backend contract remains independent from that library.

## Completed Work

- Split the monolithic `ProcessBoardDraft.tsx` (246 lines) into 6 focused components.
- Added `StatusBadge` with Turkish status labels and a missing `Pending` badge color.
- Added `ProcessListView` with status filter chips (Tumu, Beklemede, Devam Eden, Tamamlanan, Reddedilen).
- Added `ProcessDetailPanel` with completion date, task count summary and the audit timeline.
- Added `MyTasksView` with task creation date display and dialog-triggered actions.
- Added `TaskActionDialog` as a modal with note textarea, replacing hardcoded approve/reject notes.
- Added `AuditTimeline` with color-coded nodes (blue=Start, green=Approve, red=Reject), from/to transition badges, user names, timestamps and notes.
- Refactored `ProcessBoardDraft` into a thin orchestrator that delegates to sub-components.
- Added `TaskAuthorizationTests` with 5 test scenarios covering role-based access and error handling.
- Added `AuditLogTests` with 4 test scenarios covering audit log creation after approve/reject actions.
- Added `TestDbFactory` as a shared helper for creating InMemory database contexts and seed data.

### Bonus Work (Completed)
- **Drag-and-Drop Process Reordering**: Implemented a drag-and-drop feature to allow users to visually reorder processes in `ProcessListView` using `@dnd-kit`. Extracted `SortableProcessCard`, added ghost overlay during dragging, persisted ordering logic to `localStorage` to save the customized view state per user, and added keyboard accessible move up/down controls.
- **Escalation Workflow**: Added `Escalate` action and `Escalated` status. Updated `ProcessStateMachine` to handle transitions (`InProgress → Escalated`, `Escalated → Approve/Reject`) and updated `TaskService` to automatically spawn an Admin review task upon escalation.

## Current Process Flow Capabilities

### Process List

- All processes are loaded from `GET /api/processes` with role-based visibility (User sees only their own, Admin/Approver see all).
- The list endpoint returns summary DTOs through EF Core projection, so it does not load every task and audit log while rendering the board.
- Status filter chips allow narrowing the list by Pending, InProgress, Completed, Rejected or Escalated.
- Filtered count and total count are shown in the card header.
- Clicking a process loads its full detail from `GET /api/processes/{id}`.

### Process Detail

- Shows form name, status badge, start date, completion date (if finished) and task summary.
- Displays submitted form data as formatted JSON.
- Shows all tasks with their open/completed counts.
- Renders the full audit timeline with chronological entries.
- Full task and audit data is intentionally loaded at detail time, not during the process list query.

### My Tasks

- Open tasks are loaded from `GET /api/tasks/my` filtered by the current user's role.
- Each task shows a process context label, task ID prefix, assigned role and creation date.
- Approve and Reject buttons open a `TaskActionDialog` modal.

### Task Action Dialog

- Modal overlay with backdrop blur for focus.
- Text input for the action note with contextual placeholder text.
- If the user leaves the note empty, a sensible default is used.
- Confirm sends `POST /api/tasks/{id}/actions` with the action and note.
- Loading state disables buttons while the API call completes.

### Audit Timeline

- Visual timeline with a vertical connecting line between entries.
- Each entry has a color-coded circular node:
  - Blue for `Start` actions.
  - Green for `Approve` actions.
  - Red for `Reject` actions.
- Each entry shows the action label in Turkish, a from/to status transition badge, the acting user's display name, a timestamp in Turkish locale and the action note.

## State Machine Summary

The `ProcessStateMachine` defines allowed transitions as a simple dictionary:

| Current Status | Action | Next Status |
| --- | --- | --- |
| Pending | Start | InProgress |
| InProgress | Approve | Completed |
| InProgress | Reject | Rejected |
| InProgress | Escalate | Escalated |
| Escalated | Approve | Completed |
| Escalated | Reject | Rejected |

Any other combination returns a validation error. This remains the lifecycle guarantee for legacy and dynamic processes. Dynamic node routing adds a second guarantee: an action must exist on the published task node and have exactly one valid graph edge. Graph navigation never directly replaces lifecycle validation.

## Test Coverage

### ProcessStateMachineTests (8 tests, existing)

- 3 valid transitions are allowed.
- 4 invalid transitions are rejected with explicit errors.
- Available actions are derived from current status.

### TaskAuthorizationTests (5 tests, new)

- User role cannot execute Approver-assigned tasks.
- Approver can execute Approver-assigned tasks.
- Admin can execute any task.
- Closed tasks return "already closed" error.
- Nonexistent tasks return "not found" error.

### AuditLogTests (4 tests, new)

- Approve creates an audit log with InProgress to Completed transition.
- Reject creates an audit log with InProgress to Rejected transition.
- Start audit log exists from seed with Pending to InProgress transition.
- Null action note is preserved as empty string.

## Files Changed

Frontend process-flow files:

- `apps/web/src/features/processes/ProcessBoardDraft.tsx`
- `apps/web/src/features/processes/ProcessListView.tsx`
- `apps/web/src/features/processes/ProcessCard.tsx`
- `apps/web/src/features/processes/SortableProcessCard.tsx`
- `apps/web/src/features/processes/ProcessDetailPanel.tsx`
- `apps/web/src/features/processes/MyTasksView.tsx`
- `apps/web/src/features/processes/TaskActionDialog.tsx`
- `apps/web/src/features/processes/AuditTimeline.tsx`
- `apps/web/src/features/processes/StatusBadge.tsx`
- `apps/web/src/styles/processes.css`
- `apps/web/src/lib/types.ts`
- `apps/web/src/features/i18n/translations.ts`

Backend test files:

- `apps/api/tests/TechYouthBpm.Tests/Workflow/TaskAuthorizationTests.cs`
- `apps/api/tests/TechYouthBpm.Tests/Workflow/AuditLogTests.cs`
- `apps/api/tests/TechYouthBpm.Tests/Workflow/ProcessStateMachineTests.cs`
- `apps/api/tests/TechYouthBpm.Tests/TestDbFactory.cs`
- `apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj`

Documentation files:

- `docs/12-cagdas-process-flow.md`

## Out of Scope

These capabilities remain intentionally deferred:

- Parallel gateway execution and join semantics.
- Timer/SLA/escalation scheduling.
- Service tasks and external ERP integrations.
- BPMN XML import/export or Camunda deployment.
- Cross-community workflow routing.

## Presentation Talking Points

1. **Why a state machine plus runtime?** The state machine owns process lifecycle, while the runtime owns graph navigation. This prevents canvas routing rules from scattering status mutations across controllers.

2. **End-to-end action flow:** UI note/task form → API client → `TasksController` → authorization/claim/form validation → `DynamicWorkflowEngine.ContinueAsync()` → next task/end → audit/notification commit → detail reload.

3. **Authorization layers:** community scope, permission/candidate membership, claim ownership, persisted available action, graph edge and lifecycle validation must all pass.

4. **Why separate audit logs?** They are a separate table, not just a status field. You can trace who did what, when, with which note. This is real BPM traceability.

5. **Component architecture:** The monolithic draft was split into 6 focused components. Each has a single responsibility: list, detail, tasks, actions, timeline, badge.

## Review Questions To Be Ready For

- Why use a state machine instead of directly changing statuses?
- What happens if an invalid action is sent to the backend?
- How does the audit log help reviewers trace process decisions?
- Why split process audit and system audit?
- How would a new workflow action (e.g. Escalate) be added?
- Why collect action notes before approve/reject?
- Why create a new attempt on SendBack instead of reopening history?
- How does `ClaimVersion` prevent a double claim?
- Why keep React Flow objects out of the API DTO?

## Verification

Frontend checks:

```bash
cd apps/web
npm run lint
npm run build
```

Backend checks:

```bash
dotnet test apps/api/TechYouthBpm.slnx
```

The current suite also covers graph validation, gateway conditions, rollback, candidate resolution, claim competition, HTTP workflow publish/start/complete and provider migrations. The exact baseline is tracked in `docs/01-agent-notes.md`.
