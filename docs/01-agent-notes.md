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
- Keep `Community`, `Team` and `CommunityRole` separate: security boundary, operational group and permission bundle respectively.
- `Takimsiz` is always a virtual query result; never seed a fake team row for it.
- Team leadership is context only and must never become an authorization shortcut.

## Demo Users

- `admin` / `admin123` / SuperAdmin
- `fatih.terim` / `imparator123` / Sportif Faaliyetler - Topluluk Admin
- `alex` / `alex123` / Urun Siparisi - Topluluk Admin
- `user` / `user123` / Sportif Faaliyetler - Surec Baslatici
- `approver` / `approver123` / Sportif Faaliyetler - Onay Sorumlusu
- `senol.gunes` / `senol123` / Insan Kaynaklari - Topluluk Admin
- `ali.koc` / `ali123` / Satin Alma - Topluluk Admin

## Current Implementation Log

- Ozgun Form Flow responsive polish now covers the main 1920/1440/1366/1240/1100/1024/860/768/640 viewport bands. Designer content is centered within the workspace, medium-width panels no longer force horizontal overflow, and field/rule/option editors plus scoped input/select controls wrap safely. At 1600 CSS pixels and above the palette remains a right rail; narrower or zoom-constrained layouts use the right-side FAB/drawer instead of placing the palette below the form. The desktop palette list scrolls independently, the save/update panel stays in a separate visible rail row, and the dragged palette source is allowed to paint outside the scroll container without changing insertion or drop rules. Drawer selection currently adds fields by click. Existing DnD context, sortable/draggable setup, reorder, move controls, insertion preview, field editing, options, `RequiredWhen`, save/update and Runner payload behavior remain intact. Known follow-ups are the mixed-height field-card preview distortion, exact cursor tracking while scrolling a palette drag, optional drawer drag/drop, and the separately authorized real binary File Upload/storage design.
- Ozgun Form Flow now includes a metadata-only `FileUpload` foundation across the frontend/backend field-type contract, Designer palette and type selector, Runner rendering, frontend validation and backend process-data validation. The Runner keeps the real browser `File` out of JSON and sends only `name`, `size`, `type` and `lastModified`; the fixed first-phase policy is single-file, 10 MB maximum, and the documented PDF/image/Office allowlists. API errors now normalize both string arrays and ASP.NET validation dictionaries while preserving the localized community-required mapping. This is not a real upload module: binary transfer, multipart endpoints, attachment persistence, storage, access control, download/delete, content inspection, scanning, cleanup and retention remain deferred pending an authorized follow-up decision. Existing field types, `RequiredWhen`, palette invalid-drop guards, reorder/move controls, JSON preview, process/task/audit/auth/dashboard behavior and package dependencies remain intact. Frontend lint/build, backend build/tests and `git diff --check` passed; the five pre-existing `ProcessListView.tsx` unused warnings and the two known DnD preview/scroll issues remain out of scope.
- Ozgun Form Flow's latest committed polish centers the Designer and Runner content within their workspace area, strengthens scoped validation/save feedback, adds short panel-local feedback for manual field insertion and new-form reset, and keeps the saved-form transition overlay. Palette insertion now uses a consistent decorative preview at top, middle and bottom positions without changing drop/index rules; invalid, outside and palette-return drops still create nothing. Designer validation feedback summarizes specific key, label, option and `RequiredWhen` problems, shows at most three readable items plus a remaining count, and maps the known API message `A community is required for form definitions.` to the active UI language. Existing reorder, move up/down, `sortOrder` normalization, JSON preview, save/update payloads, backend rules and package files remain unchanged. Frontend lint/build and `git diff --check` passed; the five pre-existing `ProcessListView.tsx` unused-symbol warnings remain out of scope. Two visual DnD issues are explicitly deferred: mixed-height field-card drag previews can still distort, and palette previews can drift from the cursor when the page scrolls.
- Auth and community boundaries were split without changing HTTP contracts. Controllers now depend on focused authentication, registration, account, session, user-administration, community and community-role interfaces; the compatibility aggregate remains only while older callers are migrated.
- Inbox state now lives in a user-scoped Zustand store with a 30-page in-memory LRU, a 30-second freshness window, stale-while-revalidate behavior and optimistic read-state rollback. The first notification poll establishes a baseline; only later visible polling can create live toasts.
- Team/workflow architecture is fixed in `docs/18-dynamic-workflow-and-team-architecture.md`. Task priority remains a future Cagdas-owned workflow concern and must not be slipped into the current task entity during Ufuk's team package.
- Ozgun Form Flow UX polish fixed the ThemeToggleButton first-render hydration mismatch, compacted and balanced the designer palette rail, moved save/update into a separate action panel, restricted palette-created fields to valid canvas/field targets, added a form-selection overlay and management-style opening/field-card skeletons for designer and runner, and added scoped move up/down feedback for both swapped cards. The clicked card keeps the primary highlight while its displaced neighbor uses neutral motion; drag/drop ordering, validation, payloads, backend behavior and package definitions were unchanged. Frontend lint/build and `git diff --check` passed; the five existing `ProcessListView.tsx` unused-symbol warnings remain out of scope.
- Form Runner hardening now formats process `startedAt` with the shared date/time helper and a safe empty fallback, while its async form-list load ignores state, message and cache updates after unmount. Existing selection, initial values, validation, payload and runner status behavior remain unchanged; frontend lint/build passed.
- Ozgun Form Flow validation hardening aligned frontend and backend text validation, added a dedicated localized text-type error, blocked raw empty designer keys before key normalization, strengthened case-insensitive Select/Radio option validation, added backend `yyyy-MM-dd` Date validation, and added focused backend definition/process-start tests. Frontend lint/build, backend solution build and the related backend tests passed; auth/session/login, dashboard, app shell, workflow/task/audit behavior, drag/drop UI and package/dependency files were unchanged.
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
- The authenticated workspace now uses the shared `apps/web/src/app/(workspace)/layout.tsx` App Router layout. Sidebar/topbar persist across client-side route changes; route pages render only their feature view. Session timing, navigation, notifications and loading chrome are split into focused components under `features/app-shell/components`.
- User and audit list pagination now uses single-page lazy loading. Do not reintroduce previous/next page prefetch unless there is a measured UX need; log category counts may stay query-cached and can be refreshed manually.
- Access security now uses opaque access sessions plus hashed rotating refresh tokens for remembered devices. Browser flows receive HttpOnly access/refresh cookies and a CSRF token, while Swagger/dev flows can still use the returned bearer token.
- Password reset and public email verification are part of Ufuk's identity flow. Pending-approval users can verify email before admin approval; password reset completion revokes existing sessions.
- Community/custom role authorization is now part of the access model. `SuperAdmin` is the platform-level role; everyday BPM access uses `Community`, `CommunityRole`, `CommunityRolePermission` and `UserCommunityMembership`. Forms and processes are community-scoped, navigation reads `UserDto.permissions`, and backend services still enforce the same permissions server-side.
- `Admin`, `User` and `Approver` are no longer selectable day-to-day authorization roles. Every non-SuperAdmin account is normalized to the standard platform user value; user creation first selects a community and then a role owned by that community. Do not reintroduce platform-role checks as permission shortcuts.
- `ProcessTask.AssignedRole` is a database-compatibility field only. Do not use it in services, DTOs or frontend logic; task assignment is represented by `AssignedCommunityRoleId` and `RequiredPermission`.
- Demo seed currently creates five populated communities: Sportif Faaliyetler, Lojistik, Urun Siparisi, Insan Kaynaklari and Satin Alma. Each seeded community has exactly eight active memberships distributed across admin, form tasarimcisi, surec baslatici, onay sorumlusu, standart kullanici, gozlemci and `Atanmadi` roles; every community retains a pending/unassigned user for approval demos. Local SQLite, Neon and Docker call this same deterministic seed.
- Ready community-role templates remain directly selectable system roles. Changing a permission on a ready template automatically creates an editable `Rol Adi*` custom draft; do not reintroduce a second template select or a separate customization button.
- Management UI standard: user and community management stay as separate route-level views. A view must not fetch another view's list just because it shares the same shell.
- Loading standard: first load uses a matching skeleton, cached data remains visible during background refresh, and compact count/value slots use the shared inline loader. Do not render temporary `-` or `0` values while a real value is being requested.
- Dashboard chart standard: the donut uses SVG stroke segments. Hover/focus may adjust stroke width, opacity and shadow, but must not apply SVG `transform: scale`; transforms shift segments in some Docker/browser render paths.
- Feedback standard: mutations use a reusable confirmation dialog when they create, change permissions, deactivate, revoke or delete. Success/error feedback belongs inside the card that initiated the action; fixed-position toast is reserved for page-level refresh feedback.
- Cache standard: page/count caches are keyed by scope and filter. Mutating actions and explicit refresh invalidate the affected key; a route reload may obtain fresh data normally.
- Workspace routing standard: protected routes live under the shared `(workspace)` layout. Sidebar/topbar must not remount during client navigation. Route pages read the required session values directly from Zustand; do not add a second global workspace context or manual route-wide prefetch loop.
- Session-expiry standard: a protected API request returning `401` clears the persisted session and routes to `/login`. A successful login always routes to `/dashboard`; do not retain the previous protected route for a different user.
- Community lifecycle: SuperAdmin can activate/deactivate and edit all community metadata. A Topluluk Admin may toggle only its own community's active status; it cannot change name, description or invite code. Deactivation revokes normal member sessions but keeps the acting admin's session and normal permission-aware workspace navigation available.
- Login fallback rule: demo credentials are an offline-development fallback only. A real API response, including a rejected login such as an inactive community, must be shown to the user and must never create a demo session.
- Community role editor rule: use one role-source select for `Ozel rol olustur`, ready templates and saved custom roles. A ready template becomes an editable `Role Name*` custom draft only after a permission change; do not add a second template select or a separate customization button.
- Community status UI rule: SuperAdmin changes the status select through `Degisikligi uygula`. Topluluk Admin sees a read-only status and uses the single right-aligned active/passive action. Keep semantic icons: community `Landmark/Building2`, role `Tags/BadgeCheck`, member count `Users`, and shields for session/security only.
- Dashboard summary rule: use `GET /api/dashboard/summary`, not full process/task lists. The server applies the current process/task visibility rules before aggregating counts; frontend cache keys include the current user and community scope.
- Audit list rule: category card counts are global to the current user's audit scope and remain stable while searching or switching categories. Log sorting is server-side and occurs before pagination; supported fields are created time, action and actor.
- Chart and detail disclosure rule: dashboard segment hover may use the current stroke-width/opacity focus animation and update tooltip/center values, but must not use SVG scale/translate transforms that move the chart. User-detail chronological history stays collapsed until explicitly opened through the disclosure control.
- User filtering standard: user status is a multi-select OR filter. The API accepts repeated `statuses` values; no selected status means all statuses. Include the sorted status set in the page-cache key.
- System role naming: `Standart Kullanici` can start and monitor processes plus view open work; `Gozlemci` is the user-facing name for the old read-only template. Keep technical template keys stable when changing display copy.
- Latest verified backend baseline is 105 passing tests. Service tests use relational SQLite instead of EF InMemory. `WebApplicationFactory` tests cover real HTTP cookies, CSRF, Bearer/Swagger compatibility, refresh rotation/reuse, logout, `429` rate limiting, inactive-community login, management scope and form-to-process smoke behavior.
- Transaction standard: form update, process start and task action must commit business state, notifications and audit writes atomically. Failure-injection tests intentionally break system audit persistence and verify full rollback.
- Migration smoke standard: normal tests create isolated temporary SQLite databases. When `TECHYOUTH_TEST_POSTGRES_CONNECTION` exists, the opt-in Neon test creates a unique temporary schema, applies migrations and seed, checks login/forms through HTTP, then drops only that schema.
- Notification coverage: community managers receive pending-registration notices; task candidates receive task-assigned notices; a process starter receives completion/rejection updates; and a user receives `User.AccessUpdated` when a manager changes the user's active status or community role. Password-reset delivery keeps its separate `User.PasswordReset` notification.
- Inactive-community login rule: normal members cannot sign in or refresh a session for an inactive community. A `Topluluk Admin` may sign in again after deactivating its own community so it can inspect the scoped management state; form, process and task write operations remain blocked by their active-community checks. `SuperAdmin` can reactivate the community.
- Docker Compose is an optional provider-compatibility environment, not a replacement for the SQLite local demo. `eczacibasi-local` runs SQLite API + web, while `eczacibasi-cloud` runs the same API + web against Neon using ignored `.env.neon.local`; both apply migrations and the same mock seed data. They share ports and must not run together.
- System role policy: do not keep multiple default roles with the same permission set. `Lojistik Gorevlisi` was retired as a redundant system role; existing assignments migrate to `Onay Sorumlusu`. A ready template is always copied into a community-specific custom role before its permissions are changed.
- UI style ownership is documented in `docs/19-ui-ux-system.md`. Global CSS is split by domain and imported in a fixed order; do not rebuild a monolithic `globals.css` or place feature rules in unrelated style files.
- Form Designer palette rule: at 1440 CSS pixels and above, the palette is a sticky third grid column and the route may widen to 1460px. Below that breakpoint it returns to normal flow. Keep `overflow-x: clip`; `hidden` creates a scroll container and breaks sticky positioning.
- Language toggle rule: render TR and EN in one fixed-width horizontal track. Direction mirrors the active language, layout width never changes, and reduced-motion disables the transition.
- Personal dashboard rule: `GET /api/dashboard/summary` returns the three backward-compatible counts plus at most four recent open tasks and four recent visible processes. Ordering and community/permission scope are applied in the database; never fetch full task/process lists to build the dashboard in the browser.
- Notification list rule: `GET /api/notifications` is current-user scoped and server-paged. `totalCount` follows the active search/category/read filter, while `allCount` and `unreadCount` remain global to that user's inbox. Popovers request only five records; `/inbox` requests ten records per page. Never load all notifications merely to render the badge.
- Notification refresh rule: the shared Zustand notification store owns topbar preview items and unread count. Refresh on workspace entry, popover open, visible-tab return and every 30 seconds while visible. V1 deliberately uses polling; WebSocket/SSE is a future transport improvement, not a requirement for correct state.
- Notification navigation rule: task notices open tasks, process notices open the related process query, access/account notices open settings, and pending registration notices open user management. Mark a selected unread notification before navigating; notifications without a known target fall back to `/inbox`.
- Dashboard composition rule: counts live in the clickable donut legend instead of duplicate metric cards. Header commands are limited to permission-aware create/start actions, the focus card shows open tasks or the user's visible processes, and recent activity is driven by the latest four notifications. Do not reintroduce a second shortcut grid with the same destinations.
- Mobile Form Designer rule: palette selection is tap-to-append; fields already on the canvas remain touch-sortable through their dedicated drag handle, with move up/down controls as fallback. Palette-to-canvas long-press drag is intentionally avoided because it conflicts with sheet dismissal and page scrolling.
- Mobile palette dismissal rule: the full 48px top strip is draggable. Opening, open, dragging and closing are separate phases; dismissal waits for the sheet transform `transitionend` with a timeout fallback. Escape, backdrop, close button, field selection and downward swipe must use the same close path, while reduced-motion closes immediately.
