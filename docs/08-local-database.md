# Local Database

## Current Decision

The default development and demo database is SQLite. A shared Neon PostgreSQL database can also be used when the team wants to test common data, shared sessions and audit history across different machines.

SQLite is enough for fast local demo work because the PDF allows SQLite, PostgreSQL or MSSQL and the app already uses EF Core behind the Infrastructure layer. Neon is useful for team-level remote testing.

## Local Demo Database File

When the API starts with the default config, EF Core creates this ignored local file:

```text
apps/api/src/TechYouthBpm.Api/techyouth-bpm.db
```

This file is not committed. Each teammate can create their own local copy by starting the API.

## Create Or Reset Local Demo DB

Recommended helper script from the repo root:

```powershell
./scripts/run-api-local.ps1
```

The local script uses a 120-minute normal session by default. To test timeout UX quickly:

```powershell
./scripts/run-api-local.ps1 -SessionDurationMinutes 1
```

To reset the local SQLite demo database and start from seed data:

```powershell
./scripts/run-api-local.ps1 -ResetDb
```

For non-interactive reset:

```powershell
./scripts/run-api-local.ps1 -ResetDb -Force
```

Mock workflow data is enabled by default. To start only with demo users and without the seeded forms/processes:

```powershell
./scripts/run-api-local.ps1 -ResetDb -Force -SkipMockData
```

The script sets:

```text
ASPNETCORE_ENVIRONMENT=Development
Database__Provider=Sqlite
Auth__SessionDurationMinutes=120
Auth__RememberMeDurationMinutes=43200
Auth__MaxFailedLoginAttempts=5
Auth__LockoutMinutes=10
Auth__RateLimitPermitLimit=10
Auth__RateLimitWindowMinutes=1
Seed__MockData=true
```

The script starts the API in the same terminal. Keep that terminal open while using the web app and stop the API with `Ctrl+C` in that terminal.

## Shared Neon PostgreSQL

Create a gitignored `.env.neon.local` file in the repo root:

```text
Database__Provider=PostgreSql
ConnectionStrings__DefaultConnection=Host=your-neon-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true;Channel Binding=Require
Seed__MockData=true
```

Start the API against Neon:

```powershell
./scripts/run-api-neon.ps1 -Url http://localhost:5292
```

If another API process is already using the compiled backend files, start Neon with the existing build:

```powershell
./scripts/run-api-neon.ps1 -Url http://localhost:5292 -NoBuild
```

The Neon environment file is ignored by git. Do not commit real connection strings or passwords.

The web app can be started from the repo root with:

```powershell
./scripts/run-web-local.ps1
```

Use these scripts instead of long inline `Start-Process -Command` strings. This avoids PowerShell quoting issues around window titles and paths.

## Current Schema Overview

The schema is created from EF Core entities in `TechYouthBpm.Domain` through `AppDbContext`.

Current tables:

- `Users`: demo users, emails, roles, approval status, email verification state, failed login counters, lockout timestamps and PBKDF2 password hashes.
- `UserSessions`: session ids, hashed opaque bearer session tokens, expiry times, last-seen timestamps and revoke timestamps.
- `RefreshTokens`: hashed rotating remember-me tokens tied to access sessions and devices.
- `Communities`: BPM working groups such as Sportif Faaliyetler, Lojistik and Urun Siparisi.
- `Notifications`: DB-backed user notifications for task/access/process events and read state.
- `CommunityRoles`: custom roles inside one community.
- `CommunityRolePermissions`: operation permissions attached to community roles.
- `UserCommunityMemberships`: active user/community/role assignments.
- `Teams`: community-scoped operational groups with normalized names, lifecycle state and creator metadata.
- `TeamMemberships`: multi-team user membership, active state and informational lead state.
- `FormDefinitions`: saved dynamic form definitions.
- `FormFieldDefinitions`: fields belonging to a form definition.
- `FieldValidationRules`: dependent validation rules such as required-when.
- `FormDefinitionVersions`: immutable published or editable draft form schemas.
- `FormPageDefinitions`, `FormVersionFieldDefinitions`, `FormVersionFieldValidationRules`: ordered multi-page version content.
- `ProcessDefinitions`, `ProcessDefinitionVersions`: logical workflows and pinned typed JSON graph versions.
- `ProcessInstances`: started BPM process records.
- `ProcessTasks`: direct or candidate-pool work items, priority, nullable SLA deadline, claim state, node key and optional task form.
- `ProcessStepExecutions`: immutable node attempt, actor, action, timing and output history.
- `AuditLogs`: traceable process state changes.
- `SystemAuditLogs`: critical identity, access, form, process and task actions for Admin review.

SQLite stores `Guid` values as lowercase text through an EF Core value converter. Keep this converter in mind when adding new `Guid` properties; it prevents casing mismatches during update/delete statements in local SQLite demos.

## Seed Data

`DatabaseSeeder` creates the demo users on startup if they do not already exist. The same deterministic seeder is used by local SQLite, Neon and both Docker stacks:

| Username | Password | Platform role | Community role | Status | Email verified |
| --- | --- | --- | --- | --- | --- |
| `admin` | `admin123` | SuperAdmin | Global | Active | true |
| `user` | `user123` | User | Surec Baslatici | Active | true |
| `approver` | `approver123` | User | Onay Sorumlusu | Active | true |
| `mario.gomez` | `mario123` | User | Atanmadi | PendingApproval | false |
| `quaresma` | `trivela123` | User | Onay Sorumlusu | Active | true |
| `atiba` | `atiba123` | User | Onay Sorumlusu | Active | true |
| `alex` | `alex123` | User | Topluluk Admin | Active | true |
| `fatih.terim` | `imparator123` | User | Topluluk Admin | Active | true |
| `sergen.yalcin` | `sergen123` | User | Onay Sorumlusu | Active | true |
| `tuncay.sanli` | `tuncay123` | User | Topluluk Admin | Active | true |
| `volkan.demirel` | `volkan123` | User | Onay Sorumlusu | Rejected | true |

