"use client";

import {
  applyEdgeChanges,
  applyNodeChanges,
  type Connection,
  type EdgeChange,
  type NodeChange,
  type XYPosition,
} from "@xyflow/react";
import { create } from "zustand";
import type {
  WorkflowDefinitionDraft,
  WorkflowNode,
  WorkflowNodeData,
  WorkflowNodeKind,
  TeamSwimlaneNode,
  WorkflowTransition,
  WorkflowTransitionData,
} from "@/features/workflows/contracts";
import {
  cloneWorkflowDraft,
  createStarterWorkflowDraft,
  createTransition,
  createWorkflowNode,
  orderWorkflowNodes,
  workflowLaneHeight,
  workflowLaneWidth,
} from "@/features/workflows/workflowDraft";

type WorkflowNodePlacement = {
  position?: XYPosition;
  parentId?: string;
};

type WorkflowDraftState = {
  draft: WorkflowDefinitionDraft;
  selectedNodeId: string | null;
  selectedEdgeId: string | null;
  isDirty: boolean;
  hydrate: (draft: WorkflowDefinitionDraft) => void;
  reset: () => void;
  setMetadata: (patch: Pick<Partial<WorkflowDefinitionDraft>, "name" | "description">) => void;
  applyNodeChanges: (changes: NodeChange<WorkflowNode>[]) => void;
  applyEdgeChanges: (changes: EdgeChange<WorkflowTransition>[]) => void;
  addNode: (kind: WorkflowNodeKind, placement?: WorkflowNodePlacement) => string | null;
  connect: (connection: Connection) => void;
  updateNodeData: (nodeId: string, updater: (data: WorkflowNodeData) => WorkflowNodeData) => void;
  updateEdgeData: (edgeId: string, patch: Partial<WorkflowTransitionData>) => void;
  selectNode: (nodeId: string | null) => void;
  selectEdge: (edgeId: string | null) => void;
  syncSelection: (nodeId: string | null, edgeId: string | null) => void;
  deleteSelection: () => void;
  markSaved: (draft?: WorkflowDefinitionDraft) => void;
  markPublished: (draft?: WorkflowDefinitionDraft) => void;
};

let fallbackId = 0;

