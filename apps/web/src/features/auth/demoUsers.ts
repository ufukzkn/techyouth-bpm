import type { LoginResponse, Role } from "@/lib/types";

const demoSessionDurationMinutes = 120;
const demoRememberMeDurationMinutes = 43200;

export const demoUsers: Array<{ username: string; password: string; displayName: string; role: Role }> = [
  { username: "admin", password: "admin123", displayName: "Admin User", role: "Admin" },
  { username: "user", password: "user123", displayName: "Process Starter", role: "User" },
  { username: "approver", password: "approver123", displayName: "Process Approver", role: "Approver" },
];

export function loginWithDemoUser(username: string, password: string, rememberMe = false): LoginResponse | null {
  const user = demoUsers.find((item) => item.username === username && item.password === password);

  if (!user) {
    return null;
  }

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
    },
  };
}
