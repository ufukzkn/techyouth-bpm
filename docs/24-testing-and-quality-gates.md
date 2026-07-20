# Testing And Quality Gates

## Purpose

This is the canonical source for automated-test scope, verification commands and
the latest verified baseline. Feature and review documents link here instead of
copying counts that become stale.

## Test Layers

| Layer | Evidence | What it protects |
| --- | --- | --- |
| Pure unit | State machine, graph adapter, validators and store helpers | Deterministic rules without HTTP or persistence noise |
| Relational service | SQLite-backed service tests | Foreign keys, unique indexes, LINQ behavior and transactions |
| HTTP integration | `WebApplicationFactory<Program>` | Routing, cookies, CSRF, Bearer, authorization and response contracts |
| Provider smoke | Opt-in PostgreSQL temporary schema | EF migrations and provider compatibility without touching demo data |
| Frontend unit | Vitest | Notification cache, scope isolation, form/version helpers and workflow draft behavior |
| Browser E2E | Playwright + isolated SQLite | Real cookie session, route guard, form/workflow publication and candidate claim |
| Manual acceptance | Cross-role workflow scenarios | Visual geometry, real-device touch and the complete presentation narrative |

## Backend Coverage Catalog

- `ProcessStateMachineTests`: allowed lifecycle transitions, available actions
  and invalid-transition rejection.
- `TaskAuthorizationTests`: assignee/approver/admin boundaries, closed tasks and
  missing tasks.
- `AuditLogTests` and `SystemAuditServiceTests`: actor, transition, note,
  category, community scope and admin visibility.
- `AuthServiceTests`: PBKDF2, hashed opaque sessions, registration, approval,
  lockout, remember-me, logout/revoke, refresh rotation and reuse handling.
- HTTP security tests: cookie bootstrap, `/api/auth/me`, CSRF rejection and
  success, Bearer compatibility, rate limiting and forced logout.
- Operational HTTP tests: liveness/readiness, migration/SuperAdmin invariant,
  RFC 7807 responses, validated correlation IDs, CORS and production HSTS.
- Form tests: create/update, ordered fields/pages, dependent validation,
  immutable published versions and task-form payload validation.
- `ProcessDefinitionServiceTests`: graph validation, geometry round-trip,
  namespace-safe conditions, same-community references, cycle rules and
  published-version immutability.
- `DynamicWorkflowRuntimeTests`: gateway routing, send-back attempts, candidate
  resolution, claim concurrency, task forms, SLA/deadline, team-lead-only tasks
  and rollback when a dependent write fails.
- `AuthorizationAndWorkflowIntegrationTests`: real controllers for workflow
  create, publish, runnable list, process start, task claim/action, notification
  and both audit layers.
- Visibility and paging tests: personal/community/global scope, permission
  rejection, projected summaries, total counts, priority/deadline sorting and
  user-scoped cache assumptions.
- Contract/performance regression: normalized OpenAPI path+verb snapshot,
  `MultipleCollectionIncludeWarning` as an error and bounded query counts over
  250 users, 500 audit entries and 180 process/task records.
- Seed/migration tests: deterministic showcase workflows, idempotency, preservation
  of user-created records and SQLite/PostgreSQL schema compatibility.

## Security Acceptance Matrix

- Browser login returns authentication through HttpOnly cookies without exposing
  access or refresh secrets to JavaScript persistence.
- Cookie mutations require a matching `X-CSRF-Token`; Bearer requests do not.
- Invalid, expired or revoked sessions return `401` and cannot be refreshed
  outside their valid refresh chain.
- Permission, community and team changes affect an already-issued opaque session
  on the next protected request.
- Community Admin operations cannot cross community boundaries; SuperAdmin-only
  operations are enforced in backend services.
- Password reset and all-device logout revoke access and refresh sessions.
- Critical form/process/task state plus notification and audit writes are atomic.

## Frontend Coverage

- Notification cache uses user-scoped keys, stale-while-revalidate behavior,
  optimistic read-state updates and rollback.
- Process/dashboard cache keys include user and effective visibility scope.
- Form tests protect page/field ordering, validation and version selection.
- Workflow tests protect graph conversion, draft state, SLA representation and
  publish-validation helpers.
- Production build verifies App Router boundaries and route-level imports.
- Playwright runs four real-server scenarios for session reload/logout, direct
  route authorization, form/workflow publication/start and team-role claim.
- Responsive geometry, drag/drop precision, focus, full-screen canvas and
  real-device touch still require manual acceptance.

The canonical manual chain is [Workflow End-to-End Test Scenarios](22-workflow-end-to-end-test-scenarios.md).

## Commands

```powershell
dotnet test apps/api/TechYouthBpm.slnx
Set-Location apps/web
npm run test
npm run lint
npm run build
npm run test:e2e
```

Optional PostgreSQL smoke:

```powershell
$env:TECHYOUTH_TEST_POSTGRES_CONNECTION = "<temporary PostgreSQL connection>"
dotnet test apps/api/TechYouthBpm.slnx
Remove-Item Env:TECHYOUTH_TEST_POSTGRES_CONNECTION
```

Docker and direct startup commands are in [QUICKSTART.md](../QUICKSTART.md).

## Latest Verified Baseline

On 20 July 2026:

- Backend: **208/208** tests passed.
- Frontend: **51/51** tests passed.
- Playwright: **4/4** real-server scenarios passed.
- Frontend production build passed.
- ESLint passed without errors or warnings.
- Local/cloud Compose configuration, API/web image builds and PostgreSQL
  migration smoke are part of the full GitHub Actions gate.
- The locally built images were also inspected as non-root: API runs as `app`,
  web runs as `nextjs`.

Run the commands again before presenting; this snapshot is evidence, not a
substitute for current verification.

## GitHub Actions Policy

- Every push runs .NET tests, frontend tests/lint/build and whitespace checks.
- `master` pushes and `workflow_dispatch` additionally run Playwright,
  PostgreSQL migration smoke and Docker Compose/image checks.
- A newer push to the same branch cancels the obsolete run.
- CI validates only; it does not deploy and does not require a pull request.
- Failed Playwright runs upload screenshot, trace, video and HTML report
  diagnostics for seven days.

## Remaining Quality Work

- Run accessibility automation plus keyboard and real-device touch checks.
- Add true load/soak tests if production traffic targets are defined; current
  large-fixture tests guard pagination/query regressions, not throughput.
- Connect health/log/error signals to the selected hosting platform and define
  alert ownership.
- Expand browser coverage when new workflow nodes or community mutations are
  introduced.
