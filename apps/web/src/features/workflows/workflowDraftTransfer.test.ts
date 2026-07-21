import { describe, expect, it } from "vitest";
import { createStarterWorkflowDraft } from "@/features/workflows/workflowDraft";
import {
  parseWorkflowDraft,
  serializeWorkflowDraft,
  workflowDraftSchema,
} from "@/features/workflows/workflowDraftTransfer";

describe("workflow draft transfer", () => {
  it("preserves graph topology and clears environment-specific bindings", () => {
    const draft = createStarterWorkflowDraft("Transfer Akışı");
    const lane = draft.nodes.find((node) => node.type === "teamSwimlane");
    const start = draft.nodes.find((node) => node.type === "start");
    const task = draft.nodes.find((node) => node.type === "userTask");
    if (!lane || lane.type !== "teamSwimlane" || !start || start.type !== "start" || !task || task.type !== "userTask") {
      throw new Error("Starter workflow is incomplete.");
    }
    lane.data.teamId = "team-1";
    lane.data.teamName = "Mali İşler";
    start.data.formBinding = { formVersionId: "form-1", formName: "Talep", version: 2, mode: "Required" };
    task.data.assignment = { type: "team", teamId: "team-1", teamName: "Mali İşler" };
    task.data.formBinding = { formVersionId: "form-2", formName: "Onay", version: 1, mode: "Required" };

    const imported = parseWorkflowDraft(serializeWorkflowDraft(draft, "2026-07-20T12:00:00.000Z"));
    const importedLane = imported.nodes.find((node) => node.id === lane.id);
    const importedStart = imported.nodes.find((node) => node.id === start.id);
    const importedTask = imported.nodes.find((node) => node.id === task.id);

    expect(imported.edges.map((edge) => [edge.source, edge.target])).toEqual(draft.edges.map((edge) => [edge.source, edge.target]));
    expect(importedLane?.type === "teamSwimlane" ? importedLane.data.teamId : "wrong").toBe("");
    expect(importedStart?.type === "start" ? importedStart.data.formBinding : "wrong").toBeNull();
    expect(importedTask?.type === "userTask" ? importedTask.data.assignment : "wrong").toMatchObject({ type: "team", teamId: "" });
    expect(importedTask?.type === "userTask" ? importedTask.data.formBinding : "wrong").toBeNull();
    expect(imported.status).toBe("Draft");
    expect(imported.requiresBinding).toContain(`node:${task.id}:assignment`);
  });

  it("rejects unknown schemas and dangling graph edges", () => {
    expect(() => parseWorkflowDraft(JSON.stringify({
      schema: `${workflowDraftSchema}.future`,
      schemaVersion: 1,
      draft: createStarterWorkflowDraft(),
    }))).toThrow("DRAFT_SCHEMA_UNSUPPORTED");

    const draft = createStarterWorkflowDraft();
    draft.edges[0].target = "missing-node";
    expect(() => parseWorkflowDraft(serializeWorkflowDraft(draft))).toThrow("DRAFT_EDGE_INVALID");
  });
});
