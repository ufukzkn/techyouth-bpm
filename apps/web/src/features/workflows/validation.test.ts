import { describe, expect, it } from "vitest";
import type { TeamSwimlaneNodeData, UserTaskNodeData, WorkflowNode } from "@/features/workflows/contracts";
import { createStarterWorkflowDraft, getNextWorkflowName } from "@/features/workflows/workflowDraft";
import { validateWorkflow } from "@/features/workflows/validation";

describe("validateWorkflow", () => {
  it("creates the next available starter workflow name", () => {
    expect(getNextWorkflowName([{ name: "Yeni Akış 1" }, { name: " yeni akış 2 " }])).toBe("Yeni Akış 3");
  });

  it("uses the namespaced start form path in the starter gateway", () => {
    const gatewayEdge = createStarterWorkflowDraft().edges.find((edge) => edge.data?.condition);

    expect(gatewayEdge?.data?.condition?.fieldKey).toBe("start.amount");
  });

  it("reports missing team and task assignment references in the starter draft", () => {
    const issues = validateWorkflow(createStarterWorkflowDraft());

    expect(issues.filter((item) => item.code === "swimlane.team.required")).toHaveLength(2);
    expect(issues.some((item) => item.code === "task.assignment.required")).toBe(true);
    expect(issues.some((item) => item.code === "start.form.required")).toBe(true);
  });

  it("accepts the starter graph after required assignment references are supplied", () => {
    const draft = createStarterWorkflowDraft();
    draft.nodes = draft.nodes.map((node): WorkflowNode => {
      if (node.type === "teamSwimlane") {
        return {
          ...node,
          data: { ...node.data, teamId: `team-${node.id}`, teamName: "Ekip" } satisfies TeamSwimlaneNodeData,
        };
      }
      if (node.type === "userTask") {
        return {
          ...node,
          data: {
            ...node.data,
            assignment: { type: "team", teamId: "team-review", teamName: "İnceleme" },
          } satisfies UserTaskNodeData,
        };
      }
      if (node.type === "start") {
        return {
          ...node,
          data: {
            ...node.data,
            formBinding: {
              formVersionId: "form-version-1",
              formName: "Talep formu",
              version: 1,
              mode: "Required",
            },
          },
        };
      }
      return node;
    });

    expect(validateWorkflow(draft)).toEqual([]);
  });

  it("requires a typed condition and one default branch on a gateway", () => {
    const draft = createStarterWorkflowDraft();
    draft.edges = draft.edges.map((edge) => edge.source === "node-decision"
      ? { ...edge, data: { ...edge.data, isDefault: false, condition: null } }
      : edge);

    const issues = validateWorkflow(draft);

    expect(issues.some((item) => item.code === "gateway.default.count")).toBe(true);
    expect(issues.filter((item) => item.code === "gateway.condition.required")).toHaveLength(2);
  });

  it("marks detached flow nodes as unreachable", () => {
    const draft = createStarterWorkflowDraft();
    draft.edges = draft.edges.filter((edge) => edge.target !== "node-rejected");

    const issues = validateWorkflow(draft);

    expect(issues.some((item) => item.code === "node.unreachable" && item.entityId === "node-rejected")).toBe(true);
  });

  it("rejects automatic cycles and forward send-back targets before publish", () => {
    const draft = createStarterWorkflowDraft();
    const reviewToGateway = draft.edges.find((edge) => edge.id === "transition-review-gateway")!;
    draft.edges.push({
      ...reviewToGateway,
      id: "transition-gateway-review-cycle",
      source: "node-decision",
      target: "node-review",
      data: { ...reviewToGateway.data, action: null, condition: null, isDefault: false },
    });

    const issues = validateWorkflow(draft);

    expect(issues.some((item) => item.code === "workflow.forward.cycle")).toBe(true);
  });

  it("rejects a gateway condition that reads a future task output", () => {
    const draft = createStarterWorkflowDraft();
    const conditionEdge = draft.edges.find((edge) => edge.id === "transition-gateway-complete")!;
    conditionEdge.data.condition = {
      fieldKey: "steps.node-future.result",
      operator: "Equals",
      valueType: "String",
      value: "ok",
    };

    const issues = validateWorkflow(draft);

    expect(issues.some((item) => item.code === "gateway.condition.future-step")).toBe(true);
  });

  it.each([0, 525601])("rejects task SLA outside the supported range: %s", (slaDurationMinutes) => {
    const draft = createStarterWorkflowDraft();
    draft.nodes = draft.nodes.map((node): WorkflowNode => node.type === "userTask"
      ? {
          ...node,
          data: {
            ...node.data,
            slaDurationMinutes,
          } satisfies UserTaskNodeData,
        }
      : node);

    const issues = validateWorkflow(draft);

    expect(issues.some((item) => item.code === "task.sla.range")).toBe(true);
  });

  it("rejects a team-lead restriction on a non-team assignment", () => {
    const draft = createStarterWorkflowDraft();
    draft.nodes = draft.nodes.map((node): WorkflowNode => node.type === "userTask"
      ? {
          ...node,
          data: {
            ...node.data,
            assignment: { type: "processStarter" },
            requiresTeamLead: true,
          } satisfies UserTaskNodeData,
        }
      : node);

    const issues = validateWorkflow(draft);

    expect(issues.some((item) => item.code === "task.teamLead.assignment")).toBe(true);
  });
});
