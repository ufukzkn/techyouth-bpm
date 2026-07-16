import type {
  UserTaskNodeData,
  WorkflowAssignment,
  WorkflowDefinitionDraft,
  WorkflowNode,
  WorkflowTransition,
  WorkflowValidationIssue,
} from "@/features/workflows/contracts";

const flowNodeTypes = new Set(["start", "userTask", "exclusiveGateway", "completedEnd", "rejectedEnd"]);
const terminalNodeTypes = new Set(["completedEnd", "rejectedEnd"]);

export function validateWorkflow(draft: WorkflowDefinitionDraft): WorkflowValidationIssue[] {
  const issues: WorkflowValidationIssue[] = [];
  const flowNodes = draft.nodes.filter(isFlowNode);
  const nodeIds = new Set(draft.nodes.map((node) => node.id));
  const starts = flowNodes.filter((node) => node.type === "start");

  if (nodeIds.size !== draft.nodes.length) {
    issues.push(issue("workflow.node.keys.duplicate", "error", "workflow", "Düğüm anahtarları benzersiz olmalıdır."));
  }

  if (!draft.name.trim()) {
    issues.push(issue("workflow.name.required", "error", "workflow", "Akış adı zorunludur."));
  }

  if (starts.length !== 1) {
    issues.push(issue(
      "workflow.start.count",
      "error",
      "workflow",
      starts.length === 0 ? "Akışta bir başlangıç düğümü olmalıdır." : "Akışta yalnızca bir başlangıç düğümü olabilir.",
    ));
  }

  if (!flowNodes.some((node) => terminalNodeTypes.has(node.type))) {
    issues.push(issue("workflow.end.required", "error", "workflow", "Akışta en az bir sonuç düğümü olmalıdır."));
  }

  const validEdges = draft.edges.filter((edge) => {
    if (!nodeIds.has(edge.source) || !nodeIds.has(edge.target)) {
      issues.push(issue(
        "transition.endpoint.missing",
        "error",
        "transition",
        "Bağlantının kaynak veya hedef düğümü bulunamadı.",
        edge.id,
      ));
      return false;
    }

    const source = draft.nodes.find((node) => node.id === edge.source);
    const target = draft.nodes.find((node) => node.id === edge.target);
    if (source?.type === "teamSwimlane" || target?.type === "teamSwimlane") {
      issues.push(issue(
        "transition.swimlane.invalid",
        "error",
        "transition",
        "Takım kulvarları akış bağlantısına dahil edilemez.",
        edge.id,
      ));
      return false;
    }

    if (edge.source === edge.target) {
      issues.push(issue("transition.self.invalid", "error", "transition", "Bir düğüm kendisine bağlanamaz.", edge.id));
      return false;
    }

    return true;
  });

  draft.nodes.forEach((node) => {
    if (!node.id.trim()) {
      issues.push(issue("node.key.required", "error", "node", "Düğüm için kalıcı bir anahtar zorunludur.", node.id));
    }
    if (!node.data.label.trim()) {
      issues.push(issue("node.label.required", "error", "node", "Düğüm adı zorunludur.", node.id));
    }
    if (!Number.isFinite(node.position.x) || !Number.isFinite(node.position.y)) {
      issues.push(issue("node.position.invalid", "error", "node", "Düğüm konumu geçerli sayılardan oluşmalıdır.", node.id));
    }
    if ((node.width != null && (!Number.isFinite(node.width) || node.width <= 0))
      || (node.height != null && (!Number.isFinite(node.height) || node.height <= 0))) {
      issues.push(issue("node.size.invalid", "error", "node", "Düğüm ölçüleri pozitif sayılar olmalıdır.", node.id));
    }
    if (node.parentId) {
      const parent = draft.nodes.find((candidate) => candidate.id === node.parentId);
      if (parent?.type !== "teamSwimlane") {
        issues.push(issue("node.parent.invalid", "error", "node", "Düğüm üst öğesi bir takım kulvarı olmalıdır.", node.id));
      }
    }

    if (node.type === "teamSwimlane") {
      if (!node.data.teamId) {
        issues.push(issue("swimlane.team.required", "error", "node", "Kulvar için bir takım seçilmelidir.", node.id));
      }
      return;
    }

    validateNodeConnections(node, validEdges, issues);

    if (node.type === "start" && !node.data.formBinding?.formVersionId) {
      issues.push(issue(
        "start.form.required",
        "error",
        "node",
        "Başlangıç düğümünde bir form sürümü seçilmelidir.",
        node.id,
      ));
    }

    if (node.type === "userTask") {
      validateUserTask(node.data, node.id, issues);
      validateTaskTransitions(node, validEdges, draft.nodes, issues);
    }

    if (node.type === "exclusiveGateway") {
      validateGateway(node.id, validEdges, issues);
    }
  });

  validEdges.forEach((edge) => validateTransition(edge, draft.nodes, issues));
  validateReachability(starts[0], flowNodes, validEdges, issues);
  validateCyclesAndSendBack(flowNodes, validEdges, issues);
  validateGatewayConditionSources(validEdges, issues);

  return issues;
}

