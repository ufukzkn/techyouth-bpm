# API And Services

## Auth

- `POST /api/auth/login`
  - Validates username/password against the stored password hash.
  - Rejects pending/rejected users and temporarily locked accounts.
  - Returns an opaque session token value, user profile and expiry time.
  - Stores only the SHA-256 hash of the session token in `UserSessions`.
  - Accepts `rememberMe` to use the longer remember-me session duration.
- `POST /api/auth/register`
  - Creates a new `User` account with `PendingApproval` status.
  - Stores a PBKDF2 password hash and starts with `IsEmailVerified=false`.
- `GET /api/auth/me`
  - Hashes the incoming bearer token and reads the current unexpired session.
  - Returns active user information, including email verification and temporary-password requirement.
- `PATCH /api/auth/me/profile`
  - Updates the current user's display name and email.
  - If email changes, resets `IsEmailVerified=false` and clears any previous verification code.
- `POST /api/auth/me/password`
  - Verifies the current password and stores the new password as a PBKDF2 hash.
  - Clears `MustChangePassword` after a temporary-password user sets a real password.
- `POST /api/auth/logout`
  - Revokes the current session in the database.
- `GET /api/auth/sessions`
  - Lists active sessions for the current user, including created time, last seen time, IP address and user agent.
- `DELETE /api/auth/sessions/{sessionId}`
  - Revokes a selected session belonging to the current user.
- `POST /api/auth/me/email-verification`
  - Generates a short-lived local demo verification code.
  - Production should replace the response-visible demo code with an email delivery service.
- `POST /api/auth/me/email-verification/confirm`
  - Confirms the code and marks the current user's email as verified.

`POST /api/auth/login` and `POST /api/auth/register` use ASP.NET Core rate limiting. Login also increments failed attempts and sets `LockedUntil` after the configured threshold.

The project does not currently use JWT. It uses opaque bearer session tokens backed by the database. This is intentional for the current BPM scope because sessions can be expired and revoked centrally from the database. JWT would not remove the need for server-side state for pending approval, lockout, logout/revoke and active-session management. Future JWT support should be paired with refresh-token rotation and explicit revoke/session management instead of replacing the current flow blindly.

## Users And Access

- `GET /api/users`
  - Admin-only list of registered users, statuses, roles, verification state and lockout info.
- `POST /api/users`
  - Admin-only user creation with username, display name, email, role, status and temporary password.
  - New admin-created users start with `MustChangePassword=true`.
- `PATCH /api/users/{userId}/access`
  - Admin-only role/status update.
  - Used for approving `PendingApproval` accounts, rejecting accounts or changing roles.
  - Moving a user out of `Active` revokes existing sessions.
  - Audit text records the previous role/status and the new role/status.
- `GET /api/users/{userId}/sessions`
  - Admin-only active-session list for the selected user.
  - Used by the `Yetki ve Onay` detail panel to show whether a user is online, which sessions are active and which device/IP last touched the API.
- `DELETE /api/users/{userId}/sessions/{sessionId}`
  - Admin-only session revoke for the selected user.
  - The frontend asks for confirmation before sending this request.
  - Writes a system audit log.

## Audit

- `GET /api/audit/system`
  - Admin-only list of the latest critical system actions.
  - Shows actor user, action name, entity type/id, description and date.
  - Covers identity/access actions and core BPM actions such as form create/update, process start and task approve/reject.
  - The frontend does not dump all logs by default; Admin users search and page through the results, then open related chronological history.

Process-specific history is still returned from `GET /api/processes/{id}` as `auditLogs`. Admins and approvers can inspect visible process history; a normal user can inspect the history of processes they started. This satisfies the PDF expectation that actions such as start/approve/reject are tied back to the user who performed them.

## Forms

- `GET /api/forms`
  - Lists saved form definitions.
- `POST /api/forms`
  - Creates a form definition with fields and validation rules.
  - Stores `CreatedByUserId` and writes a system audit log.
- `GET /api/forms/{id}`
  - Returns a single form definition.
