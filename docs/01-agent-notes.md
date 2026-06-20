# Agent Notes

These notes are the project memory. Update this file whenever an implementation choice affects future work.

## Fixed Decisions

- Scope: full-stack.
- Repository target: private `ufukzkn/techyouth-bpm`.
- Local repository path: `C:\Users\ufuk_\OneDrive\Documents\eczacibasi`.
- Frontend: Next.js App Router, TypeScript, Zustand.
- Backend: .NET 8 Web API, EF Core, SQLite.
- Documentation must be kept current as code changes.
- Team split: flow-based, not layer-based.

## Tooling Notes

- Node and npm are installed.
- .NET SDK 10 is installed and can target `net8.0`.
- Git is initialized locally with no starting commits.
- GitHub App is connected to `ufukzkn`, but GitHub CLI is not installed.
- If repo creation/push is blocked, explain that `gh` or a pre-created remote is needed.

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
