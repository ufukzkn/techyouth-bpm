// Platform seviyesinde yalnizca SuperAdmin ayricaliklidir. Diger tum erisim,
// kullanicinin aktif topluluk rolu ve onun izinlerinden gelir.
export type Role = "SuperAdmin" | "User";
export type UserStatus = "PendingApproval" | "Active" | "Rejected";

export type User = {
  id: string;
  username: string;
  displayName: string;
  email: string;
  role: Role;
  status: UserStatus;
  isEmailVerified: boolean;
  mustChangePassword: boolean;
  communityId?: string | null;
  communityName: string;
  communityRoleId?: string | null;
  communityRoleName: string;
  permissions: PermissionName[];
  isCommunityActive: boolean;
};

export type PermissionName =
  | "Community.ManageUsers"
  | "Community.ManageRoles"
  | "Community.ManageAdmins"
  | "Forms.View"
  | "Forms.Create"
  | "Forms.Update"
  | "Processes.View"
  | "Processes.Start"
  | "Tasks.View"
  | "Tasks.Act"
  | "Audit.View";

export type LoginResponse = {
  token: string;
  user: User;
  expiresAt: string;
  csrfToken: string;
};

export type RegisterResponse = {
  id: string;
  username: string;
  email: string;
  status: UserStatus;
};

export type UserAdmin = User & {
  failedLoginCount: number;
  lockedUntil?: string | null;
  createdAt: string;
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type UserSession = {
  id: string;
  createdAt: string;
  expiresAt: string;
  lastSeenAt?: string | null;
  isCurrent: boolean;
  ipAddress?: string | null;
  userAgent?: string | null;
  rememberedDevice: boolean;
};

export type UpdateProfileRequest = {
  displayName: string;
  email: string;
};

export type ChangePasswordRequest = {
  currentPassword: string;
  newPassword: string;
};

export type CreateUserAdminRequest = {
  username: string;
  displayName: string;
  email: string;
  role: Role;
  status: UserStatus;
  temporaryPassword: string;
  communityId?: string | null;
  communityRoleId?: string | null;
};

export type AdminPasswordResetRequest = {
  useManualPassword: boolean;
  temporaryPassword?: string | null;
};

export type AdminPasswordResetResponse = {
  message: string;
};

export type EmailVerificationStartResponse = {
  message: string;
  demoCode: string;
  expiresAt: string;
};

export type ForgotPasswordRequest = {
  usernameOrEmail: string;
};

export type ForgotPasswordResponse = {
  message: string;
  demoToken: string;
  expiresAt?: string | null;
};

export type ResetPasswordRequest = {
  usernameOrEmail: string;
  token: string;
  newPassword: string;
};

export type ThemeMode = "light" | "dark";
export type Language = "tr" | "en";

export type FieldType = "Text" | "TextArea" | "Number" | "Email" | "Select" | "Radio" | "Checkbox" | "Date";

export type ValidationRuleType = "RequiredWhen";

export type ValidationRule = {
  ruleType: ValidationRuleType;
  dependsOnFieldKey: string;
  expectedValue: string;
  message: string;
};

export type FormFieldDefinition = {
  id?: string;
  key: string;
  label: string;
  type: FieldType;
  required: boolean;
  sortOrder: number;
  options: string[];
  validationRules: ValidationRule[];
};

export type FormDefinition = {
  id: string;
  name: string;
  description: string;
  communityId: string;
  communityName: string;
  createdByUserId: string;
  createdAt: string;
  fields: FormFieldDefinition[];
};

export type CreateFormRequest = {
  name: string;
  description: string;
  fields: Omit<FormFieldDefinition, "id">[];
  communityId?: string | null;
};

export type ProcessStatus = "Pending" | "InProgress" | "Completed" | "Rejected" | "Escalated";
export type ProcessTaskStatus = "Open" | "Completed" | "Cancelled";
export type WorkflowAction = "Start" | "Approve" | "Reject" | "Escalate";

export type ProcessTask = {
  id: string;
  processInstanceId: string;
  assignedCommunityRoleId?: string | null;
  assignedCommunityRoleName: string;
  requiredPermission: PermissionName;
  status: ProcessTaskStatus;
  availableActions: WorkflowAction[];
  createdAt: string;
  completedAt?: string | null;
};

export type AuditLog = {
  id: string;
  action: WorkflowAction;
  fromStatus: ProcessStatus;
  toStatus: ProcessStatus;
  userId: string;
  userDisplayName: string;
  userUsername: string;
  createdAt: string;
  note: string;
};

export type SystemAuditLog = {
  id: string;
  actorUserId?: string | null;
  actorDisplayName: string;
  actorUsername: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  description: string;
  createdAt: string;
  entityDisplayName?: string | null;
  entityUsername?: string | null;
};

export type SystemAuditCategoryCounts = {
  all: number;
  identity: number;
  access: number;
  forms: number;
  processes: number;
  tasks: number;
};

export type ProcessSummary = {
  id: string;
  formDefinitionId: string;
  formName: string;
  communityId: string;
  communityName: string;
  status: ProcessStatus;
  startedAt: string;
  completedAt?: string | null;
};

export type ProcessDetail = ProcessSummary & {
  formData: Record<string, unknown>;
  tasks: ProcessTask[];
  auditLogs: AuditLog[];
};

export type StartProcessRequest = {
  formDefinitionId: string;
  formData: Record<string, unknown>;
};

export type TaskActionRequest = {
  action: WorkflowAction;
  note?: string;
};

export type DashboardSummary = {
  openTaskCount: number;
  inProgressProcessCount: number;
  completedProcessCount: number;
};

export type Community = {
  id: string;
  name: string;
  description: string;
  inviteCode: string;
  isActive: boolean;
  createdAt: string;
};

export type CommunityRole = {
  id: string;
  communityId: string;
  name: string;
  description: string;
  templateKey: string;
  isSystemRole: boolean;
  permissions: PermissionName[];
};

export type RoleTemplate = {
  key: string;
  name: string;
  description: string;
  permissions: PermissionName[];
};

export type CreateCommunityRequest = {
  name: string;
  description: string;
  inviteCode?: string;
  isActive: boolean;
};

export type UpdateCommunityRequest = CreateCommunityRequest;

export type CreateCommunityRoleRequest = {
  name: string;
  description: string;
  templateKey: string;
  permissions: PermissionName[];
};

export type CommunityRoleCount = {
  communityRoleId: string;
  communityRoleName: string;
  userCount: number;
};

export type CommunitySummary = {
  communityId: string;
  memberCount: number;
  roleCounts: CommunityRoleCount[];
};

export type NotificationItem = {
  id: string;
  type: string;
  title: string;
  message: string;
  entityType?: string | null;
  entityId?: string | null;
  createdAt: string;
  readAt?: string | null;
};
