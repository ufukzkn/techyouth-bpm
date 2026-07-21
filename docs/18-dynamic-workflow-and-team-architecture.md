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
- optional User Task SLA values, persisted task deadlines and server-side deadline/priority sorting;
- optional team-lead-only User Tasks, with candidate visibility separated from claim/action permission;
- pinned form/workflow versions, namespaced variables and node execution history.

The visual editor uses `@xyflow/react`; form page/field ordering continues to use `@dnd-kit`. An adapter keeps React Flow presentation state out of the API graph contract. Camunda and Kissflow inform the modeling experience, but the application runs its own typed .NET workflow runtime rather than deploying BPMN XML to an external engine.

The workspace route is `/workflows`. Form Runner lists only published workflows the active user can start and whose start-form version matches the selected form. The old form-id start endpoint remains available through `Legacy Basic Approval` for compatibility.

## Process Visibility Policy

`WorkflowVisibilityService` is the single backend policy used by dashboard summaries, process pages, task pages and process-detail authorization.

- `personal`: processes started by the user plus processes/tasks directly assigned, claimed or matched by the user's active team and community role.
- `community`: all processes in the active community; requires `Processes.ViewAll`.
- `global`: all communities; SuperAdmin-only.
- `GET /api/tasks/my` always remains the personal candidate pool and has no management-wide mode.

Dashboard and Process screens preserve the selected scope in the URL and cache by user plus scope. Wider counters are labeled as community/platform information rather than personal workload. Read, filter, paging and disclosure events are intentionally not audited; process starts, task claim/release/actions, definition publishing and team membership changes are.

## Runtime Safety

Published definitions are immutable. A running instance stays pinned to the version with which it started. Gateway conditions use form-derived paths such as `start.bonservis` and typed operators; arbitrary JavaScript is forbidden. Start, task creation, transition, notification and audit writes share transactions. Automatic routing has a 100-hop limit. Non-`SendBack` cycles are rejected before publish, while `SendBack` may only target an earlier user task and creates a new attempt instead of rewriting history.

Task priority values are `Low`, `Normal`, `High` and `Critical`. Candidate-pool tasks require `Tasks.Act`; team-plus-role assignment resolves the intersection. `ClaimVersion` is an EF Core concurrency token, so two users working from the same snapshot cannot both claim one task.

A Team or Team-and-Community-Role User Task may additionally set `RequiresTeamLead`. Matching members still see the task in their candidate pool, but only an active matching lead may claim or act on it. Publication fails when the selected team has no eligible active lead with `Tasks.Act`. The task snapshot persists this rule so a running process remains pinned to the published definition; `CanCurrentUserAct` and a stable denial-reason code let the UI explain the lock without replacing backend authorization.

User Task nodes may define an SLA between 1 minute and 365 days. The graph stores this value as minutes and each task attempt receives its own nullable `DueAt`; a task recreated after `SendBack` therefore receives a fresh deadline. This is deadline tracking rather than a background timer engine: automatic reminders and escalations remain a later extension.

Process and task boards query one server page at a time. Filtering and sorting run before pagination, and summaries carry workflow context, nearest deadline and highest priority. Deep links use the exact `processId` or `taskId`, so the UI never opens an unrelated first record.

## Seeded Demo

`Sportif Faaliyetler` includes a published `Transfer Teklif ve Onay Akışı`:

1. A two-page transfer offer form is submitted with all supported field types, conditional validation and file metadata.
2. `Scout Ekibi` completes a scout report.
3. `Teknik Degerlendirme` approves, rejects or sends back.
4. `start.bonservis > 5,000,000` routes to `Mali Isler`; lower values skip that step.
5. Team-lead-only Finance and final `Transfer Operasyon` tasks protect the critical decisions.
6. Every task form, actor, attempt, transition and output remains visible in process history.

`Lojistik` also includes an `Acil Sevkiyat ve Teslimat Akışı`. Its urgency gateway selects a two-hour or six-hour dispatch SLA, and its warehouse/delivery tasks demonstrate lead-only claim, `Complete`, `Reject`, `SendBack`, deadline ordering and delivery-proof metadata.

Published showcase form and workflow versions are added as new immutable versions rather than rewriting versions used by running processes. Binary files are intentionally outside the current contract: file fields validate and persist name, size, MIME type, extension and modification-time metadata only. The exact manual chain is documented in [22-workflow-end-to-end-test-scenarios.md](22-workflow-end-to-end-test-scenarios.md).

In addition, all five demo communities receive a published workflow with bound start/task forms and coherent process examples: overdue, upcoming, completed, rejected and sent-back. The deterministic seed removes only retired seed process IDs, preserves user-created process data and resolves old system-role IDs by community/template when upgrading an existing SQLite database. The same seed runs on local SQLite and PostgreSQL/Neon.

## Ownership And Integration Order

- Ufuk: team/membership model, permissions, management UI, navigation, audit and notification integration.
- Ozgun: form versioning, multi-page designer/runner and task-form binding.
- Cagdas: graph schema/validator, visual workflow editor, runtime, assignment, claim and task priority.

The shared contract was implemented in that order: team API, form versions, graph editor/validator, runtime and integration tests. The legacy one-step process remains available only as a compatibility path while new development uses version IDs.

## Portable Drafts

Form and workflow designers export versioned JSON envelopes named
`techyouth.form-draft` and `techyouth.workflow-draft`. Import always creates a
new editable draft; it never overwrites a published version. Workflow topology
and canvas geometry survive export, while environment-specific user, team,
community-role and form-version bindings are cleared and recorded in
`requiresBinding`. A draft may be saved after import but cannot be published
until those references are rebound. Files are limited to 1 MB and no imported
expression is executed as JavaScript.

## PDF Fit

The PDF-required form components, validation, JSON submission, first task, assigned work, approve/reject, status dates, Swagger, EF Core and audit behavior remain intact. Multi-page form drag/drop and the React Flow canvas directly strengthen the PDF bonus expectations for field ordering, role/user assignment and dynamic process design.

Verification covers graph validation, version immutability, gateway routing, task-form validation, rollback, team/role candidate resolution, stale-snapshot claim competition, HTTP publish/start/complete and SQLite/PostgreSQL migrations. Exact current counts and commands are recorded only in [Testing And Quality Gates](24-testing-and-quality-gates.md).
