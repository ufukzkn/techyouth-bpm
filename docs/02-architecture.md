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

## Frontend Architecture

The frontend is organized by domain features:

- Auth/session
- Dashboard
- Form designer
- Form runner
- Processes and tasks
- Settings

Pages compose feature components. API calls are made through service modules. Zustand stores only app-wide state such as the active session and theme.

Implemented frontend folders:

- `src/lib`: shared API client and types.
- `src/features/session`: Zustand session/theme store.
- `src/features/auth`: login view and demo-user fallback.
- `src/features/app-shell`: role-aware navigation and authenticated layout.

## Extensibility Strategy

- Adding a new field type should require a field renderer, validation rule and designer option, without rewriting the wizard.
- Adding a new process action should be handled in the backend state machine and exposed to the frontend as available task actions.
- Adding a new role should be centralized in auth/authorization rules and menu visibility logic.
