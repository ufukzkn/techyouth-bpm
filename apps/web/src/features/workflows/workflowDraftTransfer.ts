import {
  workflowNodeKinds,
  type WorkflowAssignment,
  type WorkflowDefinitionDraft,
  type WorkflowNode,
  type WorkflowNodeKind,
  type WorkflowTransition,
} from "@/features/workflows/contracts";
import { cloneWorkflowDraft, createEmptyAssignment, orderWorkflowNodes } from "@/features/workflows/workflowDraft";

export const workflowDraftSchema = "techyouth.workflow-draft";
export const workflowDraftSchemaVersion = 1;

type WorkflowDraftEnvelope = {
  schema: typeof workflowDraftSchema;
  schemaVersion: typeof workflowDraftSchemaVersion;
  exportedAt: string;
  draft: WorkflowDefinitionDraft;
};

export function serializeWorkflowDraft(draft: WorkflowDefinitionDraft, exportedAt = new Date().toISOString()) {
  const envelope: WorkflowDraftEnvelope = {
    schema: workflowDraftSchema,
    schemaVersion: workflowDraftSchemaVersion,
    exportedAt,
    draft: cloneWorkflowDraft(draft),
  };
  return JSON.stringify(envelope, null, 2);
}

export function parseWorkflowDraft(content: string): WorkflowDefinitionDraft {
  const envelope = parseEnvelope(content);
  const source = requireRecord(envelope.draft, "DRAFT_INVALID");
  const name = requireString(source.name, "DRAFT_NAME_INVALID").trim();
  const description = requireString(source.description, "DRAFT_DESCRIPTION_INVALID");
  const rawNodes = requireArray(source.nodes, "DRAFT_NODES_INVALID");
  const rawEdges = requireArray(source.edges, "DRAFT_EDGES_INVALID");
  if (!name || rawNodes.length === 0 || rawNodes.length > 500 || rawEdges.length > 1000) {
    throw new Error("DRAFT_INVALID");
  }

  const nodes = rawNodes.map(parseNode);
  const nodeIds = new Set(nodes.map((node) => node.id));
  if (nodeIds.size !== nodes.length) throw new Error("DRAFT_NODE_INVALID");
  const edges = rawEdges.map((edge) => parseEdge(edge, nodeIds));
  const requiresBinding = collectRequiredBindings(nodes);

  return {
    name,
    description,
    status: "Draft",
    nodes: orderWorkflowNodes(nodes),
    edges,
    requiresBinding,
  };
}

function parseNode(value: unknown): WorkflowNode {
  const node = requireRecord(value, "DRAFT_NODE_INVALID");
  const id = requireNonEmptyString(node.id, "DRAFT_NODE_INVALID");
  const type = requireNodeKind(node.type);
  const data = requireRecord(node.data, "DRAFT_NODE_INVALID");
  if (data.kind !== type) throw new Error("DRAFT_NODE_INVALID");
  const position = requireRecord(node.position, "DRAFT_NODE_INVALID");
  const x = requireFiniteNumber(position.x, "DRAFT_NODE_INVALID");
  const y = requireFiniteNumber(position.y, "DRAFT_NODE_INVALID");
  const common = {
    id,
    type,
    position: { x, y },
    parentId: typeof node.parentId === "string" ? node.parentId : undefined,
    extent: node.extent === "parent" ? ("parent" as const) : undefined,
    width: optionalFiniteNumber(node.width),
    height: optionalFiniteNumber(node.height),
    zIndex: typeof node.zIndex === "number" ? node.zIndex : type === "teamSwimlane" ? -1 : 2,
    selected: false,
  };
  const label = requireString(data.label, "DRAFT_NODE_INVALID");
  const description = requireString(data.description, "DRAFT_NODE_INVALID");

  switch (type) {
    case "start":
      return { ...common, type, data: { kind: type, label, description, formBinding: null } };
    case "userTask": {
      const assignment = parseAssignment(data.assignment);
      const actions = requireArray(data.actions, "DRAFT_NODE_INVALID").filter(isTaskAction);
      if (actions.length === 0) throw new Error("DRAFT_NODE_INVALID");
      const priority = isPriority(data.priority) ? data.priority : "Normal";
      return {
        ...common,
        type,
        data: {
          kind: type,
          label,
          description,
          assignment: clearAssignment(assignment),
          actions,
          priority,
          slaDurationMinutes: typeof data.slaDurationMinutes === "number" ? data.slaDurationMinutes : null,
          slaUnit: data.slaUnit === "days" ? "days" : "hours",
          requiresTeamLead: data.requiresTeamLead === true,
          formBinding: null,
        },
      };
    }
    case "exclusiveGateway":
    case "completedEnd":
    case "rejectedEnd":
      return { ...common, type, data: { kind: type, label, description } } as WorkflowNode;
    case "teamSwimlane":
      return {
        ...common,
        type,
        parentId: undefined,
        extent: undefined,
        data: { kind: type, label, description, teamId: "", teamName: "" },
      };
  }
}

