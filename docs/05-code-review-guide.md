# Code Review Guide

Use this file for the final code review presentation.

## What To Emphasize

- The project separates UI, API communication, domain logic and persistence.
- Form definitions are stored as models, not hardcoded screens.
- Process status changes are controlled by a state machine.
- Role checks exist in both UI visibility and backend authorization.
- Identity lifecycle includes pending approval, verified email state, session revoke, rate limiting and account lockout.
- Audit logs explain who changed a process and when.
- System audit logs explain who performed critical identity/access/form/process/task actions.
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
- `TaskAuthorizationTests` covers who can execute assigned tasks:
  - normal users cannot execute approver tasks.
  - approvers can execute approver tasks.
  - admins can execute any task.
  - closed or missing tasks return explicit errors.
- `AuditLogTests` proves approve/reject actions create audit entries with correct status transitions, user ids and notes.
- `SystemAuditServiceTests` proves only Admin users can read the system-wide audit trail.
- `FormServiceTests` covers form definition updates:
  - admin users can update a saved form.
  - non-admin users cannot update form definitions.
  - update replaces the editable field and validation-rule model.
- `AuthServiceTests` covers the hardened login path:
  - raw session tokens are returned only once to the client.
  - only hashed session tokens are stored in the database.
  - plaintext legacy passwords are upgraded to PBKDF2 hashes on successful login.
  - invalid passwords do not create sessions.
  - remember-me logins use the longer configured session duration.
  - registration creates pending/unverified accounts.
  - repeated failed login attempts lock the account.
  - logout revokes the stored session.
- The backend test suite currently passes with 30 tests across auth, form, workflow, task authorization and audit coverage.

## Current Frontend Story

- `LoginView` starts empty and uses demo-account buttons only to fill credentials for testing.
- `LoginView` supports both sign-in and register mode. Register creates a pending account and explains that admin approval is required.
- `sessionStore` keeps the active user, token, expiry and theme in Zustand so refresh does not reset the demo flow.
- `AuthService` verifies PBKDF2 password hashes and stores only hashed session tokens, while the frontend receives the raw opaque token once at login.
- `AuthService` keeps the current opaque-session model instead of JWT because logout, revoke, lockout and pending approval need server-side state anyway.
- `WorkspaceShell` verifies restored API sessions once on load, schedules local expiry, and sends expired/unauthorized sessions back to login with a confirmable alert.
- `WorkspaceShell` filters navigation items by user role while each Next.js route imports its own view component, which demonstrates role-based UI without scattering role checks across pages.
- `DashboardView` reads process/task metrics from the API, then turns metric cards and BPM flow steps into role-aware workspace shortcuts.
- Dashboard metrics keep the last loaded values while refreshing so quick navigation does not flash placeholder values.
- The form runner uses skeleton rows on first load and keeps cached form definitions during quick navigation.
- The top bar and settings screen show session expiry information so session state is visible after login.
- Settings now has email verification and active session management, including an all-devices sign-out action.
- Admin-only user approval/role/session controls live in the dedicated `Yonetim` workspace route with search, status filtering, pagination and a selected-user detail/history/session panel.
- User management and system audit search use backend paging/search parameters, so large user or audit tables are not fully loaded into the browser just to paginate in the UI.
- Role/status changes are staged until the Admin clicks an explicit apply button, then confirmed through a critical-action dialog before the API request is sent.
- Admin-only system history lives in the dedicated `Loglar` route. Logs are only shown after search, then paginated and inspectable through related chronological history.
- Process details continue to show process-specific audit history to Admin/Approver and the user who started that process.
- `FormDesignerDraft` edits a backend-ready definition model, including field keys, labels, field types, options, ordering and dependent validation rules.
- Saved form definitions can be loaded back into the designer and updated through the API instead of being create-only drafts.
- `FormRunnerDraft` loads saved form definitions, renders fields through the shared `FieldRenderer`, validates with shared helpers and starts a process through the API.
- `ProcessBoardDraft` coordinates persisted process/task data and delegates UI to `ProcessListView`, `MyTasksView`, `ProcessDetailPanel`, `TaskActionDialog`, `AuditTimeline` and `StatusBadge`.
- Task approve/reject actions collect an action note before calling the backend state machine endpoint.

## Review Questions To Be Ready For

- Why use a state machine instead of directly changing statuses?
- How would a new role be added?
- How would a new field type be added?
- What happens if an invalid action is sent to the backend?
- Why store form submissions as JSON?
- Why is backend validation still needed when frontend validation exists?
- How does the audit log help reviewers trace process decisions?
- Why split process audit and system audit?
- Why opaque session tokens instead of JWT?
- How does pending approval affect login?
- What protects login/register against repeated abuse?
