# Ufuk Access And Workspace Flow

## Purpose

This document tracks Ufuk's ownership area: access, authenticated shell, dashboard and workspace navigation. The goal is to make the app feel like a coherent role-aware workspace, not a collection of disconnected screens.

## Scope Boundary

This work coordinates the user entry and navigation experience. It does not own form modeling rules or process state transitions, but it makes those flows discoverable through role-aware navigation and dashboard actions.

## Completed Work

- Login starts empty and supports demo-account fill buttons for local testing.
- Login view now also supports registration. New accounts must provide a community code, are created as `PendingApproval` in that community and cannot sign in until a community admin activates them.
- Runtime session state keeps the resolved user and expiry in memory through Zustand; persisted Zustand storage contains only theme and language preferences. Browser auth secrets remain in HttpOnly cookies.
- Theme starts from the operating system preference when the user has no saved manual choice; after the user toggles theme, that explicit choice is persisted.
- Theme toggle uses a CSS moon/sun transition inspired by the referenced CodePen interaction, implemented locally as `ThemeToggleButton` without adding a package.
- Language preference is persisted in the same session store and can be switched from both login and authenticated top bar.
- Language toggle uses a small CSS globe/orbit microinteraction with the active `TR/EN` code, implemented locally as `LanguageToggleButton`.
- Restored API sessions are verified once through `/api/auth/me`; the app does not poll the user every second.
- The shell schedules one timeout from the in-memory `expiresAt` value. On a hard refresh it rebuilds identity through `/api/auth/me`; expired or unauthorized sessions attempt one refresh rotation and otherwise return to login.
- API session duration is read from `Auth:SessionDurationMinutes`; the normal local duration is currently 120 minutes.
- `Beni hatirla` sends `rememberMe=true` during login and creates a longer-lived refresh token while the access session stays short.
- Remember-me now creates a hashed, rotating refresh token tied to a remembered device. Access sessions remain opaque server-side sessions; refresh rotation replaces the old session/token pair and flags revoked-token reuse as a suspicious audit event.
- `/api/auth/login` still returns a Bearer token for Swagger/dev usage. The web application uses `/api/auth/browser-login`, whose response omits auth secrets and establishes HttpOnly access/refresh cookies plus a readable CSRF cookie for protected mutations.
- If an access session expires and a valid refresh cookie exists, the shell attempts a silent refresh before showing the timeout dialog.
- Long remember-me sessions are checked with capped browser timers so the timeout scheduler does not overflow for durations longer than the browser's maximum `setTimeout` delay.
- Demo fallback sessions also respect the local expiry timer, but skip `/api/auth/me` because they do not exist in the API session table.
- Passwords are verified through PBKDF2 hashes and session tokens are stored as SHA-256 hashes in the database.
- Login/register endpoints are rate limited, and repeated failed login attempts temporarily lock the account.
- Logout revokes the backend session instead of only clearing frontend state.
- Settings now includes editable profile details, password change, email verification and active session management.
- Login now includes password reset and public email verification flows. Pending-approval users can verify email before admin approval, and password reset completion revokes existing sessions. Password reset emails include a direct reset link generated from `Frontend:BaseUrl`; demo mode can expose the token in the response for local debugging.
- Settings profile, password and active-session areas are collapsible disclosure cards. This keeps the account page compact while preserving the full workflows behind explicit user intent.
- Settings action buttons are right-aligned inside their forms, and active-session lists are paginated to avoid long account pages.
- Shared pagination controls support direct page-number entry as well as previous/next buttons, so long lists do not require stepping through pages one by one.
- Email changes reset verification status and clear any previous verification code.
- Admin-created accounts can start with a temporary password. Those users are restricted to settings until they change the password.
- Admin user approval/role/session management moved to management routes instead of being embedded inside settings.
- Management now has dedicated route surfaces: `/management/users` for user approval/access/session/password reset and `/management/communities` for community codes and custom role templates.
- Admin system history moved to a separate `Loglar` route. Logs are categorized, searched and paginated instead of being dumped as one long list.
- Email verification has a provider-based OTP flow. `Demo` mode shows a local code for development; `Mailtrap`/`Smtp` mode sends the code by email. `Routing` mode sends allowlisted users through live SMTP and sends everyone else to Mailtrap Sandbox.
- The verification panel is rendered outside the settings summary grid, so opening the code form does not stretch the profile/session cards.
- Outgoing verification and temporary-password emails use a simple HTML card template so the Mailtrap preview is readable during demo.
- The email verification panel is two-step: opening the panel does not send mail; the user explicitly clicks `Kod gonder`. Codes are valid for 24 hours by default.
- After sending a code, the UI shows a 5-minute resend countdown. The backend also blocks immediate resend requests so the cooldown is not only cosmetic.
- OTP generation and verification now live behind `IOtpService`, and mail delivery lives behind `IEmailSender`, so AuthService only coordinates the email verification workflow.
- Admin users can approve pending registrations, reject accounts and assign roles from the `Yonetim` screen.
- The `Yonetim` user search is server-side paged and debounced. Search/status/page changes call `/api/users` with query parameters instead of loading every user into the browser.
- Server-paged user and audit lists use single-page lazy loading. Page changes fetch only the active page, so previous/next prefetch does not repeat extra requests.
- `Loglar` keeps category counts in a small query-based cache; the manual refresh button clears that count cache and reloads the current server-filtered data.
- `Yonetim` and `Loglar` include manual refresh buttons that reload the current server-filtered data without dumping large tables into the browser.
- Identity/access status messages use tone-aware alert styling: success messages are green, errors are red and neutral progress/info messages stay neutral.
- Admin users can create a new user with role, status and temporary password from the `Yonetim` screen.
- The user creation form sits under the right-side detail area and opens as a compact animated disclosure panel, keeping the left column focused on search/listing.
- The user detail panel is visible as a placeholder by default; clicking `Detay` expands the existing right-side panel instead of creating a new panel from nothing.
- Management and email-verification disclosure panels use a slower soft-reveal transition instead of instant toggles.
- User-detail history and related audit timelines are paginated, so destructive actions such as user deletion stay reachable without scrolling through very long logs.
- System logs now use roomier audit cards and searchable chronological history. The main log search is server-side paged and debounced through `/api/audit/system`, and category cards use `/api/audit/system/counts`, so large audit tables are not loaded into the browser. Timelines show the newest audit event first. After opening `Ilgili gecmis`, the right-side panel can switch between action context, actor history and affected-user history. Action context is intentionally an intersection filter: the same actor's actions on the same target entity.
- The audit-history perspective switcher uses a compact liquid-slider radio control with explicit selected state, instead of a plain segmented button group.
- The affected-user label in audit history is resolved from the log's `User` entity id when possible, so entries such as Admin-created users point to the created user instead of the Admin actor.
- Frontend date/time display and verification email expiry text are shown in Turkey time with an explicit `GMT+3` suffix.
- Admin user creation defaults to backend-generated temporary passwords. Admins can opt into a manual temporary password with a checkbox when needed.
- Admin-created temporary passwords are sent through the configured email provider; Mailtrap/Smtp mode sends a real sandbox/SMTP email.
- Users created with a temporary password see a blocking password-change gate instead of the normal workspace menu until the password is changed.
- Admin users can delete test users from the detail panel. The backend refuses self-delete and users with workflow history so BPM audit traceability is not broken.
- Role/status edits are staged locally and only sent after the Admin clicks `Degisikligi uygula` and confirms the critical access dialog.
- Admin users can inspect a selected user's active sessions from the same detail panel, see session device/IP metadata and revoke a session after confirmation.
- The authenticated shell filters menu items by role.
- The authenticated shell now filters menu items by permission first. `SuperAdmin` sees platform-level management, while community users only see screens covered by their active `CommunityRolePermission` records.
- `SuperAdmin` can create a brand-new `SuperAdmin` account, but existing users cannot be promoted to `SuperAdmin` from management. This keeps platform-level access intentional.
- `SuperAdmin` can reset non-SuperAdmin passwords with an emailed temporary password. Community admins cannot reset passwords.
- Auth responses include community name, community role and permission list, so the top-level workspace can explain "where" and "with which rights" the user is working.
- Dashboard copy and visual metrics include community context, making demo users such as Sportif Faaliyetler or Lojistik feel like separate working groups instead of one flat tenant.
- The `Yonetim` route now includes the first custom role flow: role templates, community role creation and permission checkbox selection.
- Built-in demo communities are `Sportif Faaliyetler`, `Lojistik` and `Urun Siparisi`.
- The authenticated workspace uses a shared `(workspace)` App Router layout. `WorkspaceSidebar`, `WorkspaceTopbar`, `NotificationMenu`, `WorkspaceSessionController` and `WorkspaceLoadingShell` keep shell behavior focused while route views remain feature-owned.
- Each workspace route under `apps/web/src/app` imports its own view component. This uses the Next.js App Router more directly and avoids one giant active-view switch loading every screen from the shell.
- Workspace navigation uses real route paths such as `/dashboard`, `/forms`, `/tasks` and `/settings` instead of hash-scroll sections or query-only views.
- Desktop navigation stays fixed on the left while the workspace scrolls.
- On tablet/mobile widths, workspace navigation is collapsed behind a fixed hamburger button and opens as a drawer with backdrop/escape-close behavior.
- Dashboard metrics and compact recent-work lists are loaded from `GET /api/dashboard/summary`.
- Dashboard now includes a compact donut distribution for pending tasks, in-progress processes and completed processes.
- Dashboard metrics keep the last loaded values while refreshing, so the cards do not flash to placeholder values during fast navigation.
- Dashboard metric cards navigate to the related workspace area when the user role has access.
- `Oncelikli Islerim` and `Son Sureclerim` show at most four newest records already filtered by the current user's permissions and community.
- Compact dashboard actions only appear when the current user can open their target route.
- Process/task refresh keeps the visible data on screen, shows inline button loading and reports success/error with a bottom-right toast.
- The top bar places the compact session icon next to the active user identity. Clicking it opens session details: display name, username, role and expiry time.
- Dashboard context and the session popover show the user's active team names without turning team leadership into an access rule.
- The top bar includes a notification dropdown backed by `/api/notifications`, with unread count and `Tumunu okundu yap`.
- Session details are informational only; actual session expiry handling stays in the centralized shell effect.
- Active sessions can be listed and revoked from settings. Revoking the current session logs the user out.
- Settings includes a `Tum cihazlardan cikis yap` action, which revokes non-current sessions first and then revokes the current session.
- Identity/access actions are written to `SystemAuditLogs` so Admin can review who registered, signed in, changed access, updated profile/email, changed password, verified email, created users or revoked sessions.
- Auth, registration, account, session and user-administration controllers now consume focused Application interfaces. The Infrastructure implementation may remain shared internally, but HTTP boundaries no longer depend on one oversized auth contract.
- Community metadata/lifecycle and community-role CRUD use separate service contracts; user membership operations stay in user administration.
- Inbox results now use a user/filter/page-scoped Zustand cache with stale-while-revalidate behavior. Returning to `/inbox` shows cached content immediately, optimistic read-state updates roll back on API failure and only post-baseline visible polling creates live notification toasts.
- `/management/teams` implements community-scoped teams, multi-team memberships and a virtual `Takimsiz` view. Team leadership is informational and never bypasses `Teams.View` or `Teams.Manage` checks.

