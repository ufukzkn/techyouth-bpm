# Ufuk Access And Workspace Flow

## Purpose

This document tracks Ufuk's ownership area: access, authenticated shell, dashboard and workspace navigation. The goal is to make the app feel like a coherent role-aware workspace, not a collection of disconnected screens.

## Scope Boundary

This work coordinates the user entry and navigation experience. It does not own form modeling rules or process state transitions, but it makes those flows discoverable through role-aware navigation and dashboard actions.

## Completed Work

- Login starts empty and supports demo-account fill buttons for local testing.
- Session state stores user, role, token, expiry and theme through Zustand.
- Restored API sessions are verified once through `/api/auth/me`; the app does not poll the user every second.
- The shell schedules one timeout from the stored `expiresAt` value. Expired or unauthorized sessions return to login with a visible notice.
- API session duration is read from `Auth:SessionDurationMinutes`; local timeout testing currently uses 1 minute.
- Demo fallback sessions also respect the local expiry timer, but skip `/api/auth/me` because they do not exist in the API session table.
- The authenticated shell filters menu items by role.
- Workspace navigation uses the `?view=` query parameter instead of hash-scroll sections.
- Dashboard metrics are loaded from process/task API data.
- Dashboard metric cards navigate to the related workspace area when the user role has access.
- BPM flow steps on the dashboard now act as role-aware shortcuts.
- The top bar uses a compact session icon. Clicking it opens session details: display name, username, role and expiry time.
- Session details are informational only; actual session expiry handling stays in the centralized shell effect.

## Current Dashboard Behavior

- `Bekleyen isler` routes approvers/admins to `Islerim`.
- `Devam eden surecler` routes users to `Surecler`.
- `Tamamlanan surecler` routes users to `Surecler`.
- Flow shortcuts only appear when the active role can open the target screen.
- If API metrics cannot be loaded, the dashboard keeps the user in place and shows an error message instead of logging out.

## Extensibility Notes

- Adding a new screen should update `navigation.ts`, the shell view switch and any dashboard shortcuts that should point to it.
- Adding a new role should update navigation visibility rules and dashboard shortcut availability together.
- Session-expiry behavior should stay centralized in `AppShell`/`sessionStore` instead of being duplicated in feature screens.
- A production-ready "remember me" option should be implemented as a separate longer-lived remember/refresh token flow, not by simply extending every normal session.

## Files Changed

- `apps/web/src/features/auth/LoginView.tsx`
- `apps/web/src/features/session/sessionStore.ts`
- `apps/web/src/features/app-shell/AppShell.tsx`
- `apps/web/src/features/app-shell/navigation.ts`
- `apps/web/src/app/globals.css`
- `docs/10-ufuk-access-shell-flow.md`

## Verification

Run these checks after workspace-shell changes:

```bash
cd apps/web
npm run lint
npm run build
```

The backend test suite should also stay green because dashboard metrics depend on existing process/task API contracts.
