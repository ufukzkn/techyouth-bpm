import type {
  CommunityRoleAssignment,
  PersonAssignment,
  ProcessStarterAssignment,
  TeamAndRoleAssignment,
  TeamAssignment,
  WorkflowAssignment,
  WorkflowAssignmentType,
  WorkflowDefinitionDraft,
  WorkflowNode,
  WorkflowNodeKind,
  WorkflowTransition,
} from "@/features/workflows/contracts";

export const workflowLaneWidth = 1040;
export const workflowLaneHeight = 220;
export const workflowNodeDimensions: Record<WorkflowNodeKind, { width: number; height: number }> = {
  start: { width: 190, height: 84 },
  userTask: { width: 230, height: 112 },
  exclusiveGateway: { width: 176, height: 104 },
  completedEnd: { width: 190, height: 76 },
  rejectedEnd: { width: 190, height: 76 },
  teamSwimlane: { width: workflowLaneWidth, height: workflowLaneHeight },
};

const defaultTransitions: WorkflowTransition[] = [
  createTransition("transition-start-review", "node-start", "node-review"),
  createTransition("transition-review-gateway", "node-review", "node-decision", { action: "Approve" }),
  createTransition("transition-review-reject", "node-review", "node-rejected", { action: "Reject" }),
  createTransition("transition-gateway-complete", "node-decision", "node-completed", {
    label: "Limit dahilinde",
    condition: {
      fieldKey: "start.amount",
      operator: "LessThanOrEquals",
      valueType: "Number",
      value: "50000",
    },
  }),
  createTransition("transition-gateway-reject", "node-decision", "node-rejected", {
    label: "Diğer durumlar",
    isDefault: true,
  }),
];

export function createStarterWorkflowDraft(name = "Yeni Akış"): WorkflowDefinitionDraft {
  const nodes: WorkflowNode[] = [
    {
      id: "lane-request",
      type: "teamSwimlane",
      position: { x: 20, y: 20 },
      width: workflowLaneWidth,
      height: workflowLaneHeight,
      zIndex: -1,
      data: {
        kind: "teamSwimlane",
        label: "Talep",
        description: "",
        teamId: "",
        teamName: "Takım seçilmedi",
      },
    },
    {
      id: "lane-review",
      type: "teamSwimlane",
      position: { x: 20, y: 280 },
      width: workflowLaneWidth,
      height: workflowLaneHeight,
      zIndex: -1,
      data: {
        kind: "teamSwimlane",
        label: "İnceleme",
        description: "",
        teamId: "",
        teamName: "Takım seçilmedi",
      },
    },
    {
      id: "node-start",
      type: "start",
      parentId: "lane-request",
      extent: "parent",
      position: { x: 72, y: 72 },
      width: workflowNodeDimensions.start.width,
      height: workflowNodeDimensions.start.height,
      zIndex: 2,
      data: {
        kind: "start",
        label: "Talep oluşturuldu",
        description: "",
        formBinding: null,
      },
    },
    {
      id: "node-review",
      type: "userTask",
      parentId: "lane-review",
      extent: "parent",
      position: { x: 150, y: 66 },
      width: workflowNodeDimensions.userTask.width,
      height: workflowNodeDimensions.userTask.height,
      zIndex: 2,
      data: {
        kind: "userTask",
        label: "Talebi incele",
        description: "",
        assignment: createEmptyAssignment("team"),
        actions: ["Approve", "Reject"],
        priority: "High",
        slaDurationMinutes: null,
        requiresTeamLead: false,
        formBinding: null,
      },
    },
    {
      id: "node-decision",
      type: "exclusiveGateway",
      parentId: "lane-review",
      extent: "parent",
      position: { x: 460, y: 70 },
      width: workflowNodeDimensions.exclusiveGateway.width,
      height: workflowNodeDimensions.exclusiveGateway.height,
      zIndex: 2,
      data: {
        kind: "exclusiveGateway",
        label: "Limit kontrolü",
        description: "",
      },
    },
    {
      id: "node-completed",
      type: "completedEnd",
      parentId: "lane-review",
      extent: "parent",
      position: { x: 760, y: 34 },
      width: workflowNodeDimensions.completedEnd.width,
      height: workflowNodeDimensions.completedEnd.height,
      zIndex: 2,
      data: {
        kind: "completedEnd",
        label: "Tamamlandı",
        description: "",
      },
    },
    {
      id: "node-rejected",
      type: "rejectedEnd",
      parentId: "lane-review",
      extent: "parent",
      position: { x: 760, y: 128 },
      width: workflowNodeDimensions.rejectedEnd.width,
      height: workflowNodeDimensions.rejectedEnd.height,
      zIndex: 2,
      data: {
        kind: "rejectedEnd",
        label: "Reddedildi",
        description: "",
      },
    },
  ];

  return {
    name,
    description: "Talepleri ekip ve tutar kurallarına göre yönlendirir.",
    status: "Draft",
    nodes,
    edges: defaultTransitions.map(cloneTransition),
  };
}

