import type {
  ApiProcessEdge,
  ApiProcessGraph,
  ApiProcessNode,
  ApiProcessNodeType,
  ApiTaskAssignment,
  SaveWorkflowDraftRequest,
  WorkflowAssignment,
  WorkflowCondition,
  WorkflowConditionValueType,
  WorkflowDefinitionDraft,
  WorkflowNode,
  WorkflowNodeData,
  WorkflowNodeKind,
  WorkflowTransition,
} from "@/features/workflows/contracts";
import {
  cloneWorkflowDraft,
  createEmptyAssignment,
  orderWorkflowNodes,
  workflowNodeDimensions,
} from "@/features/workflows/workflowDraft";

const nodeKindToApiType: Record<WorkflowNodeKind, ApiProcessNodeType> = {
  start: "Start",
  userTask: "UserTask",
  exclusiveGateway: "ExclusiveGateway",
  completedEnd: "CompletedEnd",
  rejectedEnd: "RejectedEnd",
  teamSwimlane: "TeamSwimlane",
};

const apiTypeToNodeKind: Record<ApiProcessNodeType, WorkflowNodeKind> = {
  Start: "start",
  UserTask: "userTask",
  ExclusiveGateway: "exclusiveGateway",
  CompletedEnd: "completedEnd",
  RejectedEnd: "rejectedEnd",
  TeamSwimlane: "teamSwimlane",
};

export type ApiGraphDraftMetadata = {
  id?: string;
  version?: number;
  name: string;
  description: string;
  status?: WorkflowDefinitionDraft["status"];
  updatedAt?: string;
  publishedAt?: string | null;
  formDefinitionVersionId?: string;
};

export function toApiProcessGraph(draft: WorkflowDefinitionDraft): ApiProcessGraph {
  return {
    schemaVersion: "1.0",
    nodes: draft.nodes.map(toApiNode),
    edges: draft.edges.map(toApiEdge),
  };
}

export function fromApiProcessGraph(
  graph: ApiProcessGraph,
  metadata: ApiGraphDraftMetadata,
): WorkflowDefinitionDraft {
  const { formDefinitionVersionId, ...draftMetadata } = metadata;
  const nodes = orderWorkflowNodes(graph.nodes.map(fromApiNode));
  if (formDefinitionVersionId) {
    const start = nodes.find((node) => node.type === "start");
    if (start?.type === "start" && !start.data.formBinding?.formVersionId) {
      start.data.formBinding = {
        formVersionId: formDefinitionVersionId,
        formName: "",
        version: null,
        mode: "Required",
      };
    }
  }
  return cloneWorkflowDraft({
    ...draftMetadata,
    status: draftMetadata.status ?? "Draft",
    nodes,
    edges: [...graph.edges]
      .sort((left, right) => left.order - right.order)
      .map((edge, index) => fromApiEdge(edge, index)),
  });
}

export function createWorkflowWriteModel(draft: WorkflowDefinitionDraft): SaveWorkflowDraftRequest {
  const start = draft.nodes.find((node) => node.type === "start");
  return {
    id: draft.id,
    expectedVersion: draft.version,
    name: draft.name.trim(),
    description: draft.description.trim(),
    formDefinitionVersionId: start?.type === "start" ? start.data.formBinding?.formVersionId ?? "" : "",
    graph: toApiProcessGraph(draft),
  };
}

function toApiNode(node: WorkflowNode): ApiProcessNode {
  const dimensions = workflowNodeDimensions[node.type];
  const base: ApiProcessNode = {
    key: node.id,
    type: nodeKindToApiType[node.type],
    title: node.data.label.trim(),
    description: node.data.description.trim() || null,
    priority: node.type === "userTask" ? node.data.priority : "Normal",
    parentKey: node.parentId ?? null,
    positionX: node.position.x,
    positionY: node.position.y,
    width: node.width ?? node.measured?.width ?? dimensions.width,
    height: node.height ?? node.measured?.height ?? dimensions.height,
  };

  if (node.type === "start") {
    return {
      ...base,
      formDefinitionVersionId: node.data.formBinding?.formVersionId || null,
    };
  }

  if (node.type === "userTask") {
    return {
      ...base,
      formDefinitionVersionId: node.data.formBinding?.formVersionId || null,
      slaDurationMinutes: node.data.slaDurationMinutes,
      actions: node.data.actions,
      assignment: toApiAssignment(node.data.assignment),
    };
  }

  if (node.type === "teamSwimlane") {
    return {
      ...base,
      teamId: node.data.teamId || null,
    };
  }

  return base;
}

