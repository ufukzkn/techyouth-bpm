# Team Work Split

The team split is flow-based. Each person owns one user journey end to end, including UI, API usage, state, tests, documentation and presentation.

## Ownership Summary

| Person | Flow | Main Outcome |
| --- | --- | --- |
| Person 1 | Access, shell and dashboard | User can enter the app, see role-aware navigation and understand the workspace. |
| Person 2 | Form design and form run | Admin can design a form model; user can fill it with validation. |
| Person 3 | Process, tasks and review | A submitted form becomes a process with tasks, actions, status and audit history. |

## Person 1: Access, Shell And Dashboard Flow

Production tasks:

- Login page and login error/loading states.
- Session state with active user, token and role.
- Protected app shell after login.
- Header with active user information.
- Left menu filtered by role.
- Dashboard cards for pending, in-progress and completed work.
- Settings page draft for theme and user preferences.

Backend/API touchpoints:

- `POST /api/auth/login`
- `GET /api/auth/me`
- Role data returned from auth response.

Frontend files and areas:

- `features/auth`
- `features/session`
- `features/app-shell`
- Dashboard and settings sections.

Tests and review notes:

- Login success and failure scenarios.
- Role-based menu visibility.
- Theme/session persistence behavior.

Presentation angle:

How users enter the system and how role information shapes the interface.

## Person 2: Form Design And Form Run Flow

Production tasks:

- Form designer screen.
- Custom field components for input, select, checkbox, number, email and date.
- Field properties: key, label, type, required, sort order and options.
- Field ordering with drag/drop or simple move controls.
- Form definition JSON preview.
- Dynamic form runner for filling a designed form.
- Required, type-based and dependent validation.
- Submit loading, success and error states.

Backend/API touchpoints:

- `GET /api/forms`
- `POST /api/forms`
- `GET /api/forms/{id}`
- Backend form data validation used when starting a process.

Frontend files and areas:

- `features/form-designer`
- Future `features/form-runner`
- Shared field renderer and validation helpers.

Tests and review notes:

- Required field validation.
- Type validation such as email and number.
- Dependent rule example: if request type is purchase, approval note becomes required.
- Adding a new field type should not require rewriting the whole wizard.

Presentation angle:

How a form is represented as data and rendered dynamically instead of being hardcoded.

## Person 3: Process, Tasks And Review Flow

Production tasks:

- Start process from submitted form data.
- Process list and process detail.
- My tasks list.
- Approve/reject actions.
- Status display: pending, in progress, completed and rejected.
- Audit log display.
- Code review narrative around state machine and service boundaries.

Backend/API touchpoints:

- `POST /api/processes/start`
- `GET /api/processes`
- `GET /api/processes/{id}`
- `GET /api/tasks/my`
- `POST /api/tasks/{id}/actions`

Frontend files and areas:

- `features/processes`
- Task action controls.
- Process detail and audit log views.

Tests and review notes:

- Valid state transitions.
- Invalid state transitions.
- Unauthorized task action.
- Audit log entry after every process action.

Presentation angle:

How BPM is modeled with statuses, tasks, actions and traceable transitions.

## Integration Rules

- API calls must stay in service/client modules, not scattered inside page components.
- Shared types should be reused by feature components where possible.
- Every meaningful feature should update the relevant documentation file.
- Commit history should stay progressive and readable.
- Demo flow should remain: login, design form, start process, complete task, view process detail.

## Suggested Next Commits

- `feat(web): form runner taslagi eklendi`
- `feat(api): form baslatma validasyonlari tamamlandi`
- `feat(web): task aksiyonlari api ile baglandi`
- `test(api): yetki kontrol senaryolari test edildi`
- `docs: code review anlatimi detaylandirildi`
