# BPM And State Machine

## BPM In Plain Language

BPM means Business Process Management. In this project it means modeling a real business flow as steps, tasks, statuses and actions.

Simple example:

1. A user submits a form.
2. The system starts a process.
3. An approver receives a task.
4. The approver approves or rejects.
5. The process status changes and the history is saved.

The implemented transfer demo expands this into Scout review, Technical review, an amount-based Finance gateway and Transfer Operations. The important idea is that the app is not only storing form data. It stores a published process definition, routes work through that graph and records who completed every step.

## State Machine

A state machine defines which status changes are allowed.

The process lifecycle uses these statuses:

- `Pending`: process creation inside the transaction before the runtime follows the Start node. The current synchronous runtime immediately moves a successfully created process to `InProgress`, so this is an internal transition state rather than a persistent list filter.
- `InProgress`: process is actively waiting for a task decision.
- `Completed`: process was approved and finished.
- `Rejected`: process was rejected and finished.
- `Escalated`: process needs elevated review before an approve/reject decision.

The legacy compatibility flow still supports:

- `Start`: `Pending` to `InProgress`
- `Approve`: `InProgress` to `Completed`
- `Reject`: `InProgress` to `Rejected`
- `Escalate`: `InProgress` to `Escalated`; an escalated process can then be approved or rejected.

The dynamic runtime adds node-level actions: `Approve`, `Reject`, `Complete`, `Escalate` and `SendBack`. A published `ProcessDefinitionVersion` contains typed Start, User Task, Exclusive Gateway and End nodes. The runtime follows an action edge, evaluates typed gateway conditions and either creates the next task or closes the process.

`Escalated` is entered only when an authorized user performs the explicit `Escalate` action. A User Task SLA persists `DueAt` for ordering and overdue presentation, but no background timer automatically escalates or reassigns overdue work in the current version.

## Why Two Layers Exist

- `ProcessStateMachine` protects the high-level process lifecycle.
- `DynamicWorkflowEngine` controls the current graph node, task assignment, form output, gateway routing and step history.

This separation keeps status rules small while allowing new workflow shapes without adding controller conditionals.

## Runtime Safety

- Published form and workflow versions are immutable; a running instance stays pinned to its starting versions.
- Team/role candidate tasks require claim before action. `ClaimVersion` optimistic concurrency ensures only one stale client can win.
- Task output is validated against the published task-form version.
- Gateway conditions may use only start data or outputs produced by earlier tasks, and condition value/operator types must match the selected form field.
- `SendBack` preserves completed execution history while clearing invalidated downstream variables before creating the next task attempt.
- Process, task, notification and audit writes share an EF Core transaction.
- A task decision writes two distinct business facts: the task action remains
  in the `Tasks` audit category, while its process outcome is recorded as
  `Process.Advanced`, `Process.SentBack`, `Process.Completed` or
  `Process.Rejected` in `Processes`. Automatic gateway hops are not emitted as
  noisy standalone events.
- A missing candidate or downstream failure rolls the entire operation back.
- Automatic routing has a 100-hop limit to stop accidental infinite loops.
- Dashboard, process-list and task-list reads do not create audit entries. Start, claim, release, action, publish and team-membership mutations remain auditable.
- `Islerim > Gecmis Islerim` contains tasks completed by the signed-in user. Overdue but still-open work remains in the active list; community-wide history is inspected from process history and system audit.

Invalid lifecycle transitions, unavailable actions, invalid graph edges and unauthorized claims are rejected by the backend. This is the central code-review point for BPM correctness.

When a community is permanently deleted, operational process/task rows are
removed. A safe timeline projection from `ProcessStepExecution` is copied into
the SuperAdmin-only deletion archive first. It preserves who performed which
step, the team/role snapshot and time, but deliberately excludes form answers,
task notes and raw JSON.
