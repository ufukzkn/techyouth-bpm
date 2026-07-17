import type { Edge, Node } from "@xyflow/react";

export const workflowNodeKinds = [
  "start",
  "userTask",
  "exclusiveGateway",
  "completedEnd",
  "rejectedEnd",
  "teamSwimlane",
] as const;

export type WorkflowNodeKind = (typeof workflowNodeKinds)[number];
export type WorkflowDefinitionStatus = "Draft" | "Published";
export type WorkflowTaskPriority = "Low" | "Normal" | "High" | "Critical";
export type WorkflowTaskAction = "Approve" | "Reject" | "Escalate" | "SendBack" | "Complete";
export type WorkflowAssignmentType = "processStarter" | "person" | "team" | "communityRole" | "teamAndRole";
export type WorkflowFormMode = "Required" | "Optional" | "ReadOnly";
export type WorkflowConditionOperator =
  | "Equals"
  | "NotEquals"
  | "Contains"
  | "GreaterThan"
  | "GreaterThanOrEquals"
  | "LessThan"
  | "LessThanOrEquals"
  | "IsEmpty"
  | "IsNotEmpty";
export type WorkflowConditionValueType = "String" | "Number" | "Boolean";

export type WorkflowLookupOption = {
  id: string;
  label: string;
  description?: string;
};

export type WorkflowFormVersionOption = WorkflowLookupOption & {
  definitionId: string;
  version: number;
  fields: Array<{
    key: string;
    label: string;
    valueType: WorkflowConditionValueType;
  }>;
};

export type WorkflowEditorLookups = {
  people: WorkflowLookupOption[];
  teams: WorkflowLookupOption[];
  communityRoles: WorkflowLookupOption[];
  formVersions: WorkflowFormVersionOption[];
};

export type ProcessStarterAssignment = {
  type: "processStarter";
};

export type PersonAssignment = {
  type: "person";
  personId: string;
  personName: string;
};

export type TeamAssignment = {
  type: "team";
  teamId: string;
  teamName: string;
};

export type CommunityRoleAssignment = {
  type: "communityRole";
  communityRoleId: string;
  communityRoleName: string;
};

export type TeamAndRoleAssignment = {
  type: "teamAndRole";
  teamId: string;
  teamName: string;
  communityRoleId: string;
  communityRoleName: string;
};

export type WorkflowAssignment =
  | ProcessStarterAssignment
  | PersonAssignment
  | TeamAssignment
  | CommunityRoleAssignment
  | TeamAndRoleAssignment;

export type WorkflowFormBinding = {
  formVersionId: string;
  formName: string;
  version: number | null;
  mode: WorkflowFormMode;
};

type WorkflowNodeDataBase = Record<string, unknown> & {
  label: string;
  description: string;
};

export type StartNodeData = WorkflowNodeDataBase & {
  kind: "start";
  formBinding: WorkflowFormBinding | null;
};

export type UserTaskNodeData = WorkflowNodeDataBase & {
  kind: "userTask";
  assignment: WorkflowAssignment;
  actions: WorkflowTaskAction[];
  priority: WorkflowTaskPriority;
  slaDurationMinutes?: number | null;
  slaUnit?: "hours" | "days";
  requiresTeamLead?: boolean;
  formBinding: WorkflowFormBinding | null;
};

export type ExclusiveGatewayNodeData = WorkflowNodeDataBase & {
  kind: "exclusiveGateway";
};

export type CompletedEndNodeData = WorkflowNodeDataBase & {
  kind: "completedEnd";
};

export type RejectedEndNodeData = WorkflowNodeDataBase & {
  kind: "rejectedEnd";
};

export type TeamSwimlaneNodeData = WorkflowNodeDataBase & {
  kind: "teamSwimlane";
  teamId: string;
  teamName: string;
};

export type WorkflowNodeData =
  | StartNodeData
  | UserTaskNodeData
  | ExclusiveGatewayNodeData
  | CompletedEndNodeData
  | RejectedEndNodeData
  | TeamSwimlaneNodeData;

