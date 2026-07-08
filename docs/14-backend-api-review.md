# Backend API Review

## Scope

This report reviews the backend/API side of the TechYouth BPM project: .NET REST API structure, authentication, authorization, persistence, BPM/process/task flow, Swagger, tests and production-readiness. It intentionally does not review frontend UI details.

## Current Backend Fit Against PDF Requirements

The backend satisfies the core PDF expectations well:

- **.NET 8 REST API:** implemented under `apps/api` with thin controllers and service-driven business logic.
- **Swagger/OpenAPI:** enabled in development through Swashbuckle with bearer token support.
- **Authentication and user/role storage:** users, roles, statuses, password hashes, active sessions, refresh tokens and lockout fields are stored in EF Core entities.
- **Authorization:** service methods enforce role checks for admin-only form/user operations, process visibility and task execution.
- **EF Core database layer:** `AppDbContext` supports SQLite by default and PostgreSQL through Npgsql for Neon/shared database testing.
- **Form persistence:** form definitions, fields, options and validation rules are persisted.
- **Submission and process start:** submitted form data is validated and stored as JSON, then converted into a process instance with the first approver task.
- **Task and BPM flow:** assigned tasks support approve/reject, and process status transitions are controlled by `ProcessStateMachine`.
- **Audit traceability:** process audit logs and system audit logs record who performed key actions.
- **Backend validation:** required, type-based and dependent validation are centralized in backend services.

Overall, the backend is beyond a simple demo API. It has a layered structure and enough security/audit behavior to be defended as an extensible BPM foundation.

## Architecture Assessment

The project uses a clean four-layer split:

- `TechYouthBpm.Domain`: entities and enums.
- `TechYouthBpm.Application`: DTOs, service interfaces and workflow state machine.
- `TechYouthBpm.Infrastructure`: EF Core, seed data and service implementations.
- `TechYouthBpm.Api`: controllers, Swagger, CORS, cookies and startup configuration.

This is a good code review story because controllers stay mostly orchestration-only while services own rules. The state machine is isolated, so adding new workflow statuses or actions should not require rewriting controllers. EF Core provider selection is also centralized, which keeps SQLite local development and PostgreSQL/Neon shared testing compatible.

Main architectural risk: the project currently uses `EnsureCreated`/startup seeding style rather than a migration-first production path. This is acceptable for demo speed, but production-readiness would improve with EF migrations and a controlled migration command.

## Authentication and Security Review

The auth model is intentionally not JWT-only. It uses opaque access session tokens, stores only token hashes, supports HttpOnly cookies for browser flow, keeps Bearer token support for Swagger, and adds CSRF protection for cookie-authenticated mutations. Passwords use PBKDF2 hashing. Login failures increment counters and can temporarily lock accounts.

Remember-me is implemented with hashed rotating refresh tokens. On refresh, the old refresh token and old access session are revoked, then a new pair is issued. If a revoked refresh token is reused, the backend treats it as suspicious and revokes active sessions for that user.

Good production-leaning features already present:

- pending approval before login
- email verification OTP
- public email verification for users who cannot login yet
- password reset with generic response to avoid account enumeration
- admin-created temporary-password users
- session metadata: IP address, user agent, current/remembered device
- admin session revoke
- system audit for identity and access events

Remaining hardening opportunities:

- split rate limit policies per endpoint group instead of one shared `auth` policy
- add refresh-token family/device identifiers for more precise suspicious-device handling
- configure allowed CORS origins through environment variables for non-local deployments
- use production migrations instead of implicit schema creation
- add integration tests against real HTTP middleware for cookie/CSRF behavior

## BPM and Process Flow Review

The BPM model is simple and clear:

- process starts at `Pending`
- `Start` moves it to `InProgress`
- `Approve` moves it to `Completed`
- `Reject` moves it to `Rejected`

The allowed transition table lives in `ProcessStateMachine`, and invalid transitions are rejected. This is exactly the right abstraction for presentation because it proves the process is rule-driven, not scattered across button handlers.

