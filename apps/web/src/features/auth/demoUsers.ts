import type { LoginResponse, Role } from "@/lib/types";

export const demoUsers: Array<{ username: string; password: string; displayName: string; role: Role }> = [
  { username: "admin", password: "admin123", displayName: "Admin User", role: "Admin" },
  { username: "user", password: "user123", displayName: "Process Starter", role: "User" },
  { username: "approver", password: "approver123", displayName: "Process Approver", role: "Approver" },
];

export function loginWithDemoUser(username: string, password: string): LoginResponse | null {
  const user = demoUsers.find((item) => item.username === username && item.password === password);

  if (!user) {
    return null;
  }

  return {
    token: `demo-${user.username}`,
    expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
    user: {
      id: user.username,
      username: user.username,
      displayName: user.displayName,
      role: user.role,
    },
  };
}
