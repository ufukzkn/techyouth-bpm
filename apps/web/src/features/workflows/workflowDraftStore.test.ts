import { beforeEach, describe, expect, it } from "vitest";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";

describe("workflowDraftStore", () => {
  beforeEach(() => {
    useWorkflowDraftStore.getState().reset();
  });

  it("prevents a second start node", () => {
    const id = useWorkflowDraftStore.getState().addNode("start");

    expect(id).toBeNull();
    expect(useWorkflowDraftStore.getState().draft.nodes.filter((node) => node.type === "start")).toHaveLength(1);
  });

  it("adds a task to the first lane and marks the draft dirty", () => {
    const id = useWorkflowDraftStore.getState().addNode("userTask");
    const state = useWorkflowDraftStore.getState();
    const node = state.draft.nodes.find((item) => item.id === id);

    expect(node?.parentId).toBe("lane-request");
    expect(state.selectedNodeId).toBe(id);
    expect(state.isDirty).toBe(true);
  });

  it("deletes lane children and their transitions with the lane", () => {
    useWorkflowDraftStore.getState().selectNode("lane-review");
    useWorkflowDraftStore.getState().deleteSelection();
    const state = useWorkflowDraftStore.getState();

    expect(state.draft.nodes.some((node) => node.id === "lane-review" || node.parentId === "lane-review")).toBe(false);
    expect(state.draft.edges.some((edge) => edge.source === "node-review" || edge.target === "node-review")).toBe(false);
  });

  it("clears dirty state after a successful save", () => {
    useWorkflowDraftStore.getState().setMetadata({ name: "Yeni ad" });
    useWorkflowDraftStore.getState().selectNode("node-review");
    useWorkflowDraftStore.getState().markSaved();

    expect(useWorkflowDraftStore.getState().draft.name).toBe("Yeni ad");
    expect(useWorkflowDraftStore.getState().isDirty).toBe(false);
    expect(useWorkflowDraftStore.getState().selectedNodeId).toBeNull();
    expect(useWorkflowDraftStore.getState().draft.nodes.some((node) => node.selected)).toBe(false);
  });
});
