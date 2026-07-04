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

The script then starts the API. Keep that terminal open while using the web app. Stop it with `Ctrl + C`.

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
- `FormDefinitions`: saved dynamic form definitions.
- `FormFieldDefinitions`: fields belonging to a form definition.
- `FieldValidationRules`: dependent validation rules such as required-when.
- `ProcessInstances`: started BPM process records.
- `ProcessTasks`: assigned approve/reject work items.
- `AuditLogs`: traceable process state changes.
- `SystemAuditLogs`: critical identity, access, form, process and task actions for Admin review.

SQLite stores `Guid` values as lowercase text through an EF Core value converter. Keep this converter in mind when adding new `Guid` properties; it prevents casing mismatches during update/delete statements in local SQLite demos.

## Seed Data

`DatabaseSeeder` creates the demo users on startup if they do not already exist:

| Username | Password | Role | Status | Email verified |
| --- | --- | --- | --- | --- |
| `admin` | `admin123` | Admin | Active | true |
| `user` | `user123` | User | Active | true |
| `approver` | `approver123` | Approver | Active | true |
| `mario.gomez` | `mario123` | User | PendingApproval | false |
| `quaresma` | `trivela123` | Approver | Active | true |
| `atiba` | `atiba123` | User | Active | true |
| `alex` | `alex123` | User | Rejected | true |
| `fatih.terim` | `imparator123` | Admin | PendingApproval | false |

Passwords are stored as PBKDF2 hashes, not plain text. Existing local SQLite files from the earlier plaintext phase are upgraded on API startup by hashing any user password that is not already in the `pbkdf2:v1` format.

Session tokens are stored as SHA-256 hashes. Active sessions can be revoked through logout, the settings screen or `DELETE /api/auth/sessions/{sessionId}`.

The current local setup uses `EnsureCreated`, not migrations. When entity columns change, existing SQLite files may not get the new columns automatically. After identity/schema changes, reset local SQLite before testing:

```powershell
./scripts/run-api-local.ps1 -ResetDb -Force
```

When mock data is enabled, the seeder also creates:

- `Transfer Talep Formu`
- `Kamp Hazirlik Onay Formu`
- 8 demo process instances.
- 4 open approver tasks.
- completed/rejected examples with audit logs.
- system audit examples for registration, login, role/status updates, form updates, process start and task approval.

The seeded form/process data uses deterministic IDs and is idempotent, so restarting the API does not duplicate records. Resetting the SQLite database with `-ResetDb -Force` recreates the full demo scenario.

Mock user, process and log names intentionally use familiar football figures such as Mario Gomez, Ricardo Quaresma, Atiba Hutchinson, Alex de Souza, Ali Koc, Fatih Terim and Senol Gunes. These records are only local demo data for making the BPM flow easier to inspect during presentation rehearsal.

## Maintenance Rule

Whenever the team adds a new entity, table, seed value, migration strategy or demo data rule:

- Update this file.
- Update `docs/04-api-and-services.md` if an endpoint/service behavior changes.
- Update `scripts/run-api-local.ps1` if local startup/reset behavior changes.
- Keep real database files, credentials and connection strings out of git.

For now the API uses `EnsureCreated` for fast demo setup. If the project moves to formal EF Core migrations, document the migration commands here and update the helper script accordingly.
