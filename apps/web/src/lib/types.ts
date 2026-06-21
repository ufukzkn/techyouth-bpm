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
  userDisplayName: string;
  createdAt: string;
  note: string;
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
