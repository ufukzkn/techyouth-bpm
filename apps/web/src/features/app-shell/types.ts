import type { Role, UserStatus } from "@/lib/types";

export type AuditCategory = "all" | "identity" | "access" | "forms" | "processes" | "tasks";

export type PendingAccessChange = {
  userId: string;
  displayName: string;
  username: string;
  fromRole: Role;
  toRole: Role;
  fromStatus: UserStatus;
  toStatus: UserStatus;
};

export type PendingSessionRevoke = {
  userId: string;
  sessionId: string;
  displayName: string;
  username: string;
  expiresAt: string;
  isCurrent: boolean;
};

export type PendingUserDelete = {
  userId: string;
  displayName: string;
  username: string;
};

export type StatusTone = "success" | "error" | "info";
export type SettingsSectionId = "profile" | "password" | "sessions";
export type AuditHistoryMode = "related" | "actor" | "target";

export type SelectedAuditHistory = {
  logId: string;
  mode: AuditHistoryMode;
};

export type AccessDraft = {
  userId: string;
  role: Role;
  status: UserStatus;
};

export const auditCategories: AuditCategory[] = ["all", "identity", "access", "forms", "processes", "tasks"];
