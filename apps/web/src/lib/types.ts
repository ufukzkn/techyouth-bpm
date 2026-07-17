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
  teams?: UserTeam[];
};

export type UserTeam = {
  id: string;
  name: string;
  isLead: boolean;
};

export type PermissionName =
  | "Community.ManageUsers"
  | "Community.ManageRoles"
  | "Community.ManageAdmins"
  | "Teams.View"
  | "Teams.Manage"
  | "Forms.View"
  | "Forms.Create"
  | "Forms.Update"
  | "Workflows.View"
  | "Workflows.Create"
  | "Workflows.Update"
  | "Workflows.Publish"
  | "Processes.View"
  | "Processes.ViewAll"
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

export type FieldType = "Text" | "TextArea" | "Number" | "Email" | "Select" | "Radio" | "Checkbox" | "Date" | "FileUpload";

export type FileUploadMetadata = {
  name: string;
  size: number;
  type: string;
  lastModified: number;
};

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
  latestPublishedVersionId?: string | null;
  latestPublishedVersionNumber?: number | null;
};

export type CreateFormRequest = {
  name: string;
  description: string;
  fields: Omit<FormFieldDefinition, "id">[];
  communityId?: string | null;
  createPublishedVersion?: boolean;
};

export type DefinitionVersionStatus = "Draft" | "Published" | "Archived";

export type CreateFormPageRequest = {
  key: string;
  title: string;
  description: string;
  sortOrder: number;
  fields: Omit<FormFieldDefinition, "id">[];
};

export type CreateFormVersionRequest = {
  pages: CreateFormPageRequest[];
};

export type FormPage = {
  id: string;
  key: string;
  title: string;
  description: string;
  sortOrder: number;
  fields: FormFieldDefinition[];
};

export type FormDefinitionVersion = {
  id: string;
  formDefinitionId: string;
  formName: string;
  versionNumber: number;
  status: DefinitionVersionStatus;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
  pages: FormPage[];
};

export type ProcessStatus = "Pending" | "InProgress" | "Completed" | "Rejected" | "Escalated";
export type ProcessTaskStatus = "Open" | "Completed" | "Cancelled" | "Claimed";
export type WorkflowAction = "Start" | "Approve" | "Reject" | "Escalate" | "SendBack" | "Complete";
export type TaskPriority = "Low" | "Normal" | "High" | "Critical";
export type TaskAssignmentType = "ProcessStarter" | "SpecificUser" | "Team" | "CommunityRole" | "TeamAndCommunityRole";
export type ProcessNodeType = "Start" | "UserTask" | "ExclusiveGateway" | "CompletedEnd" | "RejectedEnd" | "TeamSwimlane";
export type GraphConditionOperator = "Equals" | "NotEquals" | "GreaterThan" | "GreaterThanOrEquals" | "LessThan" | "LessThanOrEquals" | "Contains" | "IsEmpty" | "IsNotEmpty";
export type ProcessStepStatus = "Active" | "Completed" | "Cancelled";

export type TaskAssignment = {
  type: TaskAssignmentType;
  userId?: string | null;
  teamId?: string | null;
  communityRoleId?: string | null;
};

export type ProcessNode = {
  key: string;
  type: ProcessNodeType;
  title: string;
  description?: string | null;
  formDefinitionVersionId?: string | null;
  priority: TaskPriority;
  slaDurationMinutes?: number | null;
  actions?: WorkflowAction[] | null;
  assignment?: TaskAssignment | null;
  parentKey?: string | null;
  teamId?: string | null;
  positionX: number;
  positionY: number;
  width?: number | null;
  height?: number | null;
};

export type ProcessCondition = {
  path: string;
  operator: GraphConditionOperator;
  value?: unknown;
};

export type ProcessEdge = {
  source: string;
  target: string;
  action?: WorkflowAction | null;
  condition?: ProcessCondition | null;
  isDefault: boolean;
  order: number;
  label?: string | null;
};

export type ProcessGraph = {
  schemaVersion: string;
  nodes: ProcessNode[];
  edges: ProcessEdge[];
};

export type ProcessDefinitionSummary = {
  id: string;
  name: string;
  description: string;
  communityId: string;
  communityName: string;
  latestVersionNumber?: number | null;
  latestPublishedVersionId?: string | null;
  latestPublishedFormDefinitionVersionId?: string | null;
  createdAt: string;
};

export type RunnableProcessDefinition = {
  id: string;
  name: string;
  description: string;
  communityId: string;
  communityName: string;
  processDefinitionVersionId: string;
  formDefinitionVersionId: string;
  versionNumber: number;
};