## Current Dashboard Behavior

- `Bekleyen isler` routes approvers/admins to `Islerim`.
- `Devam eden surecler` routes users to `Surecler`.
- `Tamamlanan surecler` routes users to `Surecler`.
- Work lists and quick actions only appear within the active user's permission/community scope.
- Returning to the dashboard reuses the latest loaded metric values while the API refreshes in the background.
- If API metrics cannot be loaded, the dashboard keeps the user in place and shows an error message instead of logging out.

## Extensibility Notes

- Adding a new screen should update `navigation.ts`, the matching route page under `apps/web/src/app` and any dashboard shortcuts that should point to it.
- Permission-scoped management routes include `/management/users`, `/management/communities` and `/management/teams`; `/logs` remains the categorized audit search.
- Adding a new enum role should be rare; normal business access should be added through community roles and permission records.
- Adding a new permission should update `PermissionNames`, backend service checks, `navigation.ts` and the management role-template UI.
- Desktop navigation should stay fixed because the menu is short and should remain available during long workflow screens.
- Mobile navigation should stay drawer-based with a fixed floating trigger so the dashboard/content remains the first visual focus on small screens.
- Session-expiry behavior stays centralized in `WorkspaceSessionController`/`sessionStore` instead of being duplicated in feature screens.
- User-action traceability has two levels: `AuditLogs` for process state history and `SystemAuditLogs` for broader identity/access/form/process/task events.
- The current token model stays as opaque server-side sessions plus rotating refresh tokens because pending approval, lockout, refresh reuse detection and revoke all need server-side state. JWT can be considered later only if it keeps equivalent refresh-token rotation and explicit session/device management.
- Role and team permissions are not copied into the opaque token. Each protected request resolves the server-side session and rebuilds the user DTO from active database memberships, so role/team changes apply to an already-open session immediately. The web client now uses cookie-only transport: auth secrets stay in HttpOnly cookies, session identity is rebuilt through `/api/auth/me`, refresh recovery rotates the cookie, and Zustand/localStorage contains only theme and language preferences. Swagger Bearer support remains independent.
- Theme ownership should stay centralized in `sessionStore`; feature screens should read the active theme only through shared styling tokens.
- Static shell/login/dashboard/process text should use the shared i18n dictionary instead of inline copy.
- Team and workflow boundaries remain distinct: teams describe where work is performed, community roles describe what is allowed, and Cagdas's runtime combines these contracts for assignment, priority and candidate claim.

## Files Changed

- `apps/web/src/features/auth/LoginView.tsx`
- `apps/web/src/features/session/sessionStore.ts`
- `apps/web/src/app/(workspace)/layout.tsx`
- `apps/web/src/features/app-shell/components/WorkspaceSessionController.tsx`
- `apps/web/src/features/app-shell/components/WorkspaceSidebar.tsx`
- `apps/web/src/features/app-shell/components/WorkspaceTopbar.tsx`
- `apps/web/src/features/app-shell/navigation.ts`
- `apps/web/src/app/*/page.tsx`
- `apps/web/src/app/globals.css`
- `docs/10-ufuk-access-shell-flow.md`

## Verification

Run these checks after workspace-shell changes:

```bash
cd apps/web
npm run lint
npm run build
```

The backend test suite should also stay green because dashboard metrics depend on existing process/task API contracts:

```bash
dotnet test apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj
```
