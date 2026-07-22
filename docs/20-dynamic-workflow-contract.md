# Dynamic Workflow Contract

## Boundary And Versioning

`Community` remains the tenant and authorization boundary. `Team` is an operational group inside one community, while `CommunityRole` is a reusable permission bundle. A workflow cannot reference users, teams, roles or forms from another community.

`FormDefinition` and `ProcessDefinition` are logical identities. Their draft versions may change; published versions are immutable. A running process stores the exact process and form version identifiers used at start, so publishing a new version never changes an active instance.

## Graph Contract

`ProcessDefinitionVersion.GraphJson` is typed JSON with `schemaVersion`, `nodes` and `edges`.

- Node types: `Start`, `UserTask`, `ExclusiveGateway`, `CompletedEnd`, `RejectedEnd`, `TeamSwimlane`.
- Runtime nodes have stable string keys. Swimlanes are visual containers and are not executed.
- Nodes persist their canvas position and optional width/height together with the parent swimlane key. Saving and reopening a draft must reproduce the same layout.
- User tasks contain title, optional published form version, priority, action set, assignment and optional SLA duration in minutes.
- Assignment types: `ProcessStarter`, `SpecificUser`, `Team`, `CommunityRole`, `TeamAndCommunityRole`.
- Edges may contain an action, a typed condition, a default marker and deterministic order.
- Conditions reference published form paths such as `start.amount` or `steps.financeApproval.approvedBudget`; arbitrary code is forbidden.

Publish validation requires exactly one start, at least one end, reachable runtime nodes, valid same-community references, complete user-task assignments, action edges and a default gateway edge. Automatic routing is limited to 100 hops. `SendBack` may target an earlier user task, but creates a new execution attempt instead of reopening history.

The React Flow editor owns richer presentation state, while the API owns the provider-neutral graph contract. A dedicated adapter converts between these models; editor-only objects are never posted directly to the runtime API.

## Form Contract

A form version owns ordered pages; pages own ordered fields and validation rules. Field keys are unique across the whole version. A published version cannot be edited. Editing a published form clones it into a new draft version.

The runner validates the current page before navigation and the backend validates the complete submitted payload. Process variables are namespaced:

```json
{
  "start": { "amount": 125000 },
  "steps": {
    "financeApproval": { "approvedBudget": 120000 }
  }
}
```

## Task And Runtime Contract

Direct assignments (`ProcessStarter`, `SpecificUser`) do not require a separate claim. Team and role assignments are candidate pools and require `Tasks.Act` plus live membership/role eligibility. `TeamAndCommunityRole` uses the intersection. Candidate eligibility is checked again when claiming; `IsLead` grants no implicit permission.

`Tasks.ManageAll` is an explicit community-scoped operational override. It lets a community manager list and act on unclaimed work without matching the candidate team, role or team-lead condition. It never crosses the user's community and never permits taking over a task claimed by another user. SuperAdmin keeps the separate global platform behavior.

The active task list applies that override so community administrators can operate their community queue. The history view is deliberately actor-scoped for every role: `GET /api/tasks/my?view=history` returns only tasks whose `CompletedByUserId` is the signed-in user. Community-wide completed work remains available through process timelines and system audit instead of being presented as the administrator's personal history.

Only one user may claim a task. A provider-independent concurrency token protects simultaneous SQLite and PostgreSQL updates. Task priority values are `Low`, `Normal`, `High` and `Critical`. Optional SLA is validated between 1 minute and 365 days; the runtime calculates a nullable `DueAt` each time a task attempt is created, including a new attempt after `SendBack`.

Process start, task creation, process variables, step execution, notifications and audit entries share a transaction. Task action, next-node routing and completion use a second atomic transaction. Any failure rolls the whole operation back.

Runtime actions are `Approve`, `Reject`, `Complete`, `Escalate` and `SendBack`. A task persists only the actions configured on its published node. Process lifecycle status remains controlled by `ProcessStateMachine`; graph navigation is owned by `DynamicWorkflowEngine`.

`Escalate` is an explicit task action, not a timer side effect. `SlaDurationMinutes` is converted to nullable `ProcessTask.DueAt` when a task is created. The current runtime uses it for display and ordering; automatic reminders, reassignment and SLA escalation remain deferred.

List visibility is independent from graph execution. `personal` means starter/direct assignment/claim/live team-role candidacy, plus the current community task set for `Tasks.ManageAll`; `community` requires `Processes.ViewAll`, and `global` requires SuperAdmin. All visibility, `CanAct`/`CanClaim`, claim and action decisions share `TaskAccessPolicy`, and live permissions/team membership are resolved again on every protected request.

Technical node keys are stable runtime identifiers and may appear in graph JSON or process variables, but normal process/task cards present the node title and assignment context. Team and community role are shown together; a claim owner is additional context and never replaces the original assignment target.

## HTTP Contract

- `GET/POST /api/process-definitions`: scoped workflow identities.
- `GET /api/process-definitions/runnable`: latest published versions available to `Processes.Start` users.
- `POST /api/process-definitions/{id}/versions`: create draft graph version.
- `PUT /api/process-definitions/{id}/versions/{versionId}`: update a draft or clone a published version.
- `POST /api/process-definitions/{id}/validate`: validate without publishing.
- `POST /api/process-definitions/{id}/versions/{versionId}/publish`: make a validated version immutable.
- `POST /api/processes/start/version`: start from an exact process-definition version.
- `GET /api/processes`: server-paged and scope-aware process summaries with deadline/priority sorting.
- `GET /api/tasks/my`: server-paged lightweight task summaries with priority/deadline sorting and exact task deep-link filtering.
- `GET /api/tasks/{id}`: the authorized selected task, including its immutable published task-form schema.
- `POST /api/tasks/{id}/claim` and `POST|DELETE /api/tasks/{id}/release|claim`: candidate-pool ownership.
- `POST /api/tasks/{id}/actions`: submit action, note and optional task-form data.

Designer import/export is a frontend draft transport contract rather than a
backup API. The version-1 `techyouth.workflow-draft` envelope preserves node and
edge identifiers/geometry, strips deployment-specific bindings and marks each
cleared property in `requiresBinding`. Import creates a new local draft identity;
publish validation remains the authority for missing bindings.

Process detail returns the active node, pinned version IDs, task attempts and `ProcessStepExecution` history including node/assignment snapshots, completing actor, action, note and output JSON.

## Compatibility And Ownership

The legacy form-id start contract remains available through a seeded `Legacy Basic Approval` definition for compatibility. New development uses process-definition-version identifiers. The seeded `Transfer Talep Akisi` exercises the dynamic path through Scout, Technical Review, conditional Finance and Transfer Operation stages.

- Ufuk owns team contracts, permission scope, navigation, notifications and dashboard integration.
- Ozgun owns form versions, multi-page designer/runner and task-form field binding.
- Cagdas owns graph validation, visual modeler, runtime, claim/action, priority and process history.

The integration branch is `feature/dynamic-workflow`. Cross-owner changes stay here until unit, HTTP integration, frontend build and provider migration checks pass; commits are then split by capability/owner before merge.
