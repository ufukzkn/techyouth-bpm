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
- Services contain business rules such as login, form validation and process transitions.
- Repositories/EF Core DbContext handle persistence.
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
- `src/features/form-designer`: admin-facing dynamic form definition editor.
- `src/features/form-runner`: saved-form runner that starts process instances.
- `src/features/processes`: process list, task list, task action dialog, process detail, audit timeline and status badge components.

The App Router route group `apps/web/src/app/(workspace)` owns the authenticated shared layout. Its layout keeps sidebar and topbar mounted while only route content changes. Session verification, navigation, notifications and loading chrome are focused components under `features/app-shell/components`; route pages read their required Zustand state and import only their own feature view. This preserves code-splitting without rebuilding the workspace chrome on every navigation.

## Extensibility Strategy

- Adding a new field type should require a field renderer, validation rule and designer option, without rewriting the wizard.
- Adding a new process action should be handled in the backend state machine and exposed to the frontend as available task actions.
- Adding a new role should be centralized in auth/authorization rules and menu visibility logic.
- Adding community-specific access should use `CommunityRolePermission` records instead of hardcoding every role in controllers or frontend screens.
- Strengthening auth should preserve centralized session handling: password hashing, token hashing, session expiry and future remember-me/refresh-token behavior belong in the auth/session boundary.
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
