import type { LoginResponse, Role } from "@/lib/types";

const demoSessionDurationMinutes = 120;
const demoRememberMeDurationMinutes = 43200;

export const demoUsers: Array<{
  username: string;
  password: string;
  displayName: string;
  role: Role;
  community?: "sport" | "product";
  communityRoleId?: string;
  communityRoleName?: string;
  permissions?: LoginResponse["user"]["permissions"];
}> = [
  { username: "admin", password: "admin123", displayName: "Platform SuperAdmin", role: "SuperAdmin" },
  { username: "fatih.terim", password: "imparator123", displayName: "Fatih Terim", role: "User", community: "sport", communityRoleId: "20202020-0000-0000-0000-000000000001", communityRoleName: "Topluluk Admin", permissions: ["Community.ManageUsers", "Community.ManageRoles", "Community.ManageAdmins", "Forms.View", "Forms.Create", "Forms.Update", "Processes.View", "Processes.Start", "Tasks.View", "Tasks.Act", "Audit.View"] },
  { username: "alex", password: "alex123", displayName: "Alex de Souza", role: "User", community: "product", communityRoleId: "20202020-0000-0000-0000-000000000006", communityRoleName: "Topluluk Admin", permissions: ["Community.ManageUsers", "Community.ManageRoles", "Community.ManageAdmins", "Forms.View", "Forms.Create", "Forms.Update", "Processes.View", "Processes.Start", "Tasks.View", "Tasks.Act", "Audit.View"] },
  { username: "user", password: "user123", displayName: "Process Starter", role: "User", community: "sport", communityRoleId: "20202020-0000-0000-0000-000000000002", communityRoleName: "Surec Baslatici", permissions: ["Forms.View", "Processes.View", "Processes.Start"] },
  { username: "approver", password: "approver123", displayName: "Task Reviewer", role: "User", community: "sport", communityRoleId: "20202020-0000-0000-0000-000000000003", communityRoleName: "Onay Sorumlusu", permissions: ["Processes.View", "Tasks.View", "Tasks.Act"] },
];

export function loginWithDemoUser(username: string, password: string, rememberMe = false): LoginResponse | null {
  const user = demoUsers.find((item) => item.username === username && item.password === password);

  if (!user) {
    return null;
  }

  const isSuperAdmin = user.role === "SuperAdmin";
  const isProductCommunity = user.community === "product";
  const communityId = isSuperAdmin
    ? null
    : isProductCommunity
      ? "10101010-0000-0000-0000-000000000003"
      : "10101010-0000-0000-0000-000000000001";
  const communityName = isSuperAdmin ? "" : isProductCommunity ? "Urun Siparisi" : "Sportif Faaliyetler";

  return {
    token: `demo-${user.username}`,
    csrfToken: "",
    expiresAt: new Date(
      Date.now() + (rememberMe ? demoRememberMeDurationMinutes : demoSessionDurationMinutes) * 60 * 1000,
    ).toISOString(),
    user: {
      id: user.username,
      username: user.username,
      displayName: user.displayName,
      email: `${user.username}@techyouth.local`,
      role: user.role,
      status: "Active",
      isEmailVerified: true,
      mustChangePassword: false,
      communityId,
      communityName,
      communityRoleId: isSuperAdmin ? null : user.communityRoleId ?? null,
      communityRoleName: isSuperAdmin ? "" : user.communityRoleName ?? "Atanmadi",
      permissions: user.role === "SuperAdmin"
        ? ["Community.ManageUsers", "Community.ManageRoles", "Community.ManageAdmins", "Forms.View", "Forms.Create", "Forms.Update", "Processes.View", "Processes.Start", "Tasks.View", "Tasks.Act", "Audit.View"]
        : user.permissions ?? [],
    },
  };
}
