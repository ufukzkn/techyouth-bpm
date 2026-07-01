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

Navigation behaves as a multi-screen app inside the authenticated shell. The current implementation uses a route-like `?view=` query parameter for the active screen, so refresh and browser back/forward keep the user in the expected workspace area without returning to hash-scroll anchors.

Implemented frontend folders:

- `src/lib`: shared API client and types.
- `src/features/session`: Zustand session/theme store.
- `src/features/auth`: login view and demo-user fallback.
- `src/features/app-shell`: role-aware navigation and authenticated layout.
- `src/features/forms`: shared field metadata, field renderer, form value helpers and frontend validation.
- `src/features/form-designer`: admin-facing dynamic form definition editor.
- `src/features/form-runner`: saved-form runner that starts process instances.
- `src/features/processes`: process list, task list, task action dialog, process detail, audit timeline and status badge components.

## Extensibility Strategy

- Adding a new field type should require a field renderer, validation rule and designer option, without rewriting the wizard.
- Adding a new process action should be handled in the backend state machine and exposed to the frontend as available task actions.
- Adding a new role should be centralized in auth/authorization rules and menu visibility logic.
- Changing persistence provider should stay inside Infrastructure configuration and EF Core package setup.
- Adding a new screen should update the navigation model, route/screen ownership and team presentation notes.