export function createEmptyAssignment(type: WorkflowAssignmentType): WorkflowAssignment {
  const assignments: Record<WorkflowAssignmentType, WorkflowAssignment> = {
    processStarter: { type: "processStarter" } satisfies ProcessStarterAssignment,
    person: { type: "person", personId: "", personName: "" } satisfies PersonAssignment,
    team: { type: "team", teamId: "", teamName: "" } satisfies TeamAssignment,
    communityRole: {
      type: "communityRole",
      communityRoleId: "",
      communityRoleName: "",
    } satisfies CommunityRoleAssignment,
    teamAndRole: {
      type: "teamAndRole",
      teamId: "",
      teamName: "",
      communityRoleId: "",
      communityRoleName: "",
    } satisfies TeamAndRoleAssignment,
  };

  return assignments[type];
}

export function getNextWorkflowName(definitions: ReadonlyArray<{ name: string }>) {
  const names = new Set(definitions.map((definition) => definition.name.trim().toLocaleLowerCase("tr-TR")));
  let sequence = 1;
  while (names.has(`yeni akış ${sequence}`)) {
    sequence += 1;
  }
  return `Yeni Akış ${sequence}`;
}

export function createWorkflowNode(
  kind: WorkflowNodeKind,
  id: string,
  position: { x: number; y: number },
  parentId?: string,
): WorkflowNode {
  const common = {
    id,
    position,
    parentId,
    extent: parentId ? ("parent" as const) : undefined,
    zIndex: kind === "teamSwimlane" ? -1 : 2,
    width: workflowNodeDimensions[kind].width,
    height: workflowNodeDimensions[kind].height,
  };

  switch (kind) {
    case "start":
      return {
        ...common,
        type: kind,
        data: { kind, label: "Başlangıç", description: "", formBinding: null },
      };
    case "userTask":
      return {
        ...common,
        type: kind,
        data: {
          kind,
          label: "Kullanıcı görevi",
          description: "",
          assignment: createEmptyAssignment("team"),
          actions: ["Approve", "Reject"],
          priority: "Normal",
          slaDurationMinutes: null,
          requiresTeamLead: false,
          formBinding: null,
        },
      };
    case "exclusiveGateway":
      return {
        ...common,
        type: kind,
        data: { kind, label: "Karar", description: "" },
      };
    case "completedEnd":
      return {
        ...common,
        type: kind,
        data: { kind, label: "Tamamlandı", description: "" },
      };
    case "rejectedEnd":
      return {
        ...common,
        type: kind,
        data: { kind, label: "Reddedildi", description: "" },
      };
    case "teamSwimlane":
      return {
        ...common,
        type: kind,
        parentId: undefined,
        extent: undefined,
        width: workflowLaneWidth,
        height: workflowLaneHeight,
        data: {
          kind,
          label: "Takım kulvarı",
          description: "",
          teamId: "",
          teamName: "Takım seçilmedi",
        },
      };
  }
}

export function createTransition(
  id: string,
  source: string,
  target: string,
  patch: Partial<WorkflowTransition["data"]> = {},
): WorkflowTransition {
  return {
    id,
    type: "workflowTransition",
    source,
    target,
    data: {
      label: "",
      action: null,
      isDefault: false,
      condition: null,
      ...patch,
    },
  };
}

export function cloneWorkflowDraft(draft: WorkflowDefinitionDraft): WorkflowDefinitionDraft {
  return JSON.parse(JSON.stringify(draft)) as WorkflowDefinitionDraft;
}

export function orderWorkflowNodes(nodes: WorkflowNode[]) {
  return [...nodes].sort((left, right) => {
    const leftIsLane = left.type === "teamSwimlane";
    const rightIsLane = right.type === "teamSwimlane";
    return leftIsLane === rightIsLane ? 0 : leftIsLane ? -1 : 1;
  });
}

function cloneTransition(transition: WorkflowTransition): WorkflowTransition {
  return {
    ...transition,
    data: JSON.parse(JSON.stringify(transition.data)) as WorkflowTransition["data"],
  };
}