export type StartNode = Node<StartNodeData, "start">;
export type UserTaskNode = Node<UserTaskNodeData, "userTask">;
export type ExclusiveGatewayNode = Node<ExclusiveGatewayNodeData, "exclusiveGateway">;
export type CompletedEndNode = Node<CompletedEndNodeData, "completedEnd">;
export type RejectedEndNode = Node<RejectedEndNodeData, "rejectedEnd">;
export type TeamSwimlaneNode = Node<TeamSwimlaneNodeData, "teamSwimlane">;
export type WorkflowNode =
  | StartNode
  | UserTaskNode
  | ExclusiveGatewayNode
  | CompletedEndNode
  | RejectedEndNode
  | TeamSwimlaneNode;

export type WorkflowCondition = {
  fieldKey: string;
  operator: WorkflowConditionOperator;
  valueType: WorkflowConditionValueType;
  value: string;
};

export type WorkflowTransitionData = Record<string, unknown> & {
  label: string;
  action: WorkflowTaskAction | null;
  isDefault: boolean;
  condition: WorkflowCondition | null;
};

export type WorkflowTransition = Edge<WorkflowTransitionData, "workflowTransition"> & {
  data: WorkflowTransitionData;
};

export type WorkflowDefinitionDraft = {
  id?: string;
  version?: number;
  name: string;
  description: string;
  status: WorkflowDefinitionStatus;
  nodes: WorkflowNode[];
  edges: WorkflowTransition[];
  updatedAt?: string;
  publishedAt?: string | null;
};

export type ApiTaskAssignmentType =
  | "ProcessStarter"
  | "SpecificUser"
  | "Team"
  | "CommunityRole"
  | "TeamAndCommunityRole";

export type ApiProcessNodeType =
  | "Start"
  | "UserTask"
  | "ExclusiveGateway"
  | "CompletedEnd"
  | "RejectedEnd"
  | "TeamSwimlane";

export type ApiWorkflowAction = "Start" | WorkflowTaskAction;

export type ApiTaskAssignment = {
  type: ApiTaskAssignmentType;
  userId?: string | null;
  teamId?: string | null;
  communityRoleId?: string | null;
};

export type ApiProcessNode = {
  key: string;
  type: ApiProcessNodeType;
  title: string;
  description?: string | null;
  formDefinitionVersionId?: string | null;
  priority: WorkflowTaskPriority;
  slaDurationMinutes?: number | null;
  requiresTeamLead?: boolean;
  actions?: ApiWorkflowAction[] | null;
  assignment?: ApiTaskAssignment | null;
  parentKey?: string | null;
  positionX: number;
  positionY: number;
  width?: number | null;
  height?: number | null;
  teamId?: string | null;
};

export type ApiProcessCondition = {
  path: string;
  operator: WorkflowConditionOperator;
  value?: unknown;
};

export type ApiProcessEdge = {
  source: string;
  target: string;
  action?: ApiWorkflowAction | null;
  condition?: ApiProcessCondition | null;
  isDefault: boolean;
  order: number;
  label?: string | null;
};

export type ApiProcessGraph = {
  schemaVersion: string;
  nodes: ApiProcessNode[];
  edges: ApiProcessEdge[];
};

export type WorkflowDefinitionWriteModel = {
  id?: string;
  expectedVersion?: number;
  name: string;
  description: string;
  formDefinitionVersionId: string;
  graph: ApiProcessGraph;
};

export type SaveWorkflowDraftRequest = WorkflowDefinitionWriteModel;
export type PublishWorkflowRequest = WorkflowDefinitionWriteModel;

export type WorkflowMutationResult = {
  draft: WorkflowDefinitionDraft;
  message?: string;
};

export type WorkflowValidationSeverity = "error" | "warning";
export type WorkflowValidationScope = "workflow" | "node" | "transition";

export type WorkflowValidationIssue = {
  code: string;
  severity: WorkflowValidationSeverity;
  scope: WorkflowValidationScope;
  entityId?: string;
  message: string;
};

export type WorkflowSaveHandler = (
  request: SaveWorkflowDraftRequest,
) => Promise<WorkflowMutationResult | void> | WorkflowMutationResult | void;

export type WorkflowPublishHandler = (
  request: PublishWorkflowRequest,
) => Promise<WorkflowMutationResult | void> | WorkflowMutationResult | void;

export const emptyWorkflowLookups: WorkflowEditorLookups = {
  people: [],
  teams: [],
  communityRoles: [],
  formVersions: [],
};
