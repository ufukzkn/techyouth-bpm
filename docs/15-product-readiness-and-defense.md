# Product Readiness And Defense

## Executive Assessment

The project is presentation-ready for the TechYouth BPM scope. It is no longer a static UI prototype: it has a Next.js route-based workspace, a .NET 8 REST API, EF Core persistence, permission-aware access, community/custom role management, dynamic form definitions, process/task execution, state-machine transitions, audit trails and security-focused identity flows.

The strongest defense is extensibility. Forms and workflows are versioned data, API calls are isolated in a client layer, focused Application contracts keep controllers thin, EF Core migrations own schema evolution, and documentation tracks feature ownership. Critical writes are transactional; 190 backend tests cover cookie/CSRF, Bearer, refresh, rate limit, live role/team reevaluation and a realistic transfer workflow across start, claim, release, approve and reject. Candidate claims use optimistic concurrency. The main remaining production gaps are Playwright E2E, CI, cookie-only browser token transport, parallel/timer workflow nodes and final responsive/accessibility QA.

The dependency direction is healthy: Domain is independent, Application references Domain, Infrastructure implements Application contracts, and API composes the layers. Next.js routes share one persistent workspace layout while feature folders own their views. Physical modularity is not finished everywhere: `AuthService`, `DatabaseSeeder`, `DemoWorkflowSeeder`, `FormDesignerDraft` and `UsersAndRolesView` are still large files. They are maintainable behind focused interfaces/components today, but splitting their implementations is the clearest next readability improvement.

## PDF Compliance Matrix

| Requirement | Current Evidence | Readiness | Gaps / Next Action |
| --- | --- | --- | --- |
| Next.js multi-screen frontend | Real routes exist for dashboard, versioned forms, runner, workflow canvas, processes, tasks, teams, inbox, management, logs and settings. | Strong | Final mobile/tablet QA pass. |
| Login with global user/role state | Zustand session store, role-aware shell, register, password reset, remember-me and forced password change. | Strong | Finish full backend error i18n mapping. |
| Authenticated layout | Header, fixed/collapsible sidebar, active session popover and role-filtered navigation. | Strong | ARIA checks for icon-only controls. |
| Dashboard | API-backed metrics and role-aware shortcuts. | Strong | Optional security summary widget. |
| Form design | Multi-page draft/publish versions, field types, validations, page/field drag-drop and JSON preview. | Strong | Add browser E2E. |
| Form runner | Version-pinned rendering, step validation, payload preview and compatible workflow selection. | Strong | Add browser E2E. |
| Required/type/dependent validation | Frontend helpers plus backend validation for process start. | Strong | Keep backend as final source of truth. |
| Loading/success/error states | Implemented in auth, forms, dashboard refresh, process/task and management flows. | Good | Normalize every remaining backend error message. |
| .NET 8 REST API | Layered API with controllers, services, EF Core, Swagger and WebApplicationFactory integration tests. | Strong | Add automated CI execution. |
| User/role storage and authorization | Users, roles, statuses, sessions, communities, custom roles, permissions and scope checks. | Strong | Add final permission matrix screen/doc for demo. |
| Form definition persistence | EF entities, migrations and create/update/list/detail endpoints. | Strong | Add migration review to release checklist. |
| JSON submission data | Process start stores submitted form data as JSON and displays it in detail. | Strong | Consider JSON schema/versioning later. |
| Process/task workflow | Visual graph publish, process start, candidate claim, task forms, approve/reject/complete/escalate/send-back and step history. | Strong | Parallel/timer nodes are intentionally deferred. |
| BPM runtime | `ProcessStateMachine` controls lifecycle; `DynamicWorkflowEngine` routes pinned Start/UserTask/Gateway/End graphs. | Strong | Add parallel gateway only through a new schema version. |
| Audit trail | Process audit and system audit show actor, action, target and timestamp. | Strong | Add export for filtered audit results. |
| Swagger/OpenAPI | Development Swagger with Bearer token support. | Strong | Add OpenAPI examples for demo requests. |
| EF Core + database | SQLite default, PostgreSQL/Neon optional via configuration and EF Core migrations. | Strong | Reset/recreate old demo DBs created before migrations. |
| Bonus: i18n/theme/responsive/drag-drop | TR/EN dictionary, dark/light theme, drawer nav, dnd-kit form ordering and React Flow workflow canvas. | Strong | Final localization and accessibility pass. |

## User Scenario Review

### Scenario 1: Admin Creates A Form

Admin signs in, opens Form Design, loads or creates a form, adds input/select/checkbox/date fields, configures required and dependent validation, reorders fields with drag/drop and saves the definition. This proves the app stores forms as data instead of hardcoding one screen.

Demo risk: explain that backend validation still protects the process start even if frontend validation is bypassed.

### Scenario 2: User Starts A Process

User signs in, opens Form Runner, selects a published form plus compatible published workflow, fills values, sees JSON payload preview and starts a version-pinned process. The backend validates the exact form version, stores namespaced variables, creates the first graph task and writes step/audit records atomically.

Demo risk: prepare one dependent-validation failure before showing the successful submit.

### Scenario 3: Approver Approves Or Rejects

Approver opens My Tasks, claims a team/role candidate task, fills its optional task form, enters a note and runs one of the node's published actions. The backend rechecks candidate eligibility, claim ownership, form payload and graph route. Process detail shows attempts, actors, outputs and timeline.

