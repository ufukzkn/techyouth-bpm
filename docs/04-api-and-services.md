# API And Services

## Auth

- `POST /api/auth/login`
  - Validates username/password against the stored password hash.
  - Rejects pending/rejected users and temporarily locked accounts.
  - Returns an opaque session token value, user profile, CSRF token and expiry time.
  - Also writes the access token to an HttpOnly cookie for browser flows; Swagger can still use the returned bearer token.
  - Stores only the SHA-256 hash of the session token in `UserSessions`.
  - Accepts `rememberMe` to create a remembered-device refresh token stored as a hash in `RefreshTokens`.
- `POST /api/auth/refresh`
  - Uses the HttpOnly refresh cookie to rotate the refresh token and issue a new opaque access session.
  - Revokes the previous access session and previous refresh token.
  - If a revoked refresh token is reused, active sessions for that user are revoked and `Auth.RefreshReuseDetected` is written to system audit.
- `POST /api/auth/register`
  - Creates a new `User` account with `PendingApproval` status.
  - Stores a PBKDF2 password hash and starts with `IsEmailVerified=false`.
  - Requires `communityCode`; the user is attached to that community with the blank `Atanmadi` role until a community admin approves/assigns access.
- `POST /api/auth/forgot-password`
  - Sends a password reset token by email when the account exists.
  - Always returns a generic success response so unknown users are not revealed.
  - The email includes a `Sifreyi sifirla` link built from `Frontend:BaseUrl`; the token remains long and random because it is intended to be carried by the link, not typed manually.
  - `Email:Provider=Demo` returns the reset token in the response for local debugging. SMTP/Mailtrap modes keep the token only in the email.
- `POST /api/auth/reset-password`
  - Validates the single-use reset token, stores the new PBKDF2 password hash and revokes existing sessions.
- `GET /api/auth/me`
  - Hashes the incoming bearer token or access cookie and reads the current unexpired session.
  - Returns active user information, including email verification and temporary-password requirement.
- `PATCH /api/auth/me/profile`
  - Updates the current user's display name and email.
  - If email changes, resets `IsEmailVerified=false` and clears any previous verification code.
- `POST /api/auth/me/password`
  - Verifies the current password and stores the new password as a PBKDF2 hash.
  - Clears `MustChangePassword` after a temporary-password user sets a real password.
- `POST /api/auth/logout`
  - Revokes the current session in the database.
- `GET /api/auth/sessions`
  - Lists active sessions for the current user, including created time, last seen time, IP address and user agent.
- `DELETE /api/auth/sessions/{sessionId}`
  - Revokes a selected session belonging to the current user.
- `POST /api/auth/me/email-verification`
  - Generates a short-lived verification code.
  - Uses `IOtpService` for OTP generation, PBKDF2 hashing and expiry validation.
  - Uses `IEmailSender` for delivery.
  - Default OTP validity is 24 hours through `Auth:EmailVerificationMinutes=1440`.
  - Immediate resends are blocked for 5 minutes through `Auth:EmailVerificationResendCooldownMinutes=5`.
  - `Email:Provider=Demo` exposes the code in the response for local development.
  - `Email:Provider=Mailtrap` or `Smtp` sends the code through SMTP and does not expose the code in the response.
  - Mailtrap Sandbox captures emails inside the Mailtrap inbox; real user inbox delivery needs Gmail SMTP app password, Mailtrap Email Sending or another production mail provider.
  - If SMTP delivery fails, the verification code is not persisted.
- `POST /api/auth/me/email-verification/confirm`
  - Confirms the code and marks the current user's email as verified.
- `POST /api/auth/public-email-verification/start`
  - Starts email verification for a known username/email without requiring login.
  - This supports `PendingApproval` users who cannot sign in yet.
- `POST /api/auth/public-email-verification/confirm`
  - Confirms email verification before login while keeping admin approval as a separate access decision.

