# Agent Notes

These notes are the project memory. Update this file whenever an implementation choice affects future work.

## Fixed Decisions

- Scope: full-stack.
- Repository target: private `ufukzkn/techyouth-bpm`.
- Frontend: Next.js App Router, TypeScript, Zustand.
- Backend: .NET 8 Web API, EF Core, selectable SQLite/PostgreSQL provider.
- Default database mode: SQLite. Shared team testing can use Neon PostgreSQL through `.env.neon.local` and `scripts/run-api-neon.ps1`.
- Documentation must be kept current as code changes.
- Team split: flow-based, not layer-based.
- Frontend target is multi-screen navigation. Hash-scroll sections were only an early scaffold and should not be treated as final UX.

## Tooling Notes

- Node and npm are installed.
- .NET SDK 10 is installed and can target `net8.0`.
- Git history should stay progressive and easy to review.
- Repository documentation should avoid machine-specific paths, credentials, tokens, or private workflow details.
- Database connection strings and email provider credentials must stay in environment variables or .NET user secrets, not tracked files.
- When database schema, seed data or local startup changes, update `docs/08-local-database.md` and `scripts/run-api-local.ps1`.

## Architecture Principles

- Keep API calls inside a frontend service layer, not inside page components.
- Keep business logic in backend services, not controllers.
- Keep state transitions in a dedicated state machine/service so process rules are easy to change.
- Keep validation rules reusable between form start and backend validation.
- Prefer small progressive commits that match the PDF evaluation story.

## Demo Users

- `admin` / `admin123` / Admin
- `user` / `user123` / User
- `approver` / `approver123` / Approver

## Current Implementation Log

