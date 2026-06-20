export type Role = "Admin" | "User" | "Approver";

export type User = {
  id: string;
  username: string;
  displayName: string;
  role: Role;
};

export type LoginResponse = {
  token: string;
  user: User;
  expiresAt: string;
};

export type ThemeMode = "light" | "dark";