`POST /api/auth/login` and `POST /api/auth/register` use ASP.NET Core rate limiting. Login also increments failed attempts and sets `LockedUntil` after the configured threshold.

The project does not currently use JWT. It uses opaque bearer session tokens backed by the database plus rotating refresh tokens for remembered devices. This is intentional for the current BPM scope because sessions can be expired and revoked centrally from the database. JWT would not remove the need for server-side state for pending approval, lockout, logout/revoke, refresh-token reuse detection and active-session management.

Browser flows send cookies with `credentials: include`. Mutating cookie-authenticated requests include `X-CSRF-Token`; Swagger and local API debugging can still use the `Authorization: Bearer` header.

## Dashboard

- `GET /api/dashboard/summary`
  - Returns open-task, in-progress-process and completed-process counts without loading full lists.
  - Also returns at most four recent open tasks and four recent visible processes, ordered newest first.
  - Applies the same authorization scope as task/process services: SuperAdmin is global, community-scoped roles stay inside their community, and a user without task visibility sees only processes they started.
  - Keeps the original three count fields for backward compatibility; recent collections are additive.

## Users And Access

- `GET /api/users`
  - Admin/community-admin paged list of registered users, statuses, roles, community role, verification state and lockout info.
  - Supports `query`, `status`, `communityId`, `page` and `pageSize` query parameters.
  - Returns `PagedResult<UserAdminDto>` so large user tables are searched and paged in the database instead of loaded fully into the browser.
- `POST /api/users`
  - Admin/community-admin user creation with username, display name, email, platform role, community role, status and temporary password.
  - New admin-created users start with `MustChangePassword=true`.
  - If the request does not include a custom temporary password, the backend generates a strong temporary password.
  - Sends the temporary password to the created user's email through `IEmailSender`.
  - If the temporary-password email cannot be sent, the user is not persisted.
  - `SuperAdmin` accounts can only be created as brand-new users by an existing `SuperAdmin`; existing users cannot be promoted to `SuperAdmin`.
- `POST /api/users/{userId}/password-reset-by-admin`
  - `SuperAdmin`-only password reset for non-SuperAdmin users.
  - Default behavior generates a strong temporary password, emails it and sets `MustChangePassword=true`.
  - Optional manual temporary password is supported for emergency recovery, but the UI marks it as not recommended.
  - Revokes the selected user's active sessions and writes system audit.
- `DELETE /api/users/{userId}`
  - Admin-only hard delete for users without workflow history.
  - Rejects deleting the current Admin account.
  - Rejects deleting users referenced by form, process, task or process-audit history.
  - Removes active sessions and writes `User.DeletedByAdmin` to system audit.
- `PATCH /api/users/{userId}/access`
  - Admin/community-admin role, status and community membership update.
  - Used for approving `PendingApproval` accounts, rejecting accounts, changing platform roles or assigning a community role.
  - Moving a user out of `Active` revokes existing sessions.
  - Audit text records the previous role/status and the new role/status.
- `GET /api/users/{userId}/sessions`
  - Admin-only active-session list for the selected user.
  - Used by the `Yonetim` detail panel to show whether a user is online, which sessions are active and which device/IP last touched the API.
- `DELETE /api/users/{userId}/sessions/{sessionId}`
  - Admin-only session revoke for the selected user.
  - The frontend asks for confirmation before sending this request.
  - Writes a system audit log.

## Communities And Permissions

- `GET /api/communities`
  - Lists communities visible to the current user.
  - `SuperAdmin` sees all communities; community users see their own community.
- `POST /api/communities`
  - `SuperAdmin` creates a new community.
- `PATCH /api/communities/{communityId}`
  - `SuperAdmin` updates community name, description and active state.
- `PATCH /api/communities/{communityId}/invite-code/regenerate`
  - `SuperAdmin` regenerates the active 5-character registration code for a community.
- `GET /api/communities/role-templates`
  - Returns built-in role templates such as `Topluluk Admin`, `Form Tasarimcisi`, `Onay Sorumlusu` and `Lojistik Gorevlisi`.
