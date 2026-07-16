# Team Work Split

The team split is flow-based. Each person owns one user journey end to end, including UI, API usage, state, tests, documentation and presentation.

## Ownership Summary

| Owner | Flow | Main Outcome |
| --- | --- | --- |
| Ufuk | Access, shell, dashboard and management | User can enter the app, see permission-aware navigation and manage community access. |
| Ozgun Saz | Form design, versioning and form run | Authorized users can build, publish and run multi-page community forms with validation. |
| Cagdas Kaplan | Workflow design, runtime, tasks and review | A published workflow routes versioned forms through permission-aware tasks, gateways, claims and audit history. |

## Ufuk: Access, Shell And Dashboard Flow

Production tasks:

- Login page and login error/loading states.
- Session state with active user, token and role.
- Protected app shell after login.
- Header with active user information.
- Left menu filtered by role.
- Left menu filtered by active community permissions.
- Dashboard cards for pending, in-progress and completed work.
- Community name and community role context on the dashboard.
- Dashboard shortcuts that route users to role-available workflow screens.
- Session expiry visibility in the top bar and settings screen.
- Settings page draft for theme and user preferences.
- Community-scoped team and multi-team membership management.
- Virtual `Takimsiz` user view, team audit and membership notifications.
- User-detail team assignment and the personal read-only `Takimlarim` roster.

Backend/API touchpoints:

- `POST /api/auth/login`
- `GET /api/auth/me`
- Role data returned from auth response.
- Community and permission data returned from auth response.
- Community and custom role APIs.
- Team and team-membership APIs.
- Safe member roster and on-demand user membership APIs.

Frontend files and areas:

- `features/auth`
- `features/session`
- `features/app-shell`
- Dashboard and settings sections.
- `docs/10-ufuk-access-shell-flow.md`

Tests and review notes:

- Login success and failure scenarios.
- Role-based menu visibility.
- Permission-based menu visibility.
- Role-aware dashboard shortcut visibility.
- SuperAdmin vs community-admin management visibility.
- Theme/session persistence behavior.
- Expired/invalid session handling.

Presentation angle:

How users enter the system and how community permissions shape the interface.

## Ozgun Saz: Form Design And Form Run Flow

Production tasks:

- Form designer screen.
- Custom field components for input, select, checkbox, number, email and date.
- Field properties: key, label, type, required, sort order and options.
- Field ordering with drag/drop or simple move controls.
- Form definition JSON preview.
- Community-scoped form visibility and create/update permissions.
- Dynamic form runner for filling a designed form.
- Required, type-based and dependent validation.
- Submit loading, success and error states.
- Logical form definitions with editable drafts and immutable published versions.
- Multi-page form ordering and page-by-page runner validation.
- Start forms and optional task forms bound to workflow nodes.

Backend/API touchpoints:

- `GET /api/forms`
- `POST /api/forms`
- `GET /api/forms/{id}`
- `GET/POST /api/forms/{id}/versions`
- `PUT /api/forms/{id}/versions/{versionId}`
- `POST /api/forms/{id}/versions/{versionId}/publish`
- `POST /api/forms/{id}/versions/{versionId}/archive`
- Backend form data validation used when starting a process.
- Form APIs now enforce `Forms.View`, `Forms.Create` and `Forms.Update`.

Frontend files and areas:

- `features/form-designer`
- `features/form-runner`
- Shared field renderer and validation helpers.
- `features/forms` version adapters and page helpers.

Tests and review notes:

- Required field validation.
- Type validation such as email and number.
- Dependent rule example: if request type is purchase, approval note becomes required.
- Adding a new field type should not require rewriting the whole wizard.

Presentation angle:

How a form is represented as data and rendered dynamically instead of being hardcoded.

Current ownership note:

Ozgun owns the form UX, version contract, multi-page editing/running and task-form rendering story. Published versions are immutable and running processes remain pinned to their selected form snapshots.

## Cagdas Kaplan: Process, Tasks And Review Flow

Production tasks:

- Create, validate and publish typed process graphs.
- Start process from a published workflow version and submitted form data.
- Process list and process detail.
- My tasks list.
- Candidate claim/release and direct assignment behavior.
- Approve/reject/complete/send-back actions with optional task forms.
- Exclusive gateway routing and default edges.
- Step execution, attempt and actor history.
- Task visibility/action permission checks.
- Status display: pending, in progress, completed and rejected.
- Audit log display.
- Code review narrative around state machine and service boundaries.

Backend/API touchpoints:

- `POST /api/processes/start`
- `GET/POST /api/process-definitions`
- `POST /api/process-definitions/{id}/validate`
- `POST /api/process-definitions/{id}/versions/{versionId}/publish`
- `POST /api/processes/start/version`
- `GET /api/processes`
- `GET /api/processes/{id}`
- `GET /api/tasks/my`
- `POST /api/tasks/{id}/actions`
- `POST /api/tasks/{id}/claim`
- `DELETE /api/tasks/{id}/claim`

Frontend files and areas:

- `features/processes`
- `ProcessListView`
- `MyTasksView`
- `TaskActionDialog`
- `ProcessDetailPanel`
- `AuditTimeline`
- `ProcessStepTimeline`
- `features/workflows`
- `StatusBadge`
- `docs/12-cagdas-process-flow.md`

Tests and review notes:

- Valid state transitions.
- Invalid state transitions.
- Unauthorized task action.
- Community/custom-role scoped task action.
- Invalid graph and unreachable node rejection.
- Gateway routing, send-back attempts and no-candidate rollback.
- Simultaneous stale claim competition.
- Published definition immutability and process version pinning.
- Audit log entry after every process action.
- Action notes are collected before approve/reject and persisted through audit logs.

Presentation angle:

How BPM is modeled with statuses, tasks, actions and traceable transitions.

Current ownership note:

Cagdas owns the versioned process graph, validator, React Flow modeler, dynamic runtime, priority, candidate claim/action and process step history. The implemented runtime supports person, process-starter, team, community-role and team-plus-role assignment. Ufuk owns the team/membership and permission contracts consumed by that runtime; Ozgun owns the versioned start/task form contracts and renderer.

Presentation sequence for this area: publish a workflow, start its pinned version, show the team candidate pool, claim the task, complete its task form, follow a gateway branch and inspect the actor-aware step history.

## Integration Rules

- API calls must stay in service/client modules, not scattered inside page components.
- Shared types should be reused by feature components where possible.
- Every meaningful feature should update the relevant documentation file.
- Commit history should stay progressive and readable.
- Demo flow should remain: login, publish form, draw/publish workflow, start process, claim and complete tasks, then inspect process detail and audit.
