export type Role = "Admin" | "User" | "Approver";
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
};

export type LoginResponse = {
  token: string;
  user: User;
  expiresAt: string;
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

export type UserSession = {
  id: string;
  createdAt: string;
  expiresAt: string;
  lastSeenAt?: string | null;
  isCurrent: boolean;
  ipAddress?: string | null;
  userAgent?: string | null;
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
};

export type EmailVerificationStartResponse = {
  message: string;
  demoCode: string;
  expiresAt: string;
};

export type ThemeMode = "light" | "dark";
export type Language = "tr" | "en";

export type FieldType = "Text" | "Number" | "Email" | "Select" | "Checkbox" | "Date";

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
  createdByUserId: string;
  createdAt: string;
  fields: FormFieldDefinition[];
};

export type CreateFormRequest = {
  name: string;
  description: string;
  fields: Omit<FormFieldDefinition, "id">[];
};

export type ProcessStatus = "Pending" | "InProgress" | "Completed" | "Rejected";
export type ProcessTaskStatus = "Open" | "Completed" | "Cancelled";
export type WorkflowAction = "Start" | "Approve" | "Reject";

export type ProcessTask = {
  id: string;
  processInstanceId: string;
  assignedRole: Role;
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
};

export type ProcessSummary = {
  id: string;
  formDefinitionId: string;
  formName: string;
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