function toApiEdge(edge: WorkflowTransition, index: number): ApiProcessEdge {
  return {
    source: edge.source,
    target: edge.target,
    action: edge.data?.action ?? null,
    condition: edge.data?.condition ? {
      path: edge.data.condition.fieldKey.trim(),
      operator: edge.data.condition.operator,
      value: toApiConditionValue(edge.data.condition),
    } : null,
    isDefault: edge.data?.isDefault ?? false,
    order: index,
    label: edge.data?.label.trim() || null,
  };
}

function fromApiNode(node: ApiProcessNode): WorkflowNode {
  const type = apiTypeToNodeKind[node.type];
  const dimensions = workflowNodeDimensions[type];
  const common = {
    id: node.key,
    type,
    position: { x: node.positionX, y: node.positionY },
    width: node.width ?? dimensions.width,
    height: node.height ?? dimensions.height,
    parentId: node.parentKey ?? undefined,
    extent: node.parentKey ? ("parent" as const) : undefined,
    zIndex: type === "teamSwimlane" ? -1 : 2,
  };

  return { ...common, data: fromApiNodeData(node, type) } as WorkflowNode;
}

function fromApiNodeData(node: ApiProcessNode, type: WorkflowNodeKind): WorkflowNodeData {
  const description = node.description ?? "";
  switch (type) {
    case "start":
      return {
        kind: type,
        label: node.title,
        description,
        formBinding: node.formDefinitionVersionId ? {
          formVersionId: node.formDefinitionVersionId,
          formName: "",
          version: null,
          mode: "Required",
        } : null,
      };
    case "userTask":
      return {
        kind: type,
        label: node.title,
        description,
        assignment: fromApiAssignment(node.assignment),
        actions: (node.actions ?? []).filter((action) => action !== "Start"),
        priority: node.priority,
        slaDurationMinutes: node.slaDurationMinutes ?? null,
        formBinding: node.formDefinitionVersionId ? {
          formVersionId: node.formDefinitionVersionId,
          formName: "",
          version: null,
          mode: "Required",
        } : null,
      };
    case "exclusiveGateway":
      return { kind: type, label: node.title, description };
    case "completedEnd":
      return { kind: type, label: node.title, description };
    case "rejectedEnd":
      return { kind: type, label: node.title, description };
    case "teamSwimlane":
      return {
        kind: type,
        label: node.title,
        description,
        teamId: node.teamId ?? "",
        teamName: "",
      };
  }
}

function fromApiEdge(edge: ApiProcessEdge, index: number): WorkflowTransition {
  return {
    id: createStableEdgeId(edge, index),
    type: "workflowTransition",
    source: edge.source,
    target: edge.target,
    data: {
      label: edge.label ?? "",
      action: edge.action && edge.action !== "Start" ? edge.action : null,
      isDefault: edge.isDefault,
      condition: edge.condition ? {
        fieldKey: edge.condition.path,
        operator: edge.condition.operator,
        valueType: inferConditionValueType(edge.condition.value),
        value: edge.condition.value == null ? "" : String(edge.condition.value),
      } : null,
    },
  };
}

function toApiAssignment(assignment: WorkflowAssignment): ApiTaskAssignment {
  switch (assignment.type) {
    case "processStarter":
      return { type: "ProcessStarter" };
    case "person":
      return { type: "SpecificUser", userId: assignment.personId || null };
    case "team":
      return { type: "Team", teamId: assignment.teamId || null };
    case "communityRole":
      return { type: "CommunityRole", communityRoleId: assignment.communityRoleId || null };
    case "teamAndRole":
      return {
        type: "TeamAndCommunityRole",
        teamId: assignment.teamId || null,
        communityRoleId: assignment.communityRoleId || null,
      };
  }
}

