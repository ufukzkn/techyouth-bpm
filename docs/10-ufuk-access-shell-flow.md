# Ufuk Access And Workspace Flow

## Purpose

This document tracks Ufuk's ownership area: access, authenticated shell, dashboard and workspace navigation. The goal is to make the app feel like a coherent role-aware workspace, not a collection of disconnected screens.

## Scope Boundary

This work coordinates the user entry and navigation experience. It does not own form modeling rules or process state transitions, but it makes those flows discoverable through role-aware navigation and dashboard actions.

## Completed Work

- Login starts empty and supports demo-account fill buttons for local testing.
- Login view now also supports registration. New accounts are created as `PendingApproval` and cannot sign in until an Admin activates them.
- Session state stores user, role, token, expiry and theme through Zustand.
- Theme starts from the operating system preference when the user has no saved manual choice; after the user toggles theme, that explicit choice is persisted.
- Theme toggle uses a CSS moon/sun transition inspired by the referenced CodePen interaction, implemented locally as `ThemeToggleButton` without adding a package.
- Language preference is persisted in the same session store and can be switched from both login and authenticated top bar.
- Language toggle uses a small CSS globe/orbit microinteraction with the active `TR/EN` code, implemented locally as `LanguageToggleButton`.
- Restored API sessions are verified once through `/api/auth/me`; the app does not poll the user every second.
- The shell schedules one timeout from the stored `expiresAt` value. Expired or unauthorized sessions return to login and show a confirmable alert dialog.
- API session duration is read from `Auth:SessionDurationMinutes`; the normal local duration is currently 120 minutes.
- `Beni hatirla` sends `rememberMe=true` during login and uses `Auth:RememberMeDurationMinutes` for a longer session.
- Long remember-me sessions are checked with capped browser timers so the timeout scheduler does not overflow for durations longer than the browser's maximum `setTimeout` delay.
- Demo fallback sessions also respect the local expiry timer, but skip `/api/auth/me` because they do not exist in the API session table.
- Passwords are verified through PBKDF2 hashes and session tokens are stored as SHA-256 hashes in the database.
- Login/register endpoints are rate limited, and repeated failed login attempts temporarily lock the account.
- Logout revokes the backend session instead of only clearing frontend state.
- Settings now includes editable profile details, password change, email verification and active session management.
- Settings profile, password and active-session areas are collapsible disclosure cards. This keeps the account page compact while preserving the full workflows behind explicit user intent.
- Settings action buttons are right-aligned inside their forms, and active-session lists are paginated to avoid long account pages.
- Shared pagination controls support direct page-number entry as well as previous/next buttons, so long lists do not require stepping through pages one by one.
- Email changes reset verification status and clear any previous verification code.
- Admin-created accounts can start with a temporary password. Those users are restricted to settings until they change the password.
- Admin user approval/role/session management moved to a separate `Yonetim` route instead of being embedded inside settings.
- Admin system history moved to a separate `Loglar` route. Logs are categorized, searched and paginated instead of being dumped as one long list.
- Email verification has a provider-based OTP flow. `Demo` mode shows a local code for development; `Mailtrap`/`Smtp` mode sends the code by email. `Routing` mode sends allowlisted users through live SMTP and sends everyone else to Mailtrap Sandbox.
- The verification panel is rendered outside the settings summary grid, so opening the code form does not stretch the profile/session cards.
- Outgoing verification and temporary-password emails use a simple HTML card template so the Mailtrap preview is readable during demo.
- The email verification panel is two-step: opening the panel does not send mail; the user explicitly clicks `Kod gonder`. Codes are valid for 24 hours by default.
- After sending a code, the UI shows a 5-minute resend countdown. The backend also blocks immediate resend requests so the cooldown is not only cosmetic.
- OTP generation and verification now live behind `IOtpService`, and mail delivery lives behind `IEmailSender`, so AuthService only coordinates the email verification workflow.
- Admin users can approve pending registrations, reject accounts and assign roles from the `Yonetim` screen.
- The `Yonetim` user search is server-side paged and debounced. Search/status/page changes call `/api/users` with query parameters instead of loading every user into the browser.
- Server-paged user and audit lists prefetch the previous and next page into a small in-memory cache after the current page loads. This keeps near-page navigation fast without downloading the full table.
- `Yonetim` and `Loglar` include manual refresh buttons that clear the local page cache and reload the current server-filtered data.
- Identity/access status messages use tone-aware alert styling: success messages are green, errors are red and neutral progress/info messages stay neutral.
- Admin users can create a new user with role, status and temporary password from the `Yonetim` screen.
- The user creation form sits below the searchable/paginated user list in the left management column and opens as a compact animated disclosure panel, keeping listing and review as the primary screen flow.
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
- Workspace navigation uses real route paths such as `/dashboard`, `/forms`, `/tasks` and `/settings` instead of hash-scroll sections or query-only views.
- Desktop navigation stays fixed on the left while the workspace scrolls.
- On tablet/mobile widths, workspace navigation is collapsed behind a fixed hamburger button and opens as a drawer with backdrop/escape-close behavior.
- Dashboard metrics are loaded from process/task API data.
- Dashboard metrics keep the last loaded values while refreshing, so the cards do not flash to placeholder values during fast navigation.
- Dashboard metric cards navigate to the related workspace area when the user role has access.
- BPM flow steps on the dashboard now act as role-aware shortcuts.
- Process/task refresh keeps the visible data on screen, shows inline button loading and reports success/error with a bottom-right toast.
- The top bar uses a compact session icon. Clicking it opens session details: display name, username, role and expiry time.
- Session details are informational only; actual session expiry handling stays in the centralized shell effect.
- Active sessions can be listed and revoked from settings. Revoking the current session logs the user out.
- Settings includes a `Tum cihazlardan cikis yap` action, which revokes non-current sessions first and then revokes the current session.
- Identity/access actions are written to `SystemAuditLogs` so Admin can review who registered, signed in, changed access, updated profile/email, changed password, verified email, created users or revoked sessions.

