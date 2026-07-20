# Requirements From PDF

Source: `docs/TechYouth School 2. Donem Proje Dokumani.pdf`

## Project Goal

The project measures whether the team can build a frontend architecture that can work with a real backend, manage a multi-step wizard form, apply validations, handle state management, show UI/UX awareness, and understand basic BPM/workflow concepts.

## Required Frontend Scope

- Next.js based multi-screen application.
- Login screen that stores user and role information in global state.
- Authenticated app layout with header, active user information, left menu and content area.
- Main menu items:
  - Dashboard
  - Form Design
  - Processes / My Tasks
  - Settings
- Three-step form flow:
  - Design a form definition.
  - Start the designed form by entering data.
  - Show form and process details.
- Form designer must support custom components and field properties:
  - Label
  - Type
  - Required / optional
  - Input, select, checkbox and similar field types
- Validation:
  - Required fields
  - Type-based validation
  - Dependent validation, for example: if field A has a selected value, field B becomes required.
- Submit flow must show loading, success and error states.
- JSON output of submitted form data must be visible on screen or in console.

## Required Backend Scope

- .NET 8 or newer REST API.
- Authentication/login mechanism.
- User and role storage.
- Authorization checks for protected actions.
- Store frontend form definitions.
- Store form submission data as JSON.
- Start a process from a selected form.
- Create a process instance with initial status and first task.
- Track process status and actions.
- Users can list assigned tasks and execute actions such as approve/reject.
- Backend validates required fields, field types and simple dependent rules.
- Swagger/OpenAPI integration.
- ORM with EF Core.
- Database: SQLite, PostgreSQL or MSSQL.

## Extended Scope Included In This Implementation

The PDF marks some items as bonus or optional. For this project, these are treated as quality targets rather than "nice to have" items whenever they help the final code review story.

- Role-based UI.
- Role-based backend checks.
- Dashboard lists by status.
- i18n/language support.
- Dark/light theme.
- Responsive design.
- Drag/drop field ordering in form designer.
- State transition audit logs.
- Transaction handling around form/process updates.
- Unit tests for state machine transitions.
- Community roles and team membership as an extensible foundation for user/group task assignment.
- Versioned multi-page forms with immutable published snapshots.
- Visual drag/drop workflow design with start, task, gateway, end and team swimlane nodes.
- Person, team, community-role and team-plus-role task assignment with candidate claim.
- Task forms, conditional routing, send-back and step-level execution history.

## Bonus And Optional Status

`Core scope is largely exceeded` means the required baseline is implemented and several optional items were extended beyond their minimum form. It does not mean a known PDF bonus was silently skipped.

| PDF bonus / optional item | Status | Implementation |
| --- | --- | --- |
| Drag/drop field addition and ordering | Completed | Desktop palette drag/drop, mobile palette addition and page/field ordering use `@dnd-kit`. |
| BPM process modeling | Completed and extended | Versioned workflow definitions model start, user task, gateway, completed/rejected end and swimlane nodes. |
| Drag/drop process design | Completed | `/workflows` uses `@xyflow/react` for a persisted visual graph editor. |
| Bind a form to a process step | Completed | Published form versions can be pinned to the Start node and individual User Task nodes. |
| Multiple workflow actions | Completed | Approve, Reject, Complete, SendBack and Escalate are validated against node action edges. |
| Role/user assignment | Completed and extended | Specific user, team, community role and team-plus-role candidate pools are supported. |
| Dynamic flow stored and executed by backend | Completed | Published graph JSON is validated, version-pinned and interpreted by the custom .NET workflow runtime. |
| Workflow-engine execution | Completed | Candidate resolution, claim/release, gateway routing, task forms and process advancement are backend-owned. |
| Role-based UI and actions | Completed | Navigation, routes and actions are permission-aware; backend repeats every security decision. |
| Role-based dashboard | Completed and extended | Personal/community/global scopes expose pending tasks and active/completed processes according to permission. |
| State transition audit | Completed | Process audit, step execution and categorized system audit are stored separately. |
| Transaction handling | Completed | Form update, process start and task action commit business state, notification and audit atomically. |
| State-machine unit tests | Completed | Transition, graph, runtime, rollback, claim and HTTP action tests are present. |
| Layered architecture | Completed | API, Application, Domain and Infrastructure layers are used; EF Core DbContext supplies repository/unit-of-work behavior. |
| Clean commit history | Ongoing repository discipline | Feature ownership and scoped commits are used; this is a Git-history quality criterion rather than an application feature. |
| i18n, theme and responsive UX | Completed for current scope | TR/EN dictionaries, light/dark themes and desktop/mobile layouts are implemented. |

Playwright browser automation and CI quality gates are implemented. Binary file
storage, parallel gateways and timer jobs remain valuable product/production
extensions rather than missing core PDF requirements.

## Production-Readiness Work Included

These items are not required by the PDF, but they strengthen the final review story:

- PostgreSQL/Neon provider and migration smoke flow for shared remote database development.
- Separate local SQLite and cloud Neon Docker Compose stacks.
- Production deployment hardening for HTTPS cookie settings, SMTP domain verification and environment-specific secrets.
- GitHub Actions checks for backend/frontend tests, lint/build, PostgreSQL migrations, Docker images and Playwright browser journeys.
- Layered liveness/readiness endpoints, RFC 7807 errors, correlation IDs and safe production JSON logs.

Remaining delivery work is final accessibility/real-device QA and environment-specific
deployment/observability integration. Cookie-only browser auth is implemented:
raw access/refresh tokens are no longer returned to or persisted by the normal
web client.

## Important Evaluation Point

The evaluator explicitly expects more than "a working UI". The architecture must be readable, modular, and easy to extend without breaking unrelated parts of the system.

Team management and the visual workflow designer extend rather than replace the required baseline. Legacy form-based process start remains compatible, while new published workflow versions add multi-step routing, task forms, candidate assignment and a complete execution history.
