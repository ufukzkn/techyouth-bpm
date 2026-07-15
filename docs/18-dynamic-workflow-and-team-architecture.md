# Dynamic Workflow And Team Architecture

## Purpose

This document is the shared contract for the next BPM expansion. It separates three concepts that must not be confused:

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

## Planned Workflow Model

The current state machine remains the production path until the dynamic runtime is ready. The planned model adds immutable published versions:

- `FormDefinitionVersion` and multi-page form schemas.
- `ProcessDefinitionVersion` with a versioned JSON graph.
- Start, User Task, Exclusive Gateway and End nodes.
- Person, team, community role and team-plus-role assignment targets.
- Candidate task pools with transactional claim/release behavior.
- Step execution history, node-level form output and pinned versions.

The visual editor will use `@xyflow/react`; existing form field ordering continues to use `@dnd-kit`. Camunda and Kissflow inform the modeling experience, but the application will run its own typed .NET workflow runtime rather than deploy BPMN XML to an external engine.

## Runtime Safety

Published definitions are immutable. A running instance stays pinned to the version with which it started. Gateway conditions use typed field/operator/value structures; arbitrary JavaScript is forbidden. Start, task creation, transition, notification and audit writes share a transaction. Automatic routing has a hop limit, and task claiming requires concurrency protection.

Task priority (`Low`, `Normal`, `High`, `Critical`) belongs to the future task/workflow model owned by Cagdas. It is not added to the current `ProcessTask` model in the team-foundation package.

## Ownership And Integration Order

- Ufuk: team/membership model, permissions, management UI, navigation, audit and notification integration.
- Ozgun: form versioning, multi-page designer/runner and task-form binding.
- Cagdas: graph schema/validator, visual workflow editor, runtime, assignment, claim and task priority.

Integration order is contract first, team API second, form versions third and runtime last. The legacy one-step process remains available until the new runtime passes end-to-end tests.

## PDF Fit

The PDF-required form components, validation, JSON submission, first task, assigned work, approve/reject, status dates, Swagger, EF Core and audit behavior remain intact. Teams and the visual workflow designer extend the bonus role assignment and drag/drop expectations without weakening the required baseline.
