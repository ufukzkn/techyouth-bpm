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
- Settings now includes profile, email verification and active session management.
- Admin user approval/role management moved to a separate `Kullanicilar / Roller` route instead of being embedded inside settings.
- Admin system history moved to a separate `Loglar` route. Logs are searched and paginated instead of being dumped as one long list.
- Email verification has a local demo-code flow; production should replace it with a real email delivery service.
- Admin users can approve pending registrations, reject accounts and assign roles from the settings screen.
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
- Identity/access actions are written to `SystemAuditLogs` so Admin can review who registered, signed in, changed access, verified email or revoked sessions.

## Current Dashboard Behavior

- `Bekleyen isler` routes approvers/admins to `Islerim`.
- `Devam eden surecler` routes users to `Surecler`.
- `Tamamlanan surecler` routes users to `Surecler`.
- Flow shortcuts only appear when the active role can open the target screen.
- Returning to the dashboard reuses the latest loaded metric values while the API refreshes in the background.
- If API metrics cannot be loaded, the dashboard keeps the user in place and shows an error message instead of logging out.

## Extensibility Notes

- Adding a new screen should update `navigation.ts`, the matching route page under `apps/web/src/app`, the shell view switch and any dashboard shortcuts that should point to it.
- Admin-only access screens currently include `/users` for user/role management and `/logs` for focused audit search.
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