export function workflowHasErrors(issues: WorkflowValidationIssue[]) {
  return issues.some((item) => item.severity === "error");
}

function validateNodeConnections(
  node: WorkflowNode,
  edges: WorkflowTransition[],
  issues: WorkflowValidationIssue[],
) {
  const incoming = edges.filter((edge) => edge.target === node.id);
  const outgoing = edges.filter((edge) => edge.source === node.id);

  if (node.type === "start") {
    if (incoming.length > 0) {
      issues.push(issue("start.incoming.invalid", "error", "node", "Başlangıç düğümünün gelen bağlantısı olamaz.", node.id));
    }
    if (outgoing.length !== 1) {
      issues.push(issue("start.outgoing.count", "error", "node", "Başlangıç düğümünün tek bir çıkışı olmalıdır.", node.id));
    }
    return;
  }

  if (incoming.length === 0) {
    issues.push(issue("node.incoming.required", "error", "node", "Düğümün en az bir gelen bağlantısı olmalıdır.", node.id));
  }

  if (terminalNodeTypes.has(node.type)) {
    if (outgoing.length > 0) {
      issues.push(issue("end.outgoing.invalid", "error", "node", "Sonuç düğümünün giden bağlantısı olamaz.", node.id));
    }
    return;
  }

  if (outgoing.length === 0) {
    issues.push(issue("node.outgoing.required", "error", "node", "Düğümün en az bir giden bağlantısı olmalıdır.", node.id));
  }
}

function validateUserTask(
  data: UserTaskNodeData,
  nodeId: string,
  issues: WorkflowValidationIssue[],
) {
  if (!isAssignmentComplete(data.assignment)) {
    issues.push(issue("task.assignment.required", "error", "node", "Görev ataması tamamlanmalıdır.", nodeId));
  }

  if (data.actions.length === 0) {
    issues.push(issue("task.actions.required", "error", "node", "Görev için en az bir işlem seçilmelidir.", nodeId));
  }

  if (data.formBinding && !data.formBinding.formVersionId) {
    issues.push(issue("task.form.required", "error", "node", "Form bağlantısı için bir form sürümü seçilmelidir.", nodeId));
  }
}

function validateGateway(
  nodeId: string,
  edges: WorkflowTransition[],
  issues: WorkflowValidationIssue[],
) {
  const outgoing = edges.filter((edge) => edge.source === nodeId);
  if (outgoing.length < 2) {
    issues.push(issue("gateway.outgoing.minimum", "error", "node", "Karar düğümünün en az iki çıkışı olmalıdır.", nodeId));
    return;
  }

  const defaultEdges = outgoing.filter((edge) => edge.data?.isDefault);
  if (defaultEdges.length !== 1) {
    issues.push(issue("gateway.default.count", "error", "node", "Karar düğümünün tek bir varsayılan çıkışı olmalıdır.", nodeId));
  }

  outgoing.forEach((edge) => {
    if (edge.data?.isDefault && edge.data.condition) {
      issues.push(issue(
        "gateway.default.condition.invalid",
        "error",
        "transition",
        "Varsayılan karar çıkışında koşul bulunamaz.",
        edge.id,
      ));
    }
    if (!edge.data?.isDefault && !isConditionComplete(edge)) {
      issues.push(issue(
        "gateway.condition.required",
        "error",
        "transition",
        "Varsayılan olmayan karar çıkışında koşul tamamlanmalıdır.",
        edge.id,
      ));
    }
  });
}

function validateTransition(
  edge: WorkflowTransition,
  nodes: WorkflowNode[],
  issues: WorkflowValidationIssue[],
) {
  const source = nodes.find((node) => node.id === edge.source);
  if (source?.type !== "userTask" || !edge.data?.action) {
    return;
  }

  if (!source.data.actions.includes(edge.data.action)) {
    issues.push(issue(
      "transition.action.unavailable",
      "error",
      "transition",
      "Bağlantı işlemi kaynak görevin kullanılabilir işlemleri arasında değil.",
      edge.id,
    ));
  }
}

function validateTaskTransitions(
  node: Extract<WorkflowNode, { type: "userTask" }>,
  edges: WorkflowTransition[],
  nodes: WorkflowNode[],
  issues: WorkflowValidationIssue[],
) {
  const outgoing = edges.filter((edge) => edge.source === node.id);
  const actions = outgoing.map((edge) => edge.data?.action).filter((action) => Boolean(action));

  outgoing.forEach((edge) => {
    if (!edge.data?.action) {
      issues.push(issue(
        "transition.action.required",
        "error",
        "transition",
        "Kullanıcı görevi çıkışında bir işlem seçilmelidir.",
        edge.id,
      ));
      return;
    }
    if (edge.data.action === "SendBack") {
      const target = nodes.find((candidate) => candidate.id === edge.target);
      if (target?.type !== "userTask") {
        issues.push(issue(
          "transition.sendback.target",
          "error",
          "transition",
          "Geri gönder işlemi bir kullanıcı görevini hedeflemelidir.",
          edge.id,
        ));
      }
    }
  });

  node.data.actions.forEach((action) => {
    const count = actions.filter((candidate) => candidate === action).length;
    if (count === 0) {
      issues.push(issue(
        "task.action.route.required",
        "error",
        "node",
        `${action} işlemi için bir çıkış bağlantısı olmalıdır.`,
        node.id,
      ));
    } else if (count > 1) {
      issues.push(issue(
        "task.action.route.duplicate",
        "error",
        "node",
        `${action} işlemi yalnızca bir çıkış bağlantısında kullanılabilir.`,
        node.id,
      ));
    }
  });
}