- `GET /api/communities/{communityId}/roles`
  - Lists custom roles in a community.
- `POST /api/communities/{communityId}/roles`
  - Creates a community role with selected permissions.
- `PATCH /api/communities/{communityId}/roles/{roleId}`
  - Updates role name, description and permissions.
- `GET /api/communities/{communityId}/users`
  - Lists users in a community.
- `POST /api/communities/{communityId}/users`
  - Creates a user directly inside the community.
- `PATCH /api/communities/{communityId}/users/{userId}/membership`
  - Changes a user's community role or active membership state.

`UserDto` now includes `communityId`, `communityName`, `communityRoleId`, `communityRoleName` and `permissions`. The frontend uses these fields for route visibility, but services still enforce permission checks on the backend.

## Notifications

- `GET /api/notifications`
  - Returns only the current user's DB-backed notifications, newest first.
  - Supports `page`, `pageSize` (maximum 50), `query`, `readStatus` (`read`/`unread`) and `category` (`task`, `process`, `access`, `account`).
  - Returns `items`, `page`, `pageSize`, filtered `totalCount`, user-wide `allCount` and user-wide `unreadCount`.
- `PATCH /api/notifications/{id}/read`
  - Backward-compatible endpoint that marks one owned notification as read.
- `PATCH /api/notifications/{id}/read-state`
  - Receives `{ isRead }` and changes an owned notification in either direction.
- `POST /api/notifications/read-all`
  - Marks all current-user notifications as read.

Notifications are created for events such as pending registration, assigned tasks, process outcome changes, password reset and access updates. Access update audit and target-user notification commit inside the same database transaction. The topbar asks for five records and polls every 30 seconds while visible; `/inbox` uses ten-record server-side pages. V1 remains database-backed polling, so WebSocket/SSE can be added later without changing the notification table or public UI behavior.

## Audit

- `GET /api/audit/system`
  - Admin-only paged list of critical system actions.
  - Supports `query`, `category`, `page` and `pageSize` query parameters.
  - Shows actor user, action name, entity type/id, affected user display/username when the entity is a user, description and date.
  - Covers identity/access actions and core BPM actions such as form create/update, process start and task approve/reject.
  - The frontend does not dump all logs by default; Admin users search and page through server-filtered results, then open related chronological history.
- `GET /api/audit/system/counts`
  - Admin-only category counts for the log cards.
  - Supports `query`, so the category cards can show count totals without loading the full audit table into the browser.

Process-specific history is still returned from `GET /api/processes/{id}` as `auditLogs`. Admins and approvers can inspect visible process history; a normal user can inspect the history of processes they started. This satisfies the PDF expectation that actions such as start/approve/reject are tied back to the user who performed them.

## Forms

- `GET /api/forms`
  - Lists saved form definitions.
  - Non-SuperAdmin users only see forms in their active community.
- `POST /api/forms`
  - Creates a form definition with fields and validation rules.
  - Stores the form under the active user's community, or the selected community for SuperAdmin.
  - Stores `CreatedByUserId` and writes a system audit log.
- `GET /api/forms/{id}`
  - Returns a single form definition.
- `PUT /api/forms/{id}`
  - Updates an existing form definition.
  - Replaces the editable field list and validation rules with the submitted model.
  - Stores `UpdatedByUserId` / `UpdatedAt` and writes a system audit log.
  - Requires an Admin session, like form creation.

## Processes

- `POST /api/processes/start`
  - Validates submitted form data.
  - Stores JSON submission data.
  - Creates a process instance.
  - Creates the first task.
  - Stores `StartedByUserId`, writes process audit and writes system audit.
- `GET /api/processes`
  - Lists processes visible to the active user.
  - Process visibility is scoped by community and `Processes.View`.
  - Uses a lightweight EF Core projection for summary fields instead of loading tasks and audit history for every row.
  - This keeps remote PostgreSQL/Neon list screens fast; full task/audit data is loaded only from the detail endpoint.
