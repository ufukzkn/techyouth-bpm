# Dynamic Workflow And Team Architecture

## Purpose

This document records the implemented shared contract for the dynamic BPM expansion. It separates three concepts that must not be confused:

- `Community`: tenant-like security and data boundary.
- `Team`: an operational group inside one community, such as Scout, Finance or Logistics.
- `CommunityRole`: reusable business permissions, such as Form Designer or Approval Specialist.

A user keeps one active community role and may join multiple teams. Team leadership is descriptive context (`IsLead`); it never bypasses permission checks.

## Team Foundation

`Team` stores community, normalized name, description, active state, creator and timestamps. `TeamMembership` stores team, user, active state, lead state and timestamps. A case-insensitive unique index on community plus normalized name prevents duplicate team names.

`Takimsiz` is a virtual query result, not a database row. It contains active, approved community members with no active membership in an active team. It cannot be edited, deactivated or selected as a workflow assignment target.

Access rules:

- SuperAdmin can inspect and manage teams in every community.
- Topluluk Admin can manage only teams and users in their own community.
- Only active, approved users from the same community may join a team.
- Moving a user to another community deactivates old team memberships.
- `Teams.View` and `Teams.Manage` are explicit permissions; `IsLead` grants neither.

This foundation is implemented. `/management/teams` provides scoped team selection, create/edit, member search, candidate assignment, lead changes, removal and the virtual unassigned list. SuperAdmin can select any community; a Topluluk Admin remains locked to its own community. Lists are paged and searched on the server, cached data stays visible during refresh and destructive membership changes use confirmation plus card-local feedback.

The user-management detail now loads team memberships on demand and reuses the same protected member mutations. SuperAdmin can manage any community user's teams; Topluluk Admin remains limited to its own community. `/teams` is a separate personal route where every active team member can inspect only their own teammates through a reduced roster DTO that excludes email, session and audit data.

Implemented API contracts are `GET/POST /api/teams`, `GET/PATCH /api/teams/{id}`, paged member/roster/candidate/unassigned queries, on-demand user memberships and add/update/remove membership endpoints. Membership mutations write system audit and target-user notifications. The deterministic seed supplies 16 teams across five communities, including leaders, multi-team users and intentionally unassigned users.

## Implemented Workflow Model

The executable DTO and graph decisions are fixed in [20-dynamic-workflow-contract.md](20-dynamic-workflow-contract.md). The implementation now includes:

- immutable published `FormDefinitionVersion` records with ordered pages and fields;
- immutable published `ProcessDefinitionVersion` records with typed JSON graphs;
- Start, User Task, Exclusive Gateway, Completed End, Rejected End and Team Swimlane nodes;
- person, process starter, team, community role and team-plus-role assignment targets;
- task priority, candidate pools, transactional claim/release and optimistic concurrency;
- pinned form/workflow versions, namespaced variables and node execution history.

The visual editor uses `@xyflow/react`; form page/field ordering continues to use `@dnd-kit`. An adapter keeps React Flow presentation state out of the API graph contract. Camunda and Kissflow inform the modeling experience, but the application runs its own typed .NET workflow runtime rather than deploying BPMN XML to an external engine.

The workspace route is `/workflows`. Form Runner lists only published workflows the active user can start and whose start-form version matches the selected form. The old form-id start endpoint remains available through `Legacy Basic Approval` for compatibility.

## Runtime Safety

Published definitions are immutable. A running instance stays pinned to the version with which it started. Gateway conditions use form-derived paths such as `start.bonservis` and typed operators; arbitrary JavaScript is forbidden. Start, task creation, transition, notification and audit writes share transactions. Automatic routing has a 100-hop limit. Non-`SendBack` cycles are rejected before publish, while `SendBack` may only target an earlier user task and creates a new attempt instead of rewriting history.

Task priority values are `Low`, `Normal`, `High` and `Critical`. Candidate-pool tasks require `Tasks.Act`; team-plus-role assignment resolves the intersection. `ClaimVersion` is an EF Core concurrency token, so two users working from the same snapshot cannot both claim one task.

## Seeded Demo

`Sportif Faaliyetler` includes a published `Transfer Talep Akisi`:

1. Transfer start form is submitted.
2. `Scout Ekibi` completes a scout report.
3. `Teknik Degerlendirme` approves, rejects or sends back.
4. `start.bonservis > 5,000,000` routes to `Mali Isler`; lower values skip that step.
5. `Transfer Operasyon` completes the contract form.
6. Every task form, actor, attempt, transition and output remains visible in process history.

Four swimlanes and four task forms are seeded. The same deterministic seed runs on local SQLite and PostgreSQL/Neon.

## Ownership And Integration Order

- Ufuk: team/membership model, permissions, management UI, navigation, audit and notification integration.
- Ozgun: form versioning, multi-page designer/runner and task-form binding.
- Cagdas: graph schema/validator, visual workflow editor, runtime, assignment, claim and task priority.

The shared contract was implemented in that order: team API, form versions, graph editor/validator, runtime and integration tests. The legacy one-step process remains available only as a compatibility path while new development uses version IDs.

## PDF Fit

The PDF-required form components, validation, JSON submission, first task, assigned work, approve/reject, status dates, Swagger, EF Core and audit behavior remain intact. Multi-page form drag/drop and the React Flow canvas directly strengthen the PDF bonus expectations for field ordering, role/user assignment and dynamic process design.

Verification covers graph validation, version immutability, gateway routing, task-form validation, rollback, team/role candidate resolution, stale-snapshot claim competition, HTTP publish/start/complete and SQLite/PostgreSQL migrations. Exact current counts are recorded in `docs/01-agent-notes.md` after each full verification run.
