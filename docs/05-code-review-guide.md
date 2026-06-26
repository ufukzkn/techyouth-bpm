# Code Review Guide

Use this file for the final code review presentation.

## What To Emphasize

- The project separates UI, API communication, domain logic and persistence.
- Form definitions are stored as models, not hardcoded screens.
- Process status changes are controlled by a state machine.
- Role checks exist in both UI visibility and backend authorization.
- Audit logs explain who changed a process and when.
- Documentation was updated together with code to keep the project reviewable.

## Suggested Review Flow

1. Show the PDF requirements summary.
2. Explain monorepo structure.
3. Demo login and role-based layout.
4. Demo form design and validation.
5. Demo process start and task action.
6. Review state machine code.
7. Review API service boundaries.
8. Review tests and commit history.

## Current Test Story

- `ProcessStateMachineTests` proves the allowed BPM transitions:
  - `Pending -> Start -> InProgress`
  - `InProgress -> Approve -> Completed`
  - `InProgress -> Reject -> Rejected`
- Invalid transitions are rejected with an explicit error.
- Available actions are derived from the current process status instead of being hardcoded in the UI.

## Current Frontend Story

- `LoginView` starts empty and uses demo-account buttons only to fill credentials for testing.
- `sessionStore` keeps the active user, token, expiry and theme in Zustand so refresh does not reset the demo flow.
- `AppShell` verifies restored API sessions once on load, schedules local expiry, and sends expired/unauthorized sessions back to login with a clear notice.
- `AppShell` filters navigation items by user role and keeps the active screen in the URL query, which demonstrates role-based UI without scattering role checks across pages.
- `DashboardView` reads process/task metrics from the API instead of showing fixed numbers.
- `FormDesignerDraft` saves the form as a backend-ready definition model instead of only previewing hardcoded markup.
- `FormRunnerDraft` loads saved form definitions and starts a process through the API after client-side validation.
- `ProcessBoardDraft` lists persisted processes/tasks and executes approve/reject actions through the backend state machine.

## Review Questions To Be Ready For

- Why use a state machine instead of directly changing statuses?
- How would a new role be added?
- How would a new field type be added?
- What happens if an invalid action is sent to the backend?
- Why store form submissions as JSON?
