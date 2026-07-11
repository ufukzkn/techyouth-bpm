import type { UserStatus } from "@/lib/types";

export type AuditCategory = "all" | "identity" | "access" | "forms" | "processes" | "tasks";

export type PendingAccessChange = {
  userId: string;
  displayName: string;
  username: string;
  fromStatus: UserStatus;
  toStatus: UserStatus;
  fromCommunityRoleName?: string;
  toCommunityRoleName?: string;
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
  status: UserStatus;
  communityId?: string | null;
  communityRoleId?: string | null;
};

export const auditCategories: AuditCategory[] = ["all", "identity", "access", "forms", "processes", "tasks"];