Process start creates:

- a `ProcessInstance`
- JSON form submission data
- an open approver task
- a process audit log
- a system audit log

Task execution checks task status, assigned role/admin override, available actions and state transition. Completed tasks store `CompletedByUserId`, and the parent process receives the final status.

Recommended BPM improvement: wrap process start and task action persistence plus system audit in explicit transactions. Today the main state save and system audit save are separate service calls, which can theoretically leave business data saved without the matching system audit if the second write fails.

## Test Coverage

Current backend tests are strong for the project scope. The latest run passed:

```bash
dotnet test apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj
```

Result: `55 passed`.

Covered areas include:

- state machine valid/invalid transitions
- task authorization and closed task behavior
- audit log creation for approve/reject
- form update and validation rule replacement
- password hashing and legacy password upgrade
- hashed access and refresh tokens
- refresh token rotation and reuse detection
- pending approval login rejection
- account lockout
- admin user creation/deletion/session management
- OTP hashing and expiry
- password reset and public email verification

Main gap: most tests are service-level with EF InMemory. Add API integration tests with `WebApplicationFactory` for controller routing, cookies, CSRF, rate limiting and Swagger-facing behavior.

## Presentation Defense Notes

**Why opaque sessions instead of JWT?**  
The project needs central revoke, account approval, lockout, active session view, logout and refresh-token reuse detection. Opaque DB-backed sessions make these behaviors direct. JWT would still need server-side state for these requirements.

**Why EF Core?**  
EF Core gives strongly typed entities, LINQ queries, provider switching between SQLite/PostgreSQL and a migration path for production. It keeps persistence logic readable for code review.

**Why SQLite and Neon/PostgreSQL?**  
SQLite is fast for local demo development and needs no setup. PostgreSQL/Neon supports shared remote testing by the team. The provider is selected by configuration, not by changing code.

**How does the state machine work?**  
Allowed transitions are defined in one dictionary: `Pending + Start -> InProgress`, `InProgress + Approve -> Completed`, `InProgress + Reject -> Rejected`. Any missing transition is rejected.

**How is audit traceability handled?**  
Workflow actions write process-level audit logs. Identity, form, process and task events also write system audit logs with actor, entity, action, date and description.

**How does refresh-token rotation work?**  
Remember-me creates a long-lived refresh token hash. Each refresh revokes the previous refresh token and access session, then creates new ones. Reuse of a revoked refresh token is treated as suspicious.

**How are passwords protected?**  
Passwords are stored with PBKDF2 hashes. Raw passwords are only used for verification or temporary email delivery during admin-created account setup.

**How does Swagger work with auth?**  
Login returns a bearer token. In Swagger, paste it into Authorize as a bearer token; protected endpoints then receive `Authorization: Bearer <token>`.

## Recommended Next Improvements

### High

- Add EF Core migrations and document migration commands for SQLite/PostgreSQL.
- Add explicit transactions around form update, process start, task action and related audit writes.
- Add API integration tests for login, cookie auth, CSRF, protected endpoints and admin-only authorization.
- Move CORS allowed origins and cookie security policy fully into environment-specific config.

### Medium

- Add paged process/task endpoints for large datasets.
- Add endpoint-specific rate limits for login, register, verification, reset password and admin mutations.
- Add optimistic concurrency or row version checks for task approval to prevent double-submit races.
- Add structured audit action enums/constants instead of free-form action strings.

### Low

- Add health check endpoints for database and SMTP readiness.
- Add audit export endpoints for CSV/JSON reporting.
- Add OpenAPI examples for common auth and BPM requests.
- Add Docker Compose for API, web and PostgreSQL onboarding.

## Reviewer Summary

The backend is presentation-ready for the PDF scope. Its strongest points are layered architecture, role-aware services, state-machine-based BPM, hashed sessions/passwords, refresh-token rotation, audit traceability and meaningful test coverage. The most valuable next step is not another feature, but production hardening: migrations, transactions and API-level security tests.