- Documentation baseline started from the PDF requirements.
- Backend scaffold created as a .NET 8 solution with Domain, Application, Infrastructure and Api projects.
- NuGet source is stored in repo-local `NuGet.config` because the machine initially only had offline Visual Studio package sources.
- Backend build is warning-free after adding domain entities, DTOs, EF Core DbContext, seed users, services and controllers.
- Backend test project added for workflow/state machine behavior.
- Frontend scaffold added with Next.js 16, TypeScript, Zustand, lucide-react and dnd-kit packages.
- Frontend app shell includes login, role-aware navigation, API-backed dashboard metrics, theme toggle and demo-user fallback when API is offline.
- Form designer is API-backed and now supports field editing, option management, move up/down ordering, dependent validation rules and JSON model preview.
- Process/task screens are API-backed and split into list, detail, task action dialog, status badge and audit timeline components.
- Form runner loads saved form definitions, renders fields through shared helpers, validates submitted values and starts process instances through the API.
- Frontend API client expanded with form, process and task methods so feature components can be wired without scattering fetch calls.
- Backend database provider selection added so local SQLite and shared PostgreSQL/Neon can use the same service/domain code.
- Local SQLite database guide and reset/start helper script added for teammate onboarding.
- Local SQLite startup now seeds optional mock workflow data by default: football-themed users, forms, processes, tasks and system/process audit logs. Use `-SkipMockData` for a nearly empty DB.
- Login no longer pre-fills credentials; demo buttons only fill credentials for testing.
- Stored frontend sessions are kept across refresh. The shell checks expiry locally, schedules a timeout for the stored expiry, and verifies real API sessions once through `/api/auth/me`; expired or unauthorized sessions return to login with a confirmable alert dialog instead of flickering or polling.
- Session duration is configuration-driven through `Auth:SessionDurationMinutes` and is currently 120 minutes. Remember-me uses hashed rotating refresh tokens controlled by `Auth:RefreshTokenDurationMinutes`, while short access sessions remain centrally revocable.
- Authenticated shell navigation now uses real route paths (`/dashboard`, `/forms`, `/runner`, `/processes`, `/tasks`, `/management`, `/logs`, `/settings`) so refresh, browser history and direct links behave closer to a production app.
- Auth hardening now stores PBKDF2 password hashes and SHA-256 session-token hashes instead of plaintext passwords or raw session tokens.
- Auth remains an opaque server-side session model rather than JWT. This is intentional because pending approval, logout/revoke, lockout and active-session management all need server-side state.
- Register creates `PendingApproval` users. Admin approval/role assignment, email verification demo flow, session listing/revoke, login/register rate limiting and failed-login lockout are now part of Ufuk's access/workspace flow.
- Critical user actions now use two audit channels: process `AuditLogs` for BPM state history and `SystemAuditLogs` for Admin-visible identity/access/form/process/task activity.
- Admin user management and system audit were split out of settings into dedicated `/management` and `/logs` workspace routes. Both use search/detail/pagination patterns so large lists are not dumped directly on screen.
- The API now uses EF Core migrations on startup through `Database.MigrateAsync`; `DatabaseSeeder` is responsible only for deterministic demo data. Old SQLite/Neon databases from the `EnsureCreated` phase should be reset/recreated before migration-based demos.
- Form designer/runner and process/task views are now wired to real API-backed flows.
- Ozgun's form foundation work continued on `feature/ozgun-form-foundation`.
- Shared frontend form helpers were added for supported field types, default field creation, reusable field rendering, form value handling and reusable validation.
- Form runner now uses the shared renderer/validation/value helpers, including stronger number conversion before process-start submit.
- Form designer now supports field editing, select/checkbox option management, move up/down field ordering and JSON preview updates from the same model that is saved.
- Dependent `RequiredWhen` validation can now be configured in the form designer and is included in the saved form definition model.
- Login/session, dashboard, app shell, process/task and audit behavior were intentionally left unchanged during Ozgun's form-flow work.
- Frontend `npm run lint` and `npm run build` passed after the form foundation, designer editing and dependent validation updates.
- Form runner loading, empty, error, submitting, success and backend-error states were strengthened for the demo flow.
- Form runner blocks the process-start API call when frontend validation finds required, type-based or dependent validation errors.
- Process-start payload is now shown more explicitly as `formDefinitionId` plus `formData`, and submitted JSON remains visible for demo review.
- Number values are prepared as numbers before submit, while checkbox values are preserved as booleans.
- Form designer drag/drop field ordering was added using the existing dnd-kit packages; no new dependency was added.
- Move up/down ordering remains available as an accessibility and fallback control.
- Field `sortOrder` values are normalized after drag/drop, move up/down and remove operations so JSON preview and saved payload stay aligned.
- Ozgun's Form Designer now includes a right-side sticky field palette for drag/drop field creation. Palette cards are icon-led and localized, click-only add is blocked, drag insertion preview shows the target position, Text Area and Radio Button shown as `Seçenek düğmesi` are supported, technical keys are ASCII-safe, option validation blocks empty/duplicate values, and existing ordering, RequiredWhen, JSON preview and save/update behavior remain intact.
- Form designer and runner UI copy, demo guidance and scoped layout polish were improved for presentation readiness.
- Backend, login/session, dashboard, app shell, process/task and audit areas were not changed during these Ozgun form-flow updates.
- Frontend `npm run lint` and `npm run build` passed after the runner, drag/drop and UI polish updates.
- Cagdas's process-flow work split the process UI into `ProcessListView`, `ProcessDetailPanel`, `MyTasksView`, `TaskActionDialog`, `AuditTimeline` and `StatusBadge`.
- Task actions now collect an action note in a dialog before calling the backend approve/reject endpoint.
- Backend tests now cover task authorization and audit log creation in addition to the state machine transition tests.
- Saved form definitions can now be loaded into the designer and updated through `PUT /api/forms/{id}`; the backend update path replaces the editable field/rule model and keeps Admin-only authorization.
- Latest verification after remember-me and UX polish work: frontend lint/build passed and backend test suite passed with 24 tests.
- Ufuk's access/workspace flow now has its own tracking document in `docs/10-ufuk-access-shell-flow.md`.
- Dashboard metric cards and BPM flow steps now act as role-aware shortcuts into the workspace.
- Top bar session details moved behind a compact session icon so the shell keeps expiry/user/role information without permanently showing a long timestamp.
- Dashboard metrics keep the last loaded values while refreshing to avoid flashing placeholder values during fast navigation.
- Language support foundation added with persisted `tr/en` preference, login/topbar language toggles, translated shell/dashboard/settings/process/task UI and documented extension rules in `docs/11-i18n-language-support.md`.
- Ufuk access flow was extended with editable profile details, password change, admin-created temporary-password users, `MustChangePassword` workspace restriction, session IP/User-Agent metadata and localized known auth/access API errors.
- Existing SQLite/PostgreSQL databases now use EF Core migrations for schema changes; `DatabaseSeeder` no longer patches identity columns manually.
- Email verification OTP uses `IOtpService`/`OtpService` for generation, PBKDF2 hashing and expiry validation. Delivery is abstracted behind `IEmailSender`: `DemoEmailSender` exposes the code for local demo, `SmtpEmailSender` sends through one SMTP provider, and `RoutingEmailSender` sends allowlisted users through live SMTP while falling back to Mailtrap Sandbox for non-allowlisted teammates.
- Email verification validity is configured with `Auth:EmailVerificationMinutes` and is currently 1440 minutes. Resends are guarded by `Auth:EmailVerificationResendCooldownMinutes` and are currently limited to one request per 5 minutes. Mailtrap Sandbox is a capture inbox, not real recipient delivery; it is intentionally used for teammates who are outside the live-send allowlist.
- Real SMTP delivery can be safety-gated with `Email:AllowedRecipients` and `Email:AllowedUsernames`. For Ufuk-only live testing, allow the Ufuk test email/username; do not commit real Mailtrap API tokens or SMTP passwords.
- Admin-created temporary-password users now use the same `IEmailSender` boundary. SMTP/Mailtrap mode sends the temporary password by email; backend generates a strong temporary password unless Admin explicitly provides a custom one; users with `MustChangePassword=true` are blocked by a dedicated password-change gate before entering the workspace.
- Admin-created users now default to backend-generated temporary passwords unless the Admin explicitly enables a manual password. Admin hard-delete is available only for users without workflow history; workflow-linked users should be disabled/rejected instead of deleted.
- The authenticated shell was moved to `WorkspaceShell`. It owns session, role guard, theme/language and navigation chrome, while each Next.js route imports only its own view component. `AppShell.tsx` remains a compatibility re-export.
- User and audit list pagination now uses single-page lazy loading. Do not reintroduce previous/next page prefetch unless there is a measured UX need; log category counts may stay query-cached and can be refreshed manually.
- Access security now uses opaque access sessions plus hashed rotating refresh tokens for remembered devices. Browser flows receive HttpOnly access/refresh cookies and a CSRF token, while Swagger/dev flows can still use the returned bearer token.
- Password reset and public email verification are part of Ufuk's identity flow. Pending-approval users can verify email before admin approval; password reset completion revokes existing sessions.
