# Agent Notes

These notes are the project memory. Update this file whenever an implementation choice affects future work.

## Fixed Decisions

- Scope: full-stack.
- Repository target: private `ufukzkn/techyouth-bpm`.
- Frontend: Next.js App Router, TypeScript, Zustand.
- Backend: .NET 8 Web API, EF Core, SQLite.
- Documentation must be kept current as code changes.
- Team split: flow-based, not layer-based.

## Tooling Notes

- Node and npm are installed.
- .NET SDK 10 is installed and can target `net8.0`.
- Git history should stay progressive and easy to review.
- Repository documentation should avoid machine-specific paths, credentials, tokens, or private workflow details.

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
- Frontend app shell includes login, role-aware navigation, dashboard preview, theme toggle and demo-user fallback when API is offline.
- Form designer draft added with local field editing and JSON model preview.
- Process board draft added with local task actions, status transitions and audit preview.
- Form runner draft added with dynamic field rendering, required/type/dependent validation and JSON submit preview.
- Frontend API client expanded with form, process and task methods so feature components can be wired without scattering fetch calls.