export const useWorkflowDraftStore = create<WorkflowDraftState>((set, get) => ({
  draft: createStarterWorkflowDraft(),
  selectedNodeId: null,
  selectedEdgeId: null,
  isDirty: false,

  hydrate: (draft) => set({
    draft: cloneWorkflowDraft(draft),
    selectedNodeId: null,
    selectedEdgeId: null,
    isDirty: false,
  }),

  reset: () => set({
    draft: createStarterWorkflowDraft(),
    selectedNodeId: null,
    selectedEdgeId: null,
    isDirty: false,
  }),

  setMetadata: (patch) => set((state) => ({
    draft: { ...state.draft, ...patch },
    isDirty: true,
  })),

  applyNodeChanges: (changes) => set((state) => {
    const nodes = orderWorkflowNodes(applyNodeChanges(changes, state.draft.nodes));
    const nodeIds = new Set(nodes.map((node) => node.id));
    const edges = state.draft.edges.filter((edge) => nodeIds.has(edge.source) && nodeIds.has(edge.target));
    const changesDraft = changes.some((change) => change.type !== "select" && change.type !== "dimensions");
    return {
      draft: { ...state.draft, nodes, edges },
      selectedNodeId: state.selectedNodeId && nodeIds.has(state.selectedNodeId) ? state.selectedNodeId : null,
      selectedEdgeId: edges.some((edge) => edge.id === state.selectedEdgeId) ? state.selectedEdgeId : null,
      isDirty: state.isDirty || changesDraft,
    };
  }),

  applyEdgeChanges: (changes) => set((state) => {
    const edges = applyEdgeChanges(changes, state.draft.edges);
    const changesDraft = changes.some((change) => change.type !== "select");
    return {
      draft: { ...state.draft, edges },
      selectedEdgeId: edges.some((edge) => edge.id === state.selectedEdgeId) ? state.selectedEdgeId : null,
      isDirty: state.isDirty || changesDraft,
    };
  }),

  addNode: (kind, placement = {}) => {
    const state = get();
    if (kind === "start" && state.draft.nodes.some((node) => node.type === "start")) {
      return null;
    }

    const id = createEntityId(kind);
    const resolved = resolveNodePlacement(kind, state.draft.nodes, placement);
    let node = createWorkflowNode(kind, id, resolved.position, resolved.parentId);
    if (node.type === "userTask" && resolved.parentId) {
      const lane = state.draft.nodes.find(
        (candidate): candidate is TeamSwimlaneNode => candidate.id === resolved.parentId && candidate.type === "teamSwimlane",
      );
      if (lane?.data.teamId) {
        node = {
          ...node,
          data: {
            ...node.data,
            assignment: {
              type: "team",
              teamId: lane.data.teamId,
              teamName: lane.data.teamName,
            },
          },
        };
      }
    }
    set({
      draft: { ...state.draft, nodes: orderWorkflowNodes([...state.draft.nodes, node]) },
      selectedNodeId: id,
      selectedEdgeId: null,
      isDirty: true,
    });
    get().selectNode(id);
    return id;
  },

  connect: (connection) => {
    if (!connection.source || !connection.target || connection.source === connection.target) {
      return;
    }
    const state = get();
    const source = state.draft.nodes.find((node) => node.id === connection.source);
    const target = state.draft.nodes.find((node) => node.id === connection.target);
    if (!source || !target || source.type === "teamSwimlane" || target.type === "teamSwimlane") {
      return;
    }
    if (state.draft.edges.some((edge) => edge.source === connection.source && edge.target === connection.target)) {
      return;
    }

    const edge = createTransition(createEntityId("transition"), connection.source, connection.target);
    edge.sourceHandle = connection.sourceHandle;
    edge.targetHandle = connection.targetHandle;
    edge.selected = true;
    set({
      draft: {
        ...state.draft,
        nodes: state.draft.nodes.map((node) => ({ ...node, selected: false } as WorkflowNode)),
        edges: [...state.draft.edges.map((candidate) => ({ ...candidate, selected: false })), edge],
      },
      selectedNodeId: null,
      selectedEdgeId: edge.id,
      isDirty: true,
    });
  },

  updateNodeData: (nodeId, updater) => set((state) => ({
    draft: {
      ...state.draft,
      nodes: state.draft.nodes.map((node) => {
        if (node.id !== nodeId) {
          return node;
        }
        return { ...node, data: updater(node.data) } as WorkflowNode;
      }),
    },
    isDirty: true,
  })),

  updateEdgeData: (edgeId, patch) => set((state) => ({
    draft: {
      ...state.draft,
      edges: state.draft.edges.map((edge) => edge.id === edgeId
        ? { ...edge, data: { ...edge.data, ...patch } }
        : edge),
    },
    isDirty: true,
  })),

  selectNode: (nodeId) => set((state) => ({
    draft: {
      ...state.draft,
      nodes: state.draft.nodes.map((node) => ({ ...node, selected: node.id === nodeId } as WorkflowNode)),
      edges: state.draft.edges.map((edge) => ({ ...edge, selected: false })),
    },
    selectedNodeId: nodeId,
    selectedEdgeId: null,
  })),

  selectEdge: (edgeId) => set((state) => ({
    draft: {
      ...state.draft,
      nodes: state.draft.nodes.map((node) => ({ ...node, selected: false } as WorkflowNode)),
      edges: state.draft.edges.map((edge) => ({ ...edge, selected: edge.id === edgeId })),
    },
    selectedNodeId: null,
    selectedEdgeId: edgeId,
  })),

  syncSelection: (nodeId, edgeId) => set({
    selectedNodeId: nodeId,
    selectedEdgeId: nodeId ? null : edgeId,
  }),

  deleteSelection: () => set((state) => {
    const selectedNodeId = state.selectedNodeId;
    const selectedEdgeId = state.selectedEdgeId;
    if (!selectedNodeId && !selectedEdgeId) {
      return state;
    }

    const removedNodeIds = new Set<string>();
    if (selectedNodeId) {
      removedNodeIds.add(selectedNodeId);
      const selected = state.draft.nodes.find((node) => node.id === selectedNodeId);
      if (selected?.type === "teamSwimlane") {
        state.draft.nodes.filter((node) => node.parentId === selectedNodeId).forEach((node) => removedNodeIds.add(node.id));
      }
    }

    const nodes = state.draft.nodes.filter((node) => !removedNodeIds.has(node.id));
    const edges = state.draft.edges.filter((edge) =>
      edge.id !== selectedEdgeId && !removedNodeIds.has(edge.source) && !removedNodeIds.has(edge.target));
    return {
      draft: { ...state.draft, nodes, edges },
      selectedNodeId: null,
      selectedEdgeId: null,
      isDirty: true,
    };
  }),

  markSaved: (draft) => set((state) => ({
    draft: clearDraftSelection(draft ? cloneWorkflowDraft(draft) : state.draft),
    selectedNodeId: null,
    selectedEdgeId: null,
    isDirty: false,
  })),

  markPublished: (draft) => set((state) => ({
    draft: clearDraftSelection(draft
      ? cloneWorkflowDraft(draft)
      : { ...state.draft, status: "Published", publishedAt: new Date().toISOString() }),
    selectedNodeId: null,
    selectedEdgeId: null,
    isDirty: false,
  })),
}));

