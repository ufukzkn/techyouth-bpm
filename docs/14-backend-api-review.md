# Backend API Review

## Scope

This report reviews the backend/API side of the TechYouth BPM project: .NET REST API structure, authentication, authorization, persistence, BPM/process/task flow, Swagger, tests and production-readiness. It intentionally does not review frontend UI details.

## Current Backend Fit Against PDF Requirements

The backend satisfies the core PDF expectations well:

- **.NET 8 REST API:** implemented under `apps/api` with thin controllers and service-driven business logic.
- **Swagger/OpenAPI:** enabled in development through Swashbuckle with bearer token support.
- **Authentication and user/role storage:** users, roles, statuses, password hashes, active sessions, refresh tokens and lockout fields are stored in EF Core entities.
- **Authorization:** service methods enforce platform role, community scope and operation permission checks for management, forms, process visibility and task execution.
- **EF Core database layer:** `AppDbContext` supports SQLite by default and PostgreSQL through Npgsql for Neon/shared database testing.
- **Form persistence:** logical forms plus immutable published versions, pages, fields, options and validation rules are persisted.
- **Community access model:** communities, custom community roles, role permissions and user memberships are persisted.
- **Submission and process start:** submitted form data is validated and stored as namespaced JSON variables, then a pinned published workflow version creates the first reachable task.
- **Task and BPM flow:** typed workflow graphs support start, user task, exclusive gateway, completed/rejected end and team swimlane nodes. Tasks support claim/release plus approve/reject/complete/escalate/send-back.
- **Audit traceability:** process audit logs and system audit logs record who performed key actions.
- **Backend validation:** required, type-based and dependent validation are centralized in backend services.

Overall, the backend is beyond a simple demo API. It has a layered structure and enough security/audit behavior to be defended as an extensible BPM foundation.

## Architecture Assessment

The project uses a clean four-layer split:

- `TechYouthBpm.Domain`: entities and enums.
- `TechYouthBpm.Application`: DTOs, service interfaces and workflow state machine.
- `TechYouthBpm.Infrastructure`: EF Core, seed data and service implementations.
- `TechYouthBpm.Api`: controllers, Swagger, CORS, cookies and startup configuration.

This is a good code review story because controllers stay mostly orchestration-only while services own rules. `ProcessStateMachine` isolates high-level lifecycle transitions; `DynamicWorkflowEngine`, `IProcessGraphValidator` and `TaskAssignmentResolver` isolate graph execution, validation and candidate resolution. EF Core provider selection is centralized, which keeps SQLite local development and PostgreSQL/Neon shared testing compatible.

The backend now uses EF Core migrations instead of `EnsureCreated`. Startup applies migrations through `Database.MigrateAsync`, then runs deterministic seed data. This gives a stronger production-readiness story while keeping SQLite local demos and PostgreSQL/Neon shared testing on the same EF model.

The authorization model is now split between platform role and community permission data. `SuperAdmin` handles platform-wide administration. Everyday BPM access is represented by `CommunityRolePermission` records such as `Forms.Create`, `Processes.Start` or `Tasks.Act`. This is more extensible than adding a new enum value for every business job title.

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
- extend concurrency protection from candidate claim to competing task-action submissions
- move production CORS/cookie policy into environment-specific deployment configuration

## BPM and Process Flow Review

The backend now has two deliberately separated layers:

- `ProcessStateMachine` protects `Pending`, `InProgress`, `Completed`, `Rejected` and `Escalated` lifecycle changes.
- `DynamicWorkflowEngine` follows the published node graph, evaluates typed conditions and records each `ProcessStepExecution`.

A logical form and process definition can have multiple versions. Draft versions are editable; published versions are immutable. Every process instance pins the exact workflow and form versions it started with, so publishing a new version cannot silently change in-flight work.

Dynamic process start creates the process, variables, start-step execution, first task, candidate notification and audit in one transaction. A user task may target the starter, a specific user, a team, a community role or a team-plus-role intersection. Candidate-pool tasks require claim; `ClaimVersion` optimistic concurrency ensures two stale clients cannot both win.

Task execution validates status, claim ownership, live candidate eligibility, published action and optional task-form data. `Approve`, `Reject`, `Complete`, `Escalate` and `SendBack` follow graph edges; gateways read safe namespaced values such as `start.amount` instead of executing arbitrary code. Conditions cannot read future task outputs and their value/operator types must match the referenced form field. A 100-hop guard stops accidental automatic loops.

Process listing now uses EF Core projection to return only summary fields such as process id, form name, status and dates. Full tasks and audit logs are loaded only by the process detail endpoint, which prevents the board from pulling large related object graphs from PostgreSQL/Neon. Detail loading uses split queries so related collections do not create one oversized joined result set.

