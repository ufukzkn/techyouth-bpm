# BPM And State Machine

## BPM In Plain Language

BPM means Business Process Management. In this project it means modeling a real business flow as steps, tasks, statuses and actions.

Example:

1. A user submits a form.
2. The system starts a process.
3. An approver receives a task.
4. The approver approves or rejects.
5. The process status changes and the history is saved.

The important idea is that the app is not only storing form data. It is also tracking where that data is in a business process.

## State Machine

A state machine defines which status changes are allowed.

This implementation uses these statuses:

- `Pending`: process exists but is waiting for the first action.
- `InProgress`: process is actively waiting for a task decision.
- `Completed`: process was approved and finished.
- `Rejected`: process was rejected and finished.

Allowed actions:

- `Start`: `Pending` to `InProgress`
- `Approve`: `InProgress` to `Completed`
- `Reject`: `InProgress` to `Rejected`

Invalid transitions are rejected by the backend. This is the main code review point for BPM correctness.
