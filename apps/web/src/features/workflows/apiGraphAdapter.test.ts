import { describe, expect, it } from "vitest";
import type { ApiProcessGraph, UserTaskNodeData } from "@/features/workflows/contracts";
import { createWorkflowWriteModel, fromApiProcessGraph, toApiProcessGraph } from "@/features/workflows/apiGraphAdapter";
import { createStarterWorkflowDraft } from "@/features/workflows/workflowDraft";

describe("workflow API graph adapter", () => {
  it("maps stable keys, geometry, parent keys, assignments, actions, and ordered edges", () => {
    const draft = createStarterWorkflowDraft();
    const task = draft.nodes.find((node) => node.type === "userTask");
    if (!task || task.type !== "userTask") {
      throw new Error("Starter task missing");
    }
    task.data = {
      ...task.data,
      assignment: { type: "processStarter" },
      actions: [...task.data.actions, "SendBack"],
      slaDurationMinutes: 1440,
      slaUnit: "days",
      requiresTeamLead: false,
    } satisfies UserTaskNodeData;

    const graph = toApiProcessGraph(draft);
    const apiTask = graph.nodes.find((node) => node.key === task.id);

    expect(apiTask).toMatchObject({
      key: "node-review",
      type: "UserTask",
      title: "Talebi incele",
      description: null,
      assignment: { type: "ProcessStarter" },
      parentKey: "lane-review",
      positionX: 150,
      positionY: 66,
      width: 230,
      height: 112,
      slaDurationMinutes: 1440,
      requiresTeamLead: false,
    });
    expect(apiTask?.actions).toContain("SendBack");
    const apiLane = graph.nodes.find((node) => node.key === "lane-review");
    expect(apiLane).toMatchObject({ teamId: null });
    expect(apiLane && "assignment" in apiLane).toBe(false);
    expect(graph.edges).toHaveLength(draft.edges.length);
    expect(graph.edges.map((edge) => edge.order)).toEqual([0, 1, 2, 3, 4]);
  });

  it("restores XYFlow nodes from the API contract", () => {
    const graph: ApiProcessGraph = {
      schemaVersion: "1.0",
      nodes: [{
        key: "review",
        type: "UserTask",
        title: "İncele",
        description: "Bütçe ve belgeleri kontrol eder.",
        priority: "Critical",
        actions: ["Approve", "SendBack"],
        assignment: { type: "SpecificUser", userId: "user-1" },
        parentKey: "finance",
        positionX: 42,
        positionY: 54,
        width: 240,
        height: 120,
        slaDurationMinutes: 720,
        requiresTeamLead: true,
      }, {
        key: "finance",
        type: "TeamSwimlane",
        title: "Finans",
        priority: "Normal",
        teamId: "team-1",
        positionX: 10,
        positionY: 20,
        width: 900,
        height: 220,
      }],
      edges: [],
    };

    const draft = fromApiProcessGraph(graph, { name: "Akış", description: "" });
    const task = draft.nodes.find((node) => node.id === "review");

    expect(draft.nodes[0].type).toBe("teamSwimlane");
    expect(task).toMatchObject({
      type: "userTask",
      parentId: "finance",
      position: { x: 42, y: 54 },
      width: 240,
      height: 120,
    });
    expect(task?.data).toMatchObject({
      label: "İncele",
      description: "Bütçe ve belgeleri kontrol eder.",
      priority: "Critical",
      actions: ["Approve", "SendBack"],
      assignment: { type: "person", personId: "user-1" },
      slaDurationMinutes: 720,
      requiresTeamLead: true,
    });
  });

  it("derives the version-level form id from the Start binding", () => {
    const draft = createStarterWorkflowDraft();
    const start = draft.nodes.find((node) => node.type === "start");
    if (!start || start.type !== "start") {
      throw new Error("Starter node missing");
    }
    start.data.formBinding = {
      formVersionId: "form-version-1",
      formName: "Talep formu",
      version: 3,
      mode: "Required",
    };

    expect(createWorkflowWriteModel(draft).formDefinitionVersionId).toBe("form-version-1");
  });

  it("preserves descriptions, edge labels, and explicit swimlane teams on reopen", () => {
    const draft = createStarterWorkflowDraft();
    const lane = draft.nodes.find((node) => node.type === "teamSwimlane");
    const task = draft.nodes.find((node) => node.type === "userTask");
    if (!lane || lane.type !== "teamSwimlane" || !task || task.type !== "userTask") {
      throw new Error("Starter nodes missing");
    }
    lane.data.teamId = "team-finance";
    lane.data.description = "Finans operasyon kulvarı";
    task.data.description = "Talebin bütçe etkisini inceler.";
    draft.edges[0].data.label = "İncelemeye gönder";

    const reopened = fromApiProcessGraph(toApiProcessGraph(draft), { name: draft.name, description: draft.description });
    const reopenedLane = reopened.nodes.find((node) => node.id === lane.id);
    const reopenedTask = reopened.nodes.find((node) => node.id === task.id);

    expect(reopenedLane?.data).toMatchObject({
      description: "Finans operasyon kulvarı",
      teamId: "team-finance",
    });
    expect(reopenedTask?.data.description).toBe("Talebin bütçe etkisini inceler.");
    expect(reopened.edges[0].data.label).toBe("İncelemeye gönder");
  });
});