export type ProcessDefinitionVersion = {
  id: string;
  processDefinitionId: string;
  versionNumber: number;
  status: DefinitionVersionStatus;
  formDefinitionVersionId: string;
  graph: ProcessGraph;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type ProcessDefinition = ProcessDefinitionSummary & {
  createdByUserId: string;
  versions: ProcessDefinitionVersion[];
};

export type CreateProcessDefinitionRequest = {
  name: string;
  description: string;
  communityId?: string | null;
};

export type CreateProcessDefinitionVersionRequest = {
  formDefinitionVersionId: string;
  graph: ProcessGraph;
};

export type WorkflowValidationResult = {
  isValid: boolean;
  errors: string[];
};

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
  nodeKey?: string;
  attempt?: number;
  title?: string;
  priority?: TaskPriority;
  assignmentType?: TaskAssignmentType | null;
  assignedUserId?: string | null;
  candidateTeamId?: string | null;
  candidateCommunityRoleId?: string | null;
  claimedByUserId?: string | null;
  claimedAt?: string | null;
  claimVersion?: string | null;
  formDefinitionVersionId?: string | null;
  taskForm?: FormDefinitionVersion | null;
  dueAt?: string | null;
  workflowName?: string;
  formName?: string;
  communityName?: string;
};

export type ProcessListParams = {
  page?: number;
  pageSize?: number;
  status?: ProcessStatus | "all";
  scope?: WorkflowVisibilityScope | "visible" | "startedByMe" | "assignedToMe";
  sortBy?: "startedAt" | "dueAt" | "priority" | "status";
  sortDirection?: "asc" | "desc";
};

export type WorkflowVisibilityScope = "personal" | "community" | "global";

export type TaskListParams = {
  page?: number;
  pageSize?: number;
  priority?: TaskPriority | "all";
  taskId?: string;
  sortBy?: "dueAt" | "priority" | "newest" | "oldest";
  sortDirection?: "asc" | "desc";
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
  communityId?: string | null;
  actorDisplayName: string;
  actorUsername: string;
  category: "identity" | "access" | "forms" | "processes" | "tasks" | "other";
  action: string;
  entityType: string;
  entityId?: string | null;
  description: string;
  metadataJson?: string | null;
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
  processDefinitionVersionId?: string | null;
  formDefinitionVersionId?: string | null;
  currentNodeKey?: string;
  workflowName?: string;
  nearestOpenTaskDueAt?: string | null;
  highestOpenTaskPriority?: TaskPriority | null;
};

export type ProcessStepExecution = {
  id: string;
  nodeKey: string;
  nodeType: ProcessNodeType;
  attempt: number;
  status: ProcessStepStatus;
  enteredAt: string;
  completedAt?: string | null;
  completedByUserId?: string | null;
  completedByUserDisplayName?: string | null;
  action?: WorkflowAction | null;
  output: Record<string, unknown>;
};

export type ProcessDetail = ProcessSummary & {
  formData: Record<string, unknown>;
  tasks: ProcessTask[];
  auditLogs: AuditLog[];
  variables?: Record<string, unknown>;
  stepExecutions?: ProcessStepExecution[];
};

export type StartProcessRequest = {
  formDefinitionId: string;
  formData: Record<string, unknown>;
};

export type TaskActionRequest = {
  action: WorkflowAction;
  note?: string;
  formData?: Record<string, unknown>;
};

export type ClaimTaskRequest = {
  claimVersion?: string | null;
};

export type DashboardSummary = {
  openTaskCount: number;
  inProgressProcessCount: number;
  completedProcessCount: number;
  recentOpenTasks?: DashboardTaskItem[];
  recentProcesses?: DashboardProcessItem[];
};

export type DashboardTaskItem = {
  id: string;
  processInstanceId: string;
  formName: string;
  status: ProcessTaskStatus;
  createdAt: string;
};

export type DashboardProcessItem = {
  id: string;
  formName: string;
  status: ProcessStatus;
  startedAt: string;
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

export type Team = {
  id: string;
  communityId: string;
  communityName: string;
  name: string;
  description: string;
  isActive: boolean;
  memberCount: number;
  leadCount: number;
  createdByUserId?: string | null;
  createdByDisplayName: string;
  createdAt: string;
  updatedAt: string;
};

export type TeamPage = PagedResult<Team> & {
  unassignedCount: number;
};

export type TeamMember = {
  userId: string;
  username: string;
  displayName: string;
  email: string;
  communityRoleName: string;
  isLead: boolean;
  joinedAt: string;
};

export type TeamMemberPage = PagedResult<TeamMember>;

export type UserTeamMembership = {
  teamId: string;
  teamName: string;
  teamIsActive: boolean;
  isLead: boolean;
  joinedAt: string;
};

export type TeamRosterMember = {
  userId: string;
  username: string;
  displayName: string;
  communityRoleName: string;
  isLead: boolean;
};

export type TeamRosterPage = PagedResult<TeamRosterMember>;

export type TeamCandidate = {
  userId: string;
  username: string;
  displayName: string;
  email: string;
  communityRoleName: string;
  activeTeamCount: number;
};

export type TeamCandidatePage = PagedResult<TeamCandidate>;

export type CreateTeamRequest = {
  communityId: string;
  name: string;
  description: string;
};

export type UpdateTeamRequest = {
  name: string;
  description: string;
  isActive: boolean;
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

export type NotificationReadStatus = "all" | "unread" | "read";
export type NotificationCategory = "all" | "task" | "process" | "access" | "account";

export type NotificationListParams = {
  page?: number;
  pageSize?: number;
  query?: string;
  readStatus?: NotificationReadStatus;
  category?: NotificationCategory;
};

export type NotificationPage = PagedResult<NotificationItem> & {
  allCount: number;
  unreadCount: number;
};