Passwords are stored as PBKDF2 hashes, not plain text. Existing local SQLite files from the earlier plaintext phase are upgraded on API startup by hashing any user password that is not already in the `pbkdf2:v1` format.

The same seed creates 16 operational teams across the five communities. Sportif Faaliyetler contains Scout, Technical, Finance and Transfer teams; the other communities contain three teams each. Seeded users include team leads, multi-team members and users with no active team. `Takimsiz` is calculated from those users at query time and is never inserted as a database row.

Session tokens are stored as SHA-256 hashes. Active sessions include created time, last seen time, IP address, user agent and remembered-device state, then can be revoked through logout, the settings screen or `DELETE /api/auth/sessions/{sessionId}`. Remember-me sessions also create hashed rows in `RefreshTokens`; refresh tokens are rotated when used and revoked together with their access session.

The current setup uses formal EF Core migrations. API startup calls `Database.MigrateAsync()` first, then `DatabaseSeeder` adds deterministic demo users, forms, processes, tasks and audit rows. `DatabaseSeeder` should not create or patch tables anymore; schema changes belong in migrations under `apps/api/src/TechYouthBpm.Infrastructure/Data/Migrations`.

Seed upgrades resolve system roles by community and template instead of assuming that every older database used the current deterministic role IDs. This keeps existing users and custom records intact while allowing the current workflow scenarios to reference valid roles.

Local demo still defaults to SQLite. PostgreSQL/Neon uses the same EF model and migration files, so provider-specific changes should be tested against PostgreSQL before the final shared demo. Existing databases created during the earlier `EnsureCreated` phase are disposable; reset/recreate them before relying on migrations:

```powershell
./scripts/run-api-local.ps1 -ResetDb -Force
```

Migration commands:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project apps/api/src/TechYouthBpm.Infrastructure/TechYouthBpm.Infrastructure.csproj --startup-project apps/api/src/TechYouthBpm.Api/TechYouthBpm.Api.csproj
```

When adding a schema change:

```powershell
$env:Database__Provider = "PostgreSql"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=techyouth_bpm;Username=postgres;Password=postgres"
dotnet tool run dotnet-ef migrations add DescriptiveMigrationName --project apps/api/src/TechYouthBpm.Infrastructure/TechYouthBpm.Infrastructure.csproj --startup-project apps/api/src/TechYouthBpm.Api/TechYouthBpm.Api.csproj --output-dir Data/Migrations
```

When mock data is enabled, the seeder also creates:

- Five demo communities: `Sportif Faaliyetler`, `Lojistik`, `Urun Siparisi`, `Insan Kaynaklari` and `Satin Alma`.
- Registration codes: `SPOR1`, `LOG01`, `URUN1`, `IK001` and `SAT01`.
- Eight active memberships in each community, distributed across community admin, form designer, process starter, approver, standard user, observer and unassigned roles. Each community includes an unassigned pending user for the approval demo.
- Built-in community roles such as `Topluluk Admin`, `Form Tasarimcisi`, `Surec Baslatici`, `Onay Sorumlusu`, `Standart Kullanici` and `Gozlemci`.
- Blank `Atanmadi` roles for pending/new users.
- Start forms such as `Transfer Talep Formu`, `Kamp Hazirlik Onay Formu`, `Izin ve Uzaktan Calisma Talep Formu` and `Satin Alma Talep Formu`.
- Transfer task forms for Scout, Technical Review, Finance Approval and Transfer Operation.
- Published `Legacy Basic Approval` compatibility workflow.
- Published `Transfer Talep Akisi` with four team swimlanes, a typed budget gateway and version-pinned task forms.
- Published workflow demos for Sportif Faaliyetler, Lojistik, Urun Siparisi, Insan Kaynaklari and Satin Alma.
- Published `Transfer Aksiyon Laboratuvarı` and `Operasyon Aksiyon Laboratuvarı` definitions. Each has team-pool, team-plus-role, specific-user and team-lead-only assignments plus five deterministic instances covering Approve, Reject, Complete, SendBack and Escalate.
- Five graph-consistent scenarios per community: overdue review, upcoming approval, completed, rejected and sent-back/reopened.
- Open tasks with overdue, upcoming and future `DueAt` values plus version-pinned task forms.
- Namespaced start/task output, step execution, process audit, system audit and notification records that refer to the same workflow instance.
- system audit examples for registration, login, role/status updates, form updates, process start and task approval.

The seeded form/process data uses deterministic IDs and is idempotent, so restarting the API does not duplicate records. Only the fourteen historical fixed mock process IDs are retired; user-created processes, users, memberships and custom communities are preserved. Resetting the SQLite database with `-ResetDb -Force` recreates the full demo scenario.

Mock user, process and log names intentionally use familiar football figures such as Mario Gomez, Ricardo Quaresma, Atiba Hutchinson, Alex de Souza, Ali Koc, Fatih Terim, Senol Gunes, Sergen Yalcin, Tuncay Sanli and Volkan Demirel. These records are only local demo data for making the BPM flow easier to inspect during presentation rehearsal.

## Maintenance Rule

Whenever the team adds a new entity, table, seed value, migration strategy or demo data rule:

- Update this file.
- Update `docs/04-api-and-services.md` if an endpoint/service behavior changes.
- Add or update an EF Core migration for schema changes.
- Update `scripts/run-api-local.ps1` if local startup/reset behavior changes.
- Keep real database files, credentials and connection strings out of git.