- `GET /api/processes/{id}`
  - Returns process status, dates, form data, tasks and audit history.
  - Uses split loading for related tasks and audit users to avoid one oversized joined result set.

## Tasks

- `GET /api/tasks/my`
  - Lists tasks assigned to the current user or role.
  - V1 task visibility uses community scope plus `Tasks.View`; action execution also requires `Tasks.Act`.
- `POST /api/tasks/{id}/actions`
  - Runs an action such as approve or reject.
  - Updates process status through the state machine.
  - Stores `CompletedByUserId`, writes a process audit log entry and writes system audit.

## Swagger Usage

Swagger is available in development at `/swagger`.

Most endpoints require `Authorization: Bearer <token>`. Use `POST /api/auth/login` first, copy the returned token, then click `Authorize` in Swagger and paste the token. Swagger adds the bearer header to protected requests after authorization.

## Service Responsibility

Controllers should stay thin. Services own decisions:

- `AuthService`: registration, profile updates, password changes, password hash verification, lockout, session-token hashing, session metadata, session revoke, email verification orchestration and admin user access.
- `OtpService`: six-digit email verification code generation, hashed OTP storage and expiry/code validation.
- `IEmailSender` implementations:
  - `DemoEmailSender`: no external dependency; exposes the generated OTP for local demo and no-ops admin temporary-password emails.
  - `SmtpEmailSender`: sends HTML OTP and admin-created temporary-password emails through SMTP providers such as Mailtrap.
  - `RoutingEmailSender`: tries the primary live SMTP provider first, then falls back to Mailtrap Sandbox when the recipient or username is outside the live-send allowlist.
- `SystemAuditService`: critical system-action logging, Admin-only paged/searchable audit list and category count queries.
- `FormService`: form definition CRUD and field validation.
- `ProcessService`: process start, detail and listing. List queries return projected summary DTOs; detail queries load the full process graph.
- `TaskService`: task listing and action execution.
- `NotificationService`: current-user paged search/filter, count, read-state and read-all operations.
- `ProcessStateMachine`: allowed transitions.
- `DatabaseSeeder`: local demo users and optional mock workflow data.

## Frontend Client Coverage

The frontend API client now exposes one method for each planned endpoint:

- Auth: `register`, `login`, `me`, `logout`, `updateProfile`, `changePassword`, `listSessions`, `revokeSession`, `startEmailVerification`, `confirmEmailVerification`
- Users: `listUsers` returns `PagedResult<UserAdmin>`, then `createUser`, `updateUserAccess`, `listUserSessions`, `revokeUserSession`, `resetUserPasswordByAdmin`
- Communities: `listCommunities`, `createCommunity`, `updateCommunity`, `regenerateCommunityInviteCode`, `getCommunitySummary`, `listRoleTemplates`, `listCommunityRoles`, `createCommunityRole`, `updateCommunityRole`, `deleteCommunityRole`
- Notifications: `listNotifications`, `markNotificationRead`, `setNotificationReadState`, `markAllNotificationsRead`
- Audit: `listSystemAuditLogs` returns `PagedResult<SystemAuditLog>`
- Forms: `listForms`, `createForm`, `updateForm`, `getForm`
- Processes: `startProcess`, `listProcesses`, `getProcess`
- Tasks: `listMyTasks`, `executeTaskAction`

Feature components should call these client methods through feature-level orchestration instead of calling `fetch` directly.

## Community Management Additions

