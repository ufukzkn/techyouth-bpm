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

## Later Production-Readiness Roadmap

These items are not required by the PDF, but they fit the goal of building the most complete version we can reasonably deliver:

- PostgreSQL/Neon setup for shared remote database development.
- Docker setup for repeatable API, web and database startup.
- Stronger authentication lifecycle with refresh-token style remember-me behavior.
- Admin-managed user onboarding and role approval.
- CI checks for backend tests, frontend lint/build and later end-to-end flows.

## Important Evaluation Point

The evaluator explicitly expects more than "a working UI". The architecture must be readable, modular, and easy to extend without breaking unrelated parts of the system.