function resolveNodePlacement(
  kind: WorkflowNodeKind,
  nodes: WorkflowNode[],
  placement: WorkflowNodePlacement,
): Required<Pick<WorkflowNodePlacement, "position">> & Pick<WorkflowNodePlacement, "parentId"> {
  if (kind === "teamSwimlane") {
    const laneCount = nodes.filter((node) => node.type === "teamSwimlane").length;
    return { position: placement.position ?? { x: 20, y: 20 + laneCount * (workflowLaneHeight + 40) } };
  }

  if (placement.parentId) {
    return { position: placement.position ?? { x: 48, y: 68 }, parentId: placement.parentId };
  }

  if (placement.position) {
    const lane = nodes.find((node) => node.type === "teamSwimlane" && pointInsideLane(placement.position!, node));
    if (lane) {
      return {
        parentId: lane.id,
        position: {
          x: clamp(placement.position.x - lane.position.x, 32, workflowLaneWidth - 220),
          y: clamp(placement.position.y - lane.position.y, 48, workflowLaneHeight - 100),
        },
      };
    }
    return { position: placement.position };
  }

  const firstLane = nodes.find((node) => node.type === "teamSwimlane");
  if (firstLane) {
    const childCount = nodes.filter((node) => node.parentId === firstLane.id).length;
    return {
      parentId: firstLane.id,
      position: {
        x: 52 + (childCount % 4) * 220,
        y: 58 + Math.floor(childCount / 4) * 98,
      },
    };
  }

  const rootFlowNodeCount = nodes.filter((node) => node.type !== "teamSwimlane" && !node.parentId).length;
  return { position: { x: 80 + (rootFlowNodeCount % 3) * 240, y: 80 + Math.floor(rootFlowNodeCount / 3) * 150 } };
}

function pointInsideLane(point: XYPosition, lane: WorkflowNode) {
  return point.x >= lane.position.x
    && point.x <= lane.position.x + (lane.width ?? workflowLaneWidth)
    && point.y >= lane.position.y
    && point.y <= lane.position.y + (lane.height ?? workflowLaneHeight);
}

function createEntityId(prefix: WorkflowNodeKind | "transition") {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  fallbackId += 1;
  return `${prefix}-${Date.now()}-${fallbackId}`;
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(Math.max(value, minimum), maximum);
}

function clearDraftSelection(draft: WorkflowDefinitionDraft): WorkflowDefinitionDraft {
  return {
    ...draft,
    nodes: draft.nodes.map((node) => ({ ...node, selected: false } as WorkflowNode)),
    edges: draft.edges.map((edge) => ({ ...edge, selected: false })),
  };
}
