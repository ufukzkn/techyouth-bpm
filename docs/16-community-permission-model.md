# Community And Permission Model

## Purpose

The first authorization model used fixed roles: `Admin`, `User` and `Approver`. That was enough for the first BPM demo, but it did not model real teams well. The current model replaces those day-to-day roles with communities, custom community roles and operation-based permissions. `SuperAdmin` is the only platform-level privilege; every other account is a standard platform user whose effective access comes from its active community membership.

## Core Concepts

- `SuperAdmin`: platform-level administrator. Can see and manage every community, user, role, form, process and audit record. It cannot be moved to pending/rejected status and cannot delete itself.
- `SuperAdmin` does not need a community membership. Existing users cannot be promoted to `SuperAdmin` from the UI/API; a `SuperAdmin` can only create a brand-new `SuperAdmin` account.
- A normal account is created by first selecting its target community and then a role from that community. `Topluluk Admin` is therefore a community role, not a separate platform `Admin` value.
- `Community`: a business group such as `Sportif Faaliyetler`, `Lojistik`, `Urun Siparisi`, `Insan Kaynaklari` or `Satin Alma`.
- `Community.InviteCode`: active 5-character registration code. New self-registering users must provide the code to enter that community as `PendingApproval`.
- `CommunityRole`: a custom role inside one community. Examples: `Topluluk Admin`, `Form Tasarimcisi`, `Surec Baslatici`, `Onay Sorumlusu`.
- `CommunityRolePermission`: operation-level rights attached to a role.
- `UserCommunityMembership`: connects a user to a community and a community role. V1 uses one active community per user, but the model can support multiple memberships later.

## Permission Set

The first permission set is operation-based:

- `Community.ManageUsers`
- `Community.ManageRoles`
- `Community.ManageAdmins`
- `Forms.View`
- `Forms.Create`
- `Forms.Update`
- `Processes.View`
- `Processes.Start`
- `Tasks.View`
- `Tasks.Act`
- `Audit.View`

The frontend receives the active user's community and permissions from `UserDto`. Navigation is filtered by `permissions`, not only by enum role. Direct API calls are still protected by backend service checks.

## Scope Rules

- `SuperAdmin` can see all communities.
- `SuperAdmin` can reset another user's password by generating and emailing a temporary password; community admins cannot reset passwords.
- A community admin manages only their own community.
- A community admin only sees pending registrations, users, sessions, roles and audit records inside their own community.
- Form definitions belong to a community.
- Process instances belong to the form's community.
- Task visibility and task action checks use community scope plus `Tasks.View` / `Tasks.Act`.
- Audit search is scoped by community for non-SuperAdmin users.
- SuperAdmin sees an explicit `Tum topluluklar` user-search scope. Community Admin never gets a cross-community selector; it can only filter users by roles inside its own community.
- Community management is separate from user management. It owns community metadata, invite code, active status, custom roles and role-distribution counts.
- A custom role can be updated or removed. Role removal requires a replacement role and moves active memberships in one transaction. System roles, notably `Atanmadi`, are protected.
- A deactivated community revokes member sessions and blocks member login plus new workflow writes. SuperAdmin retains management access to reactivate it; historical data remains available.

## Demo Templates

The deterministic demo seed contains five communities. The first three cover sport, logistics and product flows; `Insan Kaynaklari` includes an leave/remote-work form and `Satin Alma` includes a budgeted purchase form. Both also have active users, roles, process history and notification examples so scope rules can be demonstrated without manual setup.

Seed data creates these templates:

- `Topluluk Admin`: full community permissions.
- `Atanmadi`: blank role used for pending or newly created users before a real custom role is selected.
- `Ozel`: blank template for creating a custom role from zero permissions.
- `Form Tasarimcisi`: form view/create/update.
- `Surec Baslatici`: form view plus process start/view.
- `Onay Sorumlusu`: task view/act plus process view.
- `Salt Okuyucu`: limited view permissions.

The current UI labels this role as `Gozlemci`. It keeps the existing read-only permission set and is not a second duplicate role.

`Standart Kullanici` is a separate template with `Forms.View`, `Processes.View`, `Processes.Start` and `Tasks.View`. It can start and monitor work but cannot approve/reject tasks or manage the community.

`Lojistik Gorevlisi` is not a default system template because its first permission set duplicated `Onay Sorumlusu`. A logistics community can still create that name as a custom role and then tailor its permissions.

Ready templates are directly usable roles, not automatic duplicates. Selecting `Surec Baslatici` shows that exact name and its fixed permission set. The administrator must explicitly choose `Bu sablonu ozellestir` before a mutable copy such as `Surec Baslatici*` can be created; this prevents accidental duplicate roles and makes the customization intent visible in the name.

## Presentation Defense

This is more extensible than hardcoded role checks because a new community role can be created by selecting permissions instead of changing code. The platform role is reserved for `SuperAdmin`; day-to-day BPM access lives in data. Seed startup also normalizes legacy `Admin` and `Approver` records to standard users while preserving their community membership and permissions.

`ProcessTask.AssignedRole` remains only as a non-functional database compatibility column for existing SQLite/PostgreSQL installations. It is not returned by the task API and is not consulted by authorization; `AssignedCommunityRoleId` and `RequiredPermission` are the authoritative task-assignment fields.

The registration code is intentionally simple in v1. It blocks random public registration attempts without adding a full invitation lifecycle. If the product needed expiry, single-use codes or bulk invitations later, those can become a separate `CommunityInvite` table without changing the core membership model.

## Lifecycle Safety

SuperAdmin can edit every community setting. A Topluluk Admin can toggle only its own community's active status through a confirmation step; name, description and invite code remain platform-managed. Deactivation revokes normal member sessions and blocks their new workflow writes. The acting Topluluk Admin retains only its scoped management session so it can reactivate the community.

## Test Guarantees

Service tests protect the important boundaries: registration assigns the supplied community's `Atanmadi` role, invalid invite codes persist nothing, existing users cannot become `SuperAdmin`, and only a current SuperAdmin can create a new SuperAdmin account. Community admins cannot read roles or move memberships across another community. Custom-role deletion moves active memberships to its replacement role. Notification reads are user-scoped, and community-scoped audit/task actions are blocked when access or the community is not valid.
