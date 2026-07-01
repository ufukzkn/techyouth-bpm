# API And Services

## Auth

- `POST /api/auth/login`
  - Validates username/password against the stored password hash.
  - Returns an opaque session token value, user profile and expiry time.
  - Stores only the SHA-256 hash of the session token in `UserSessions`.
  - Accepts `rememberMe` to use the longer remember-me session duration.
- `GET /api/auth/me`
  - Hashes the incoming bearer token and reads the current unexpired session.
  - Returns active user information.

The project does not currently use JWT. It uses opaque bearer session tokens backed by the database. This is intentional for the current BPM scope because sessions can be expired centrally from the database. Future JWT support should be paired with refresh-token/remember-me design instead of replacing the current flow blindly.

## Forms

- `GET /api/forms`
  - Lists saved form definitions.
- `POST /api/forms`
  - Creates a form definition with fields and validation rules.
- `GET /api/forms/{id}`
  - Returns a single form definition.
- `PUT /api/forms/{id}`
  - Updates an existing form definition.
  - Replaces the editable field list and validation rules with the submitted model.
  - Requires an Admin session, like form creation.

## Processes

- `POST /api/processes/start`
  - Validates submitted form data.
  - Stores JSON submission data.
  - Creates a process instance.
  - Creates the first task.
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
  - Writes an audit log entry.

## Swagger Usage

Swagger is available in development at `/swagger`.

Most endpoints require `Authorization: Bearer <token>`. Use `POST /api/auth/login` first, copy the returned token, then click `Authorize` in Swagger and paste the token. Swagger adds the bearer header to protected requests after authorization.

## Service Responsibility

Controllers should stay thin. Services own decisions:

- `AuthService`: password hash verification, session-token hashing, login/session/user lookup.
- `FormService`: form definition CRUD and field validation.
- `ProcessService`: process start, detail and listing.
- `TaskService`: task listing and action execution.
- `ProcessStateMachine`: allowed transitions.
- `DatabaseSeeder`: local demo users and optional mock workflow data.

## Frontend Client Coverage

The frontend API client now exposes one method for each planned endpoint:

- Auth: `login`, `me`
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

The connection string is read from `ConnectionStrings:DefaultConnection`. Real PostgreSQL credentials must be supplied through environment variables or .NET user secrets. Tracked documentation and config files only contain examples.

Local SQLite setup, current schema summary and reset/start commands are documented in `docs/08-local-database.md`. Any schema, seed data or local startup change should update that document and `scripts/run-api-local.ps1`.

The local startup script enables `Seed__MockData=true` by default. This adds two form definitions, football-themed process submissions, open approver tasks and completed/rejected audit examples. Use `-SkipMockData` when a teammate needs a nearly empty local database.