function parseEdge(value: unknown, nodeIds: Set<string>): WorkflowTransition {
  const edge = requireRecord(value, "DRAFT_EDGE_INVALID");
  const id = requireNonEmptyString(edge.id, "DRAFT_EDGE_INVALID");
  const source = requireNonEmptyString(edge.source, "DRAFT_EDGE_INVALID");
  const target = requireNonEmptyString(edge.target, "DRAFT_EDGE_INVALID");
  if (!nodeIds.has(source) || !nodeIds.has(target)) throw new Error("DRAFT_EDGE_INVALID");
  const data = requireRecord(edge.data, "DRAFT_EDGE_INVALID");
  const action = data.action === null || isTaskAction(data.action) ? data.action : null;
  const condition = data.condition === null ? null : parseCondition(data.condition);

  return {
    id,
    type: "workflowTransition",
    source,
    target,
    selected: false,
    data: {
      label: requireString(data.label, "DRAFT_EDGE_INVALID"),
      action,
      isDefault: data.isDefault === true,
      condition,
    },
  };
}

function parseCondition(value: unknown): WorkflowTransition["data"]["condition"] {
  const condition = requireRecord(value, "DRAFT_CONDITION_INVALID");
  const operators = ["Equals", "NotEquals", "GreaterThan", "GreaterThanOrEquals", "LessThan", "LessThanOrEquals", "Contains", "IsEmpty", "IsNotEmpty"];
  const valueTypes = ["String", "Number", "Boolean"];
  if (!operators.includes(String(condition.operator)) || !valueTypes.includes(String(condition.valueType))) {
    throw new Error("DRAFT_CONDITION_INVALID");
  }
  return {
    fieldKey: requireString(condition.fieldKey, "DRAFT_CONDITION_INVALID"),
    operator: condition.operator as NonNullable<WorkflowTransition["data"]["condition"]>["operator"],
    valueType: condition.valueType as NonNullable<WorkflowTransition["data"]["condition"]>["valueType"],
    value: requireString(condition.value, "DRAFT_CONDITION_INVALID"),
  };
}

function parseAssignment(value: unknown): WorkflowAssignment {
  const assignment = requireRecord(value, "DRAFT_ASSIGNMENT_INVALID");
  const type = assignment.type;
  if (type !== "processStarter" && type !== "person" && type !== "team" && type !== "communityRole" && type !== "teamAndRole") {
    throw new Error("DRAFT_ASSIGNMENT_INVALID");
  }
  return createEmptyAssignment(type);
}

function clearAssignment(assignment: WorkflowAssignment) {
  return assignment.type === "processStarter" ? assignment : createEmptyAssignment(assignment.type);
}

function collectRequiredBindings(nodes: WorkflowNode[]) {
  return nodes.flatMap((node) => {
    if (node.type === "start") return [`node:${node.id}:form`];
    if (node.type === "teamSwimlane") return [`node:${node.id}:team`];
    if (node.type !== "userTask") return [];
    const bindings = node.data.assignment.type === "processStarter"
      ? []
      : [`node:${node.id}:assignment`];
    return node.data.formBinding ? [...bindings, `node:${node.id}:form`] : bindings;
  });
}

function parseEnvelope(content: string) {
  let parsed: unknown;
  try {
    parsed = JSON.parse(content);
  } catch {
    throw new Error("DRAFT_JSON_INVALID");
  }
  const envelope = requireRecord(parsed, "DRAFT_INVALID");
  if (envelope.schema !== workflowDraftSchema || envelope.schemaVersion !== workflowDraftSchemaVersion) {
    throw new Error("DRAFT_SCHEMA_UNSUPPORTED");
  }
  return envelope;
}

function requireNodeKind(value: unknown): WorkflowNodeKind {
  if (!workflowNodeKinds.includes(value as WorkflowNodeKind)) throw new Error("DRAFT_NODE_INVALID");
  return value as WorkflowNodeKind;
}

function isTaskAction(value: unknown): value is "Approve" | "Reject" | "Escalate" | "SendBack" | "Complete" {
  return value === "Approve" || value === "Reject" || value === "Escalate" || value === "SendBack" || value === "Complete";
}

function isPriority(value: unknown): value is "Low" | "Normal" | "High" | "Critical" {
  return value === "Low" || value === "Normal" || value === "High" || value === "Critical";
}

function requireRecord(value: unknown, code: string): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error(code);
  return value as Record<string, unknown>;
}

function requireArray(value: unknown, code: string): unknown[] {
  if (!Array.isArray(value)) throw new Error(code);
  return value;
}

function requireString(value: unknown, code: string) {
  if (typeof value !== "string") throw new Error(code);
  return value;
}

function requireNonEmptyString(value: unknown, code: string) {
  const result = requireString(value, code).trim();
  if (!result) throw new Error(code);
  return result;
}

function requireFiniteNumber(value: unknown, code: string) {
  if (typeof value !== "number" || !Number.isFinite(value)) throw new Error(code);
  return value;
}

function optionalFiniteNumber(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) && value > 0 ? value : undefined;
}