- `PATCH /api/communities/{id}` updates name, description, invite code and active status. Invite codes are unique, uppercase five-character alphanumeric values; blank code on creation means the server generates one.
- `GET /api/communities/{id}/summary` returns active member count and member counts grouped by community role. It avoids loading the whole user list merely to render dashboard-like counts.
- `DELETE /api/communities/{id}/roles/{roleId}` receives `{ replacementRoleId }`. It moves active memberships to the target role and removes the role in one transaction. System roles, including `Atanmadi`, cannot be removed.
- Deactivating a community revokes normal member sessions and refresh tokens. Normal-member login, refresh, form creation/update, process start and task actions are denied until the scoped Topluluk Admin or a SuperAdmin reactivates that community.
- A Topluluk Admin with `Community.ManageAdmins` may toggle only its own community status. It cannot edit name, description or invite code; the acting admin session and normal permission-aware workspace navigation remain available.
- `GET /api/users` accepts legacy `status` plus repeatable `statuses=Active&statuses=Rejected` query values. `statuses` uses OR matching and preserves server-side pagination.

## Implemented Backend Structure

- `TechYouthBpm.Domain`: entities and enums.
- `TechYouthBpm.Application`: DTOs, service interfaces and `ProcessStateMachine`.
- `TechYouthBpm.Infrastructure`: EF Core `AppDbContext`, SQLite/PostgreSQL provider setup, seed data and service implementations.
- `TechYouthBpm.Api`: controllers, Swagger, CORS and startup database seeding.

The API accepts the session token as `Authorization: Bearer <token>`.

Enum values are returned as readable strings, for example `Admin`, `InProgress` and `Approve`. This keeps the frontend role checks and status displays explicit.

Task actions load the task and parent process, validate role/action, update process status through `ProcessStateMachine`, and then write a separate `AuditLog` row. The detail response is reloaded from the database after save so UI state reflects persisted data, not a temporary in-memory object graph.

## Database Configuration

The API reads `Database:Provider` from configuration:

- `Sqlite`: default local mode, using `Data Source=techyouth-bpm.db`.
- `PostgreSql`: shared database mode, intended for Neon or another PostgreSQL host.

The connection string is read from `ConnectionStrings:DefaultConnection`. Real PostgreSQL credentials must be supplied through environment variables, .NET user secrets or a gitignored `.env.neon.local` file. Tracked documentation and config files only contain examples.

Local SQLite and shared Neon setup, current schema summary and reset/start commands are documented in `docs/08-local-database.md`. Any schema, seed data or local startup change should update that document and the matching startup script.

The local startup script enables `Seed__MockData=true` by default. This adds two form definitions, football-themed process submissions, open approver tasks and completed/rejected audit examples. Use `-SkipMockData` when a teammate needs a nearly empty local database.

## Email Configuration

The API reads email delivery settings from the `Email` configuration section:

- `Email:Provider`: `Demo`, `Mailtrap`, `Smtp` or `Routing`.
- `Email:FromAddress`: sender address used in the email envelope.
- `Email:FromName`: display name shown to the recipient.
- `Email:AllowedRecipients`: optional comma-separated safety allowlist for real SMTP recipients.
- `Email:AllowedUsernames`: optional comma-separated safety allowlist for usernames allowed to receive real SMTP emails.
- `Email:SandboxDomains`: optional comma-separated domains that should always route to sandbox delivery. Default demo convention is `@techyouth.local`.
- `Email:Smtp:Host`, `Email:Smtp:Port`, `Email:Smtp:Username`, `Email:Smtp:Password`, `Email:Smtp:EnableSsl`: SMTP delivery settings.
- `Email:Sandbox:*`: fallback SMTP settings used by `Routing` mode for Mailtrap Sandbox capture delivery.

Default local mode is `Demo`, so the project runs without an external mail service. `Routing` mode can use Gmail SMTP for real allowlisted users while sending `@techyouth.local` demo addresses to Mailtrap Sandbox. Mailtrap/Gmail credentials should be set with .NET user secrets or environment variables. Tracked config files only contain placeholders.

Mailtrap Sandbox credentials capture emails inside the Mailtrap inbox and do not deliver to real recipients. Real inbox delivery requires Gmail SMTP app password, Mailtrap Email Sending with a verified sending domain or another production mail provider. For a narrow real-delivery test, set `Email:AllowedRecipients` and `Email:AllowedUsernames` to a private allowlist before using live SMTP credentials.