Form update, process start and task action now use explicit EF Core transactions. Form fields, process/task state, notifications and audit writes commit together; a downstream audit failure rolls the full business mutation back. Dedicated failure-injection tests verify these boundaries.

## Test Coverage

Current backend tests are strong for the project scope. The latest run passed:

```bash
dotnet test apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj
```

Result: `194 passed` (latest full verification).

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
- personal/community/global workflow scope authorization
- Okan, Fatih, Approver and Quaresma candidate-pool HTTP scenarios
- process/task cache contract inputs and deterministic six-process community seed
- relational constraints through SQLite instead of EF InMemory
- real HTTP login, cookies, CSRF, Bearer and logout behavior
- refresh-token rotation and reuse through the controller pipeline
- rate limiting with standard `429` responses
- SuperAdmin and community-admin authorization boundaries
- Swagger bearer metadata and form-to-process endpoint smoke flow
- transaction rollback for form, process and task writes
- SQLite migration/startup smoke and opt-in PostgreSQL/Neon migration smoke
- form and workflow version publication/immutability
- graph reachability, gateway/default edge and form-binding validation
- dynamic runtime routing, send-back attempts and task-form validation
- team/role candidate resolution and deterministic stale-snapshot claim competition
- real HTTP workflow create, publish, runnable-list, start and complete flow
- deterministic seeded Transfer workflow validation and first-task creation
- real HTTP transfer chain with dependent/file validation, team-role candidate checks, claim/release, task-form rejection, approve/reject, notifications and both audit layers
- live role and team membership reevaluation with the same already-issued opaque session token

The remaining test gap is browser-level E2E coverage. The API now has service, relational persistence and `WebApplicationFactory` HTTP coverage; Playwright should next protect the critical login -> form -> process -> task -> audit user journey.

## Presentation Defense Notes

**Why opaque sessions instead of JWT?**  
The project needs central revoke, account approval, lockout, active session view, logout and refresh-token reuse detection. Opaque DB-backed sessions make these behaviors direct. JWT would still need server-side state for these requirements.

The token contains no role/team claims. Protected requests reload active permissions and memberships from the database, so access changes apply on the next request. The normal browser uses `/api/auth/browser-login` and never persists the raw token; HttpOnly cookies carry access/refresh secrets, while `/api/auth/login` preserves Bearer compatibility for Swagger and explicit API clients.

**Why custom community roles?**
Fixed enum roles are too rigid for BPM teams. Community roles let a team create `Lojistik Gorevlisi` or `Form Tasarimcisi` by selecting permissions instead of changing code.

**Why EF Core?**  
EF Core gives strongly typed entities, LINQ queries, provider switching between SQLite/PostgreSQL and a migration path for production. It keeps persistence logic readable for code review.

**Why SQLite and Neon/PostgreSQL?**  
SQLite is fast for local demo development and needs no setup. PostgreSQL/Neon supports shared remote testing by the team. The provider is selected by configuration, not by changing code.

**How does the state machine work?**  
Allowed lifecycle transitions remain in one dictionary. The separate dynamic engine follows a validated published graph for task-to-task routing, gateways, send-back and end nodes. This keeps lifecycle policy independent from process shape.

**Why a custom engine instead of embedding Camunda?**
The PDF asks for BPM concepts and extensibility, not BPMN deployment. React Flow provides the modeling experience while a small typed .NET runtime keeps the supported nodes, authorization and persistence visible in code review. Camunda and Kissflow are design references rather than runtime dependencies.

**How are running processes protected from later edits?**
They reference immutable published form and process-definition versions. New edits produce new drafts/versions; existing instances continue with their pinned snapshots.

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

- Move CORS allowed origins and cookie security policy fully into environment-specific config.
- Expand browser E2E coverage for cookie bootstrap, silent refresh and cross-role navigation.
- Add Playwright coverage for the critical cross-role BPM journey.

### Medium

- Add structured audit action enums/constants instead of free-form action strings.
- Extend HTTP integration coverage when new community-scoped mutations are added.

### Low

- Add health check endpoints for database and SMTP readiness.
- Add audit export endpoints for CSV/JSON reporting.
- Add OpenAPI examples for common auth and BPM requests.
- Add automated CI checks for backend tests and frontend lint/build.

## Reviewer Summary

The backend is presentation-ready for the PDF scope. Its strongest points are layered architecture, immutable versioned definitions, a validated dynamic workflow runtime, community/team-aware assignment, hashed sessions/passwords, transactional audit traceability, migrations and real HTTP tests. The most valuable next step is browser-level E2E and CI automation, followed by advanced BPM nodes such as parallel gateways and timers.