Demo risk: mention that invalid transitions are rejected by the state machine, not hidden only by UI buttons.

### Scenario 4: Admin Inspects Audit And Security

Admin opens Management and Logs, searches users or actions, inspects related history, views active sessions, revokes sessions, approves pending accounts, creates community roles and reviews identity/process events. This proves "who did what" traceability.

Demo risk: avoid dumping every log; use search/filter to show production-aware paging.

## Likely Reviewer Questions And Answers

**Why Next.js?** It gives TypeScript, route-based screens, production build tooling and a clean App Router model for the authenticated workspace.

**Why .NET 8?** The PDF asks for .NET 8 or newer. It gives a strong REST API stack, dependency injection, middleware, Swagger and EF Core integration.

**Why EF Core?** EF Core keeps persistence typed and testable, supports SQLite/PostgreSQL provider switching and gives a migration path.

**Why SQLite and Neon?** SQLite is frictionless for local demos. Neon/PostgreSQL allows shared remote testing without changing application code.

**Why Zustand?** Session, theme and language are lightweight global states. Zustand avoids heavier Redux-style boilerplate.

**Why dnd-kit?** It is a focused React drag/drop library used only for form field ordering, with move up/down controls as fallback.

**Why React Flow?** `@xyflow/react` provides proven node/edge canvas interaction, pan/zoom and custom nodes. We keep it in the presentation layer and translate it to our own typed graph DTO, so the backend is not coupled to a UI library.

**Why not install Camunda?** The PDF asks for BPM concepts and a dynamic flow, not an external engine. A typed in-project runtime makes form binding, team permissions, transaction behavior and code review visible. Camunda/Kissflow are UX and modeling references.

**How does Swagger auth work?** Login returns a Bearer token for development. Paste it into Swagger Authorize. The API also issues HttpOnly cookies and supports CSRF-protected cookie mutations. The current demo frontend still persists and sends the bearer token for compatibility; production browser hardening will remove that storage path and use the cookie session only.

**Why opaque sessions instead of JWT?** This BPM system needs central revoke, pending approval, lockout, session visibility and suspicious refresh reuse detection. Opaque DB-backed sessions make those direct. JWT would still need server-side state for these features.

**Why community/custom roles?** Real BPM systems usually need team-specific permissions. `SuperAdmin` manages the platform, while `CommunityRolePermission` records let each community define roles such as form designer, process starter or logistics operator without new code.

**Why are teams separate from roles?** A team answers where work is performed, while a role answers what a user may do. Keeping them separate allows one user to work in multiple operational teams without duplicating permissions or inventing a second authorization system.

**How are passwords protected?** Passwords are stored as PBKDF2 hashes. Raw passwords are only used at verification time or as temporary admin-created credentials before forced change.

**How do refresh tokens work?** Remember-me creates a long-lived refresh token hash. Each refresh rotates it, revokes the old token/session and creates a new pair. Reuse of a revoked refresh token is treated as suspicious.

**How does the state machine work?** `ProcessStateMachine` controls lifecycle status, while `DynamicWorkflowEngine` follows the published node graph. A task action must be both available on the node and connected to a valid edge; otherwise it fails without partial writes.

**How did you optimize process listing?** The process board uses a projected summary query and only loads id, form name, status and dates. Tasks, submitted JSON and audit history are loaded from the detail endpoint when the user opens a specific process. This avoids pulling large related graphs for every row.

**Why audit logs?** Process audit explains BPM decisions. System audit explains identity/access/form/process/task actions. Together they satisfy traceability.

**How is i18n handled?** The frontend uses a shared TR/EN dictionary and maps known API errors to localized messages. Remaining raw backend messages should be mapped before final demo.

**Is Docker ready?** Yes. Separate Compose stacks run the SQLite local demo or the Neon-backed cloud flow with the same API/web images, migrations and deterministic seed. They share host ports and are intentionally started one at a time.

## Recommended Next Work

### Must

- Run Playwright E2E for the seeded Transfer Talep Akisi across at least two task actors.
- Remove the raw bearer token from frontend persistence and make normal browser requests cookie-only; keep Bearer for Swagger/dev and explicit API clients.
- Finish i18n mapping for remaining dynamic workflow/backend validation errors.
- Run final responsive and accessibility QA on form designer, workflow canvas, management and process detail.

### Should

- Keep Docker local/cloud startup and secret onboarding notes current as configuration changes.
- Add Playwright E2E for login, form design, process start, task action and audit review.
- Add audit export for filtered logs.
- Add a compact permission matrix for SuperAdmin, Topluluk Admin and custom community roles.
- Add health checks for database and SMTP readiness.

### Could

- Add CI for frontend lint/build and backend tests.
- Add OpenAPI request examples for the demo endpoints.
- Add UI smoke tests for route access, form validation and login/session flows.
- Add a small security dashboard for locked accounts, pending approvals and active sessions.

## Team Presentation Fit

- Ufuk should present access, session, dashboard, management, audit and security decisions.
- Ozgun should present form modeling, designer, runner, validation and drag/drop.
- Cagdas should present process start, tasks, state machine, approve/reject and process audit.
- The final demo should follow one story: login -> publish form -> draw/publish workflow -> start process -> claim/complete task -> inspect pinned process history -> inspect system audit.