- `PUT /api/forms/{id}`
  - Updates an existing form definition.
  - Replaces the editable field list and validation rules with the submitted model.
  - Stores `UpdatedByUserId` / `UpdatedAt` and writes a system audit log.
  - Requires an Admin session, like form creation.

## Processes

- `POST /api/processes/start`
  - Validates submitted form data.
  - Stores JSON submission data.
  - Creates a process instance.
  - Creates the first task.
  - Stores `StartedByUserId`, writes process audit and writes system audit.
- `GET /api/processes`
  - Lists processes visible to the active user.
- `GET /api/processes/{id}`
  - Returns process status, dates, form data, tasks and audit history.

## Tasks

- `GET /api/tasks/my`
  - Lists tasks assigned to the current user or role.
- `POST /api/tasks/{id}/actions`
  - Runs an action such as approve or reject.
  - Updates process status through the state machine.
  - Stores `CompletedByUserId`, writes a process audit log entry and writes system audit.

## Swagger Usage

Swagger is available in development at `/swagger`.

Most endpoints require `Authorization: Bearer <token>`. Use `POST /api/auth/login` first, copy the returned token, then click `Authorize` in Swagger and paste the token. Swagger adds the bearer header to protected requests after authorization.

## Service Responsibility

Controllers should stay thin. Services own decisions:

- `AuthService`: registration, profile updates, password changes, password hash verification, lockout, session-token hashing, session metadata, session revoke, email verification and admin user access.
- `SystemAuditService`: critical system-action logging and Admin-only audit list.
- `FormService`: form definition CRUD and field validation.
- `ProcessService`: process start, detail and listing.
- `TaskService`: task listing and action execution.
- `ProcessStateMachine`: allowed transitions.
- `DatabaseSeeder`: local demo users and optional mock workflow data.

## Frontend Client Coverage

The frontend API client now exposes one method for each planned endpoint:

- Auth: `register`, `login`, `me`, `logout`, `updateProfile`, `changePassword`, `listSessions`, `revokeSession`, `startEmailVerification`, `confirmEmailVerification`
- Users: `listUsers`, `createUser`, `updateUserAccess`, `listUserSessions`, `revokeUserSession`
- Audit: `listSystemAuditLogs`
- Forms: `listForms`, `createForm`, `updateForm`, `getForm`
- Processes: `startProcess`, `listProcesses`, `getProcess`
- Tasks: `listMyTasks`, `executeTaskAction`

Feature components should call these client methods through feature-level orchestration instead of calling `fetch` directly.

## Implemented Backend Structure

- `TechYouthBpm.Domain`: entities and enums.
- `TechYouthBpm.Application`: DTOs, service interfaces and `ProcessStateMachine`.
- `TechYouthBpm.Infrastructure`: EF Core `AppDbContext`, SQLite/PostgreSQL provider setup, seed data and service implementations.
- `TechYouthBpm.Api`: controllers, Swagger, CORS and startup database seeding.

The API accepts the session token as `Authorization: Bearer <token>`.

Enum values are returned as readable strings, for example `Admin`, `InProgress` and `Approve`. This keeps the frontend role checks and status displays explicit.

Task actions load the task and parent process, validate role/action, update process status through `ProcessStateMachine`, and then write a separate `AuditLog` row. The detail response is reloaded from the database after save so UI state reflects persisted data, not a temporary in-memory object graph.

## Database Configuration

The API reads `Database:Provider` from configuration:

- `Sqlite`: default local mode, using `Data Source=techyouth-bpm.db`.
- `PostgreSql`: shared database mode, intended for Neon or another PostgreSQL host.

The connection string is read from `ConnectionStrings:DefaultConnection`. Real PostgreSQL credentials must be supplied through environment variables, .NET user secrets or a gitignored `.env.neon.local` file. Tracked documentation and config files only contain examples.

Local SQLite and shared Neon setup, current schema summary and reset/start commands are documented in `docs/08-local-database.md`. Any schema, seed data or local startup change should update that document and the matching startup script.

The local startup script enables `Seed__MockData=true` by default. This adds two form definitions, football-themed process submissions, open approver tasks and completed/rejected audit examples. Use `-SkipMockData` when a teammate needs a nearly empty local database.