function fromApiAssignment(assignment: ApiTaskAssignment | null | undefined): WorkflowAssignment {
  if (!assignment) {
    return createEmptyAssignment("processStarter");
  }
  switch (assignment.type) {
    case "ProcessStarter":
      return { type: "processStarter" };
    case "SpecificUser":
      return { type: "person", personId: assignment.userId ?? "", personName: "" };
    case "Team":
      return { type: "team", teamId: assignment.teamId ?? "", teamName: "" };
    case "CommunityRole":
      return {
        type: "communityRole",
        communityRoleId: assignment.communityRoleId ?? "",
        communityRoleName: "",
      };
    case "TeamAndCommunityRole":
      return {
        type: "teamAndRole",
        teamId: assignment.teamId ?? "",
        teamName: "",
        communityRoleId: assignment.communityRoleId ?? "",
        communityRoleName: "",
      };
  }
}

function toApiConditionValue(condition: WorkflowCondition): unknown {
  if (condition.operator === "IsEmpty" || condition.operator === "IsNotEmpty") {
    return null;
  }
  switch (condition.valueType) {
    case "Number": {
      const numeric = Number(condition.value);
      return Number.isFinite(numeric) ? numeric : condition.value;
    }
    case "Boolean":
      return condition.value === "true";
    case "String":
      return condition.value;
  }
}

function inferConditionValueType(value: unknown): WorkflowConditionValueType {
  if (typeof value === "number") {
    return "Number";
  }
  if (typeof value === "boolean") {
    return "Boolean";
  }
  return "String";
}

function createStableEdgeId(edge: ApiProcessEdge, index: number) {
  return `edge-${safeKeyPart(edge.source)}-${safeKeyPart(edge.target)}-${edge.order}-${index}`;
}

function safeKeyPart(value: string) {
  return value.replace(/[^a-zA-Z0-9_-]/g, "-");
}

export function resolveLookupLabels(
  draft: WorkflowDefinitionDraft,
  labels: {
    people?: Record<string, string>;
    teams?: Record<string, string>;
    communityRoles?: Record<string, string>;
    formVersions?: Record<string, { name: string; version: number | null }>;
  },
): WorkflowDefinitionDraft {
  const next = cloneWorkflowDraft(draft);
  next.nodes = next.nodes.map((node) => {
    if (node.type === "teamSwimlane") {
      return {
        ...node,
        data: { ...node.data, teamName: labels.teams?.[node.data.teamId] ?? node.data.teamName },
      };
    }
    if (node.type === "start") {
      return { ...node, data: { ...node.data, formBinding: resolveForm(node.data.formBinding, labels.formVersions) } };
    }
    if (node.type !== "userTask") {
      return node;
    }
    return {
      ...node,
      data: {
        ...node.data,
        assignment: resolveAssignment(node.data.assignment, labels),
        formBinding: resolveForm(node.data.formBinding, labels.formVersions),
      },
    };
  });
  return next;
}

function resolveAssignment(
  assignment: WorkflowAssignment,
  labels: Parameters<typeof resolveLookupLabels>[1],
): WorkflowAssignment {
  switch (assignment.type) {
    case "processStarter":
      return assignment;
    case "person":
      return { ...assignment, personName: labels.people?.[assignment.personId] ?? assignment.personName };
    case "team":
      return { ...assignment, teamName: labels.teams?.[assignment.teamId] ?? assignment.teamName };
    case "communityRole":
      return {
        ...assignment,
        communityRoleName: labels.communityRoles?.[assignment.communityRoleId] ?? assignment.communityRoleName,
      };
    case "teamAndRole":
      return {
        ...assignment,
        teamName: labels.teams?.[assignment.teamId] ?? assignment.teamName,
        communityRoleName: labels.communityRoles?.[assignment.communityRoleId] ?? assignment.communityRoleName,
      };
  }
}

function resolveForm(
  binding: Extract<WorkflowNodeData, { kind: "start" | "userTask" }>["formBinding"],
  forms: Parameters<typeof resolveLookupLabels>[1]["formVersions"],
) {
  const form = binding ? forms?.[binding.formVersionId] : undefined;
  return binding && form ? { ...binding, formName: form.name, version: form.version } : binding;
}
