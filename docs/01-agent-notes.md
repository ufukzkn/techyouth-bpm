# Agent Notes

This file contains only stable working rules. The complete development journal
is preserved in [Implementation History](history/implementation-log.md), and the
subject ownership map is in the [Documentation Guide](README.md).

## Fixed Decisions

- Scope is full-stack; the repository is a Next.js and .NET monorepo.
- Frontend uses Next.js App Router, TypeScript and focused Zustand stores.
- Backend uses .NET 8, EF Core migrations and selectable SQLite/PostgreSQL.
- SQLite is the default local demo; PostgreSQL/Neon is the shared-provider path.
- Browser authentication uses opaque HttpOnly cookie sessions plus CSRF;
  Swagger and explicit API clients retain Bearer support.
- Team ownership is flow-based rather than frontend/backend-layer based.
- Documentation changes accompany contract, schema, startup and UX-standard
  changes.

## Architecture Guardrails

- Domain remains independent. Application depends on Domain, and Infrastructure
  implements Application contracts. API composes the system.
- Controllers translate HTTP and delegate; business policy belongs in services.
- EF Core `DbContext/DbSet` provides repository and unit-of-work behavior. Do not
  add a generic repository without a concrete query or ownership benefit.
- Frontend pages compose feature modules; API calls stay in `src/lib/api.ts` or
  focused feature data hooks, not scattered through presentational components.
- Zustand is for cross-route state such as session, notification cache and
  workflow draft. Keep page-local selection and disclosure state local.
- Process lifecycle rules, graph execution, candidate resolution and visibility
  remain separate services so one concern can change without rewriting others.
- Keep `Community`, `CommunityRole` and `Team` distinct: security boundary,
  permission bundle and operational group.
- `Takimsiz` is a virtual query result, never a seeded team. `IsLead` is context
  and never bypasses permissions.
- Published form/workflow versions are immutable; running instances stay pinned
  to their starting versions.

## Ownership

- Ufuk: access, session, shell, dashboard, management, teams, audit and
  notification integration.
- Ozgun: form versioning, designer/runner, validation and task-form binding.
- Cagdas: workflow graph, validator/runtime, process/task actions and history.
- Shared contracts are agreed in documents 18 and 20 before cross-owner code is
  merged.

## UX And Data Rules

- Use shared skeleton, inline loader, action feedback, confirmation, disclosure
  and pagination components described in document 19.
- Show first-load skeletons only without cached content. Preserve cached content
  during refresh and use a compact spinner.
- Keep mutation feedback in the card that initiated it; use global toasts for
  cross-screen or background events.
- Search and paginate potentially unbounded users, logs, notifications,
  processes and tasks on the server.
- Cache counts and paged reads by user plus effective scope; invalidate only the
  affected keys after mutations or explicit refresh.
- Every destructive or privilege-changing action requires explicit confirmation
  and backend authorization.
- Permanent community deletion is a separate SuperAdmin-only operation, never a
  stronger spelling of deactivation. It requires an inactive community, an
  impact preview, exact-name confirmation, current password and a reason.
- Community purge and its safe audit archive must commit in one transaction.
  Archive snapshots may retain actor/action/team/role/timestamp context, but
  never e-mail, IP, user-agent, form payload, task note or raw metadata.
- Treat candidate-task `CanClaim` and post-claim `CanAct` as separate states.
  Never infer a team-lead denial solely from `CanAct=false` before claim.
- Keep technical workflow node keys out of normal cards. Show the node title,
  assignment team/role/lead restriction and claim owner as separate context.

## Security And Configuration

- Never track database passwords, SMTP credentials, tokens or private `.env`
  files. Commit only safe examples.
- Passwords use PBKDF2; session and refresh tokens are stored as hashes.
- Resolve current user, status, permissions, community and team membership on
  the server; never trust navigation visibility as authorization. The resolved
  DTO may use the short-lived session cache, but logout/access/community/team
  mutations must invalidate the affected token, user or community immediately.
- Keep `ISessionValidationCache` provider-neutral. `IMemoryCache` is the
  single-instance implementation; a distributed deployment may replace it with
  Redis without changing auth services. `Auth:SessionCacheSeconds=0` disables
  caching for strict integration tests.
- Apply migrations before deterministic/idempotent seed data.
- New notifications must carry `CommunityId` when an event has a community
  scope. This lets lifecycle operations remove only the intended workspace data.

## Verification And Documentation

- Run the quality gates in [Test Strategy](24-testing-and-quality-gates.md).
- Update [API contracts](04-api-and-services.md) for HTTP/DTO changes.
- Update [Database](08-local-database.md) for migrations, providers or seed.
- Update [UI/UX system](19-ui-ux-system.md) for reusable interaction rules.
- Update [PDF matrix](00-requirements-from-pdf.md) only when requirement status
  changes; do not copy that matrix into feature documents.
- Keep review documents evidence-based and record remaining risks honestly.

## Demo Accounts

- `admin` / `admin123`: SuperAdmin
- `fatih.terim` / `imparator123`: Sportif Faaliyetler community admin
- `alex` / `alex123`: Urun Siparisi community admin
- `user` / `user123`: process starter
- `approver` / `approver123`: approval specialist
- `senol.gunes` / `senol123`: Insan Kaynaklari community admin
- `ali.koc` / `ali123`: Satin Alma community admin
