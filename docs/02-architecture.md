# Architecture

## Monorepo Layout

```text
apps/
  api/     .NET 8 REST API
  web/     Next.js frontend
docs/      Requirements, architecture, BPM, API, code review and team notes
```

## Backend Architecture

The API follows a lightweight layered architecture:

- Controllers expose HTTP endpoints and translate requests/responses.
- Application contracts describe use cases without depending on EF Core or HTTP.
- Infrastructure services implement login, validation, authorization and process transitions.
- EF Core `DbContext`/`DbSet` provide repository and unit-of-work behavior inside Infrastructure.
- Domain models represent users, roles, forms, processes, tasks and audit logs.

This keeps process logic independent from HTTP so it can be tested and extended.

Persistence is selected in the Infrastructure layer through configuration:

- `Sqlite` for fast local demos and offline development.
- `PostgreSql` for shared team development through a hosted PostgreSQL service such as Neon.

The rest of the application depends on EF Core and service interfaces, so changing the database provider does not change controllers, workflow rules or frontend API contracts.

## Frontend Architecture

The frontend is organized by domain features:

- Auth/session
- Dashboard
- Form designer
- Form runner
- Processes and tasks
- Settings

Pages compose feature components. API calls are made through service modules. Zustand stores only app-wide state such as the active session and theme.

Navigation behaves as a multi-screen app inside the authenticated shell. The current implementation uses real route paths such as `/dashboard`, `/forms`, `/runner`, `/processes`, `/tasks` and `/settings`, so refresh/direct links keep the user in the expected workspace area without returning to hash-scroll anchors.

Implemented frontend folders:

- `src/lib`: shared API client and types.
- `src/features/session`: Zustand session/theme store.
- `src/features/auth`: login view and demo-user fallback.
- `src/features/app-shell`: role-aware navigation, authenticated layout, dashboard/settings/access/log views and shared shell components.
- `src/features/forms`: shared field metadata, field renderer, form value helpers and frontend validation.
- `src/features/form-designer`: admin-facing dynamic form editor plus a separately tested pure page/field ordering and validation model.
- `src/features/form-designer/FormFieldEditor.tsx` and `FormVersionActions.tsx`: field/property editing and draft/publish/archive actions extracted from the controller view.
- `src/features/form-runner`: saved-form runner that starts process instances.
- `src/features/management`: user filters, create/detail panels and the `useUserManagement` orchestration hook.
- `src/features/processes`: process list, task list, task action dialog, process detail, audit timeline and status badge components.
- `src/features/workflows`: React Flow canvas, typed graph adapter, Zustand draft store, node inspector and publish validation.

The App Router route group `apps/web/src/app/(workspace)` owns the authenticated shared layout. Its layout keeps sidebar and topbar mounted while only route content changes. Session verification, navigation, notifications and loading chrome are focused components under `features/app-shell/components`; route pages read their required Zustand state and import only their own feature view. This preserves code-splitting without rebuilding the workspace chrome on every navigation.

## Extensibility Strategy

- Adding a new field type should require a field renderer, validation rule and designer option, without rewriting the wizard.
- Adding a new process action should be handled in the backend state machine and exposed to the frontend as available task actions.
- Adding a new role should be centralized in auth/authorization rules and menu visibility logic.
- Adding community-specific access should use `CommunityRolePermission` records instead of hardcoding every role in controllers or frontend screens.
- Strengthening auth should preserve centralized session handling: password hashing, token hashing, session expiry and rotating remember-me/refresh-token behavior belong in the auth/session boundary.
- Changing persistence provider should stay inside Infrastructure configuration and EF Core package setup.
- Adding a new screen should update the navigation model, route/screen ownership and team presentation notes.

## Authorization Model

The first access model used fixed enum roles. The current model keeps enum roles for platform-level behavior and adds data-driven community roles for business permissions:

- `SuperAdmin` is a platform role and can manage all communities.
- `Community`, `CommunityRole`, `CommunityRolePermission` and `UserCommunityMembership` model team-specific authorization.
- A user has one active community in v1, but the membership table keeps the model ready for multiple communities later.
- Forms, process instances and tasks are scoped to a community.
- Navigation reads `UserDto.permissions`; backend services still enforce the same permissions server-side.

Detailed permission notes are tracked in `docs/16-community-permission-model.md`.

## Focused Application Boundaries

Identity is physically split as well as contractually separated:

- `AuthenticationService`: login, refresh and current-user resolution.
- `RegistrationService`: registration and public email verification.
- `AccountService`: profile, password and recovery operations.
- `SessionService`: logout, session listing and revoke.
- `UserAdministrationService`: paged user management and admin operations.
- `ICommunityService`: community metadata, lifecycle, invite code and summary.
- `ICommunityRoleService`: role templates and community-role CRUD.

Controllers resolve the focused Application interfaces directly. The aggregate
`AuthService`/`IAuthService` façade is not registered in production DI; it remains
only as compatibility for older service tests.

`AuthenticatedUserLoader` is the shared live authorization boundary. It resolves
the hashed opaque session plus current role permissions, community state and team
memberships from the database on every protected request. Those mutable values are
deliberately not cached.

This is a pragmatic layered architecture, influenced by Clean Architecture dependency direction: Domain is independent, Application defines contracts, Infrastructure implements them and API exposes HTTP. A generic repository is intentionally avoided because EF Core `DbContext` already supplies repository/unit-of-work behavior.

## Operations And Quality Boundaries

Operational health follows the same dependency direction:

- Application owns the neutral `ISystemReadinessService` report contract.
- Infrastructure implements database connectivity, pending migration and
  exactly-one-active-SuperAdmin checks.
- API maps `/health/live` and `/health/ready` without exposing connection details.

Unexpected HTTP failures use RFC 7807 `ProblemDetails` with a safe trace and
validated `X-Correlation-ID`. Development keeps readable console logs; production
uses the built-in JSON console formatter. Request logs contain method, path,
status and duration, never token, cookie, password, e-mail or form payloads.
`SystemAuditLog` remains the durable business trail; `ILogger` is the technical
operational trail.

GitHub Actions protects these boundaries. Every push runs backend/frontend
tests, lint, build and whitespace checks; `master` pushes and manual runs add
Playwright, PostgreSQL migration smoke and Docker build/config validation.

## Team Extension Boundary

Teams are community-scoped operational groups, not a second role system. A user can have multiple `TeamMembership` records while retaining one community role. `Takimsiz` is computed from missing active memberships. The workflow runtime targets a process starter, person, team, community role or team-plus-role intersection without changing the platform-level access model. See `docs/18-dynamic-workflow-and-team-architecture.md`.

## Dynamic Workflow Boundary

The workflow stack keeps editor, contract and runtime responsibilities separate:

- React Flow owns canvas interaction and visual node state.
- `apiGraphAdapter` converts editor nodes to the provider-neutral `ProcessGraphDto` contract.
- `ProcessGraphValidator` rejects incomplete assignments, missing routes, unsafe cycles and cross-community references before publish.
- `DynamicWorkflowEngine` navigates the pinned graph and creates task/step records.
- `ProcessStateMachine` continues to own top-level lifecycle transitions.
- EF Core transactions commit process, task, variables, notification and audit changes atomically.

This is still a layered architecture: Domain contains entities/enums, Application contains DTOs/contracts, Infrastructure implements persistence/runtime, and API exposes controllers. The React Flow library never leaks into backend contracts.