## Current Dashboard Behavior

- `Bekleyen isler` routes approvers/admins to `Islerim`.
- `Devam eden surecler` routes users to `Surecler`.
- `Tamamlanan surecler` routes users to `Surecler`.
- Flow shortcuts only appear when the active role can open the target screen.
- Returning to the dashboard reuses the latest loaded metric values while the API refreshes in the background.
- If API metrics cannot be loaded, the dashboard keeps the user in place and shows an error message instead of logging out.

## Extensibility Notes

- Adding a new screen should update `navigation.ts`, the matching route page under `apps/web/src/app`, the shell view switch and any dashboard shortcuts that should point to it.
- Admin-only access screens currently include `/users` for user/role management and `/logs` for categorized audit search.
- Adding a new role should update navigation visibility rules and dashboard shortcut availability together.
- Desktop navigation should stay fixed because the menu is short and should remain available during long workflow screens.
- Mobile navigation should stay drawer-based with a fixed floating trigger so the dashboard/content remains the first visual focus on small screens.
- Session-expiry behavior should stay centralized in `AppShell`/`sessionStore` instead of being duplicated in feature screens.
- User-action traceability has two levels: `AuditLogs` for process state history and `SystemAuditLogs` for broader identity/access/form/process/task events.
- The current remember-me option is useful for the project demo, but a stronger production version should move toward refresh-token rotation and explicit device/session management.
- The current token model stays as opaque server-side sessions because pending approval, lockout and revoke all need server-side state. JWT can be considered later only with refresh-token rotation and explicit session/device management.
- Theme ownership should stay centralized in `sessionStore`; feature screens should read the active theme only through shared styling tokens.
- Static shell/login/dashboard/process text should use the shared i18n dictionary instead of inline copy.

## Files Changed

- `apps/web/src/features/auth/LoginView.tsx`
- `apps/web/src/features/session/sessionStore.ts`
- `apps/web/src/features/app-shell/AppShell.tsx`
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