function validateReachability(
  start: WorkflowNode | undefined,
  nodes: WorkflowNode[],
  edges: WorkflowTransition[],
  issues: WorkflowValidationIssue[],
) {
  if (!start) {
    return;
  }

  const visited = new Set<string>();
  const pending = [start.id];
  while (pending.length > 0) {
    const current = pending.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    edges.filter((edge) => edge.source === current).forEach((edge) => pending.push(edge.target));
  }

  nodes.forEach((node) => {
    if (!visited.has(node.id)) {
      issues.push(issue("node.unreachable", "error", "node", "Düğüm başlangıçtan erişilebilir değil.", node.id));
    }
  });
}

function validateCyclesAndSendBack(
  nodes: WorkflowNode[],
  edges: WorkflowTransition[],
  issues: WorkflowValidationIssue[],
) {
  const forwardEdges = edges.filter((edge) => edge.data?.action !== "SendBack");
  const visiting = new Set<string>();
  const visited = new Set<string>();

  function hasCycle(nodeId: string): boolean {
    if (visiting.has(nodeId)) return true;
    if (visited.has(nodeId)) return false;
    visiting.add(nodeId);
    const cycle = forwardEdges
      .filter((edge) => edge.source === nodeId)
      .some((edge) => hasCycle(edge.target));
    visiting.delete(nodeId);
    visited.add(nodeId);
    return cycle;
  }

  if (nodes.some((node) => hasCycle(node.id))) {
    issues.push(issue(
      "workflow.forward.cycle",
      "error",
      "workflow",
      "Otomatik akış bağlantıları döngü oluşturamaz; önceki göreve dönüş için Geri Gönder kullanılmalıdır.",
    ));
  }

  edges.filter((edge) => edge.data?.action === "SendBack").forEach((edge) => {
    if (!hasForwardPath(edge.target, edge.source, forwardEdges)) {
      issues.push(issue(
        "transition.sendback.order",
        "error",
        "transition",
        "Geri Gönder yalnızca ileri akışta daha önce bulunan bir kullanıcı görevini hedefleyebilir.",
        edge.id,
      ));
    }
  });
}

function validateGatewayConditionSources(
  edges: WorkflowTransition[],
  issues: WorkflowValidationIssue[],
) {
  const forwardEdges = edges.filter((edge) => edge.data?.action !== "SendBack");
  edges.forEach((edge) => {
    const path = edge.data?.condition?.fieldKey ?? "";
    const match = /^steps\.([^.]+)\.[^.]+$/.exec(path);
    if (match && !hasForwardPath(match[1], edge.source, forwardEdges)) {
      issues.push(issue(
        "gateway.condition.future-step",
        "error",
        "transition",
        "Karar koşulu yalnızca bu adımdan önce tamamlanan görev formlarını kullanabilir.",
        edge.id,
      ));
    }
  });
}

function hasForwardPath(source: string, target: string, edges: WorkflowTransition[]) {
  const visited = new Set<string>();
  const pending = [source];
  while (pending.length > 0) {
    const current = pending.shift();
    if (!current || visited.has(current)) continue;
    if (current === target) return true;
    visited.add(current);
    edges.filter((edge) => edge.source === current).forEach((edge) => pending.push(edge.target));
  }
  return false;
}

function isAssignmentComplete(assignment: WorkflowAssignment) {
  switch (assignment.type) {
    case "processStarter":
      return true;
    case "person":
      return Boolean(assignment.personId);
    case "team":
      return Boolean(assignment.teamId);
    case "communityRole":
      return Boolean(assignment.communityRoleId);
    case "teamAndRole":
      return Boolean(assignment.teamId && assignment.communityRoleId);
  }
}

function isConditionComplete(edge: WorkflowTransition) {
  const condition = edge.data?.condition;
  if (!condition?.fieldKey.trim()) {
    return false;
  }
  return condition.operator === "IsEmpty" || condition.operator === "IsNotEmpty" || condition.value.trim().length > 0;
}

function isFlowNode(node: WorkflowNode) {
  return flowNodeTypes.has(node.type);
}

function issue(
  code: string,
  severity: WorkflowValidationIssue["severity"],
  scope: WorkflowValidationIssue["scope"],
  message: string,
  entityId?: string,
): WorkflowValidationIssue {
  return { code, severity, scope, message, entityId };
}
