# API And Services

## Auth

- `POST /api/auth/login`
  - Validates username/password.
  - Returns a token-like session value and user profile.
- `GET /api/auth/me`
  - Reads the current session.
  - Returns active user information.

## Forms

- `GET /api/forms`
  - Lists saved form definitions.
- `POST /api/forms`
  - Creates a form definition with fields and validation rules.
- `GET /api/forms/{id}`
  - Returns a single form definition.

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

## Service Responsibility

Controllers should stay thin. Services own decisions:

- `AuthService`: login/session/user lookup.
- `FormService`: form definition CRUD and field validation.
- `ProcessService`: process start, detail and listing.
- `TaskService`: task listing and action execution.
- `ProcessStateMachine`: allowed transitions.

## Frontend Client Coverage

The frontend API client now exposes one method for each planned endpoint:

- Auth: `login`, `me`
- Forms: `listForms`, `createForm`, `getForm`
- Processes: `startProcess`, `listProcesses`, `getProcess`
- Tasks: `listMyTasks`, `executeTaskAction`

Feature components should call these client methods through feature-level orchestration instead of calling `fetch` directly.

## Implemented Backend Structure

- `TechYouthBpm.Domain`: entities and enums.
- `TechYouthBpm.Application`: DTOs, service interfaces and `ProcessStateMachine`.
- `TechYouthBpm.Infrastructure`: EF Core `AppDbContext`, SQLite setup, seed data and service implementations.
- `TechYouthBpm.Api`: controllers, Swagger, CORS and startup database seeding.

The API accepts the session token as `Authorization: Bearer <token>`.
