import {
  Background,
  BackgroundVariant,
  Controls,
  MarkerType,
  MiniMap,
  ReactFlow,
  useReactFlow,
  type Connection,
} from "@xyflow/react";
import type { DragEvent, KeyboardEvent } from "react";
import { useEffect, useMemo } from "react";
import type {
  WorkflowNode,
  WorkflowNodeKind,
  WorkflowTransition,
  WorkflowValidationIssue,
} from "@/features/workflows/contracts";
import { workflowActionLabels } from "@/features/workflows/workflowLabels";
import { workflowNodeTypes } from "@/features/workflows/nodes/WorkflowNodes";
import { workflowPaletteMime } from "@/features/workflows/WorkflowPalette";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";

type WorkflowCanvasProps = {
  fitViewKey?: string;
  issues: WorkflowValidationIssue[];
  readOnly: boolean;
};

export function WorkflowCanvas({ fitViewKey, issues, readOnly }: WorkflowCanvasProps) {
  const draft = useWorkflowDraftStore((state) => state.draft);
  const applyNodeChanges = useWorkflowDraftStore((state) => state.applyNodeChanges);
  const applyEdgeChanges = useWorkflowDraftStore((state) => state.applyEdgeChanges);
  const addNode = useWorkflowDraftStore((state) => state.addNode);
  const connect = useWorkflowDraftStore((state) => state.connect);
  const syncSelection = useWorkflowDraftStore((state) => state.syncSelection);
  const deleteSelection = useWorkflowDraftStore((state) => state.deleteSelection);
  const { fitView, screenToFlowPosition } = useReactFlow<WorkflowNode, WorkflowTransition>();
  const invalidNodeIds = useMemo(
    () => new Set(issues.filter((issue) => issue.severity === "error" && issue.scope === "node").map((issue) => issue.entityId)),
    [issues],
  );
  const invalidEdgeIds = useMemo(
    () => new Set(issues.filter((issue) => issue.severity === "error" && issue.scope === "transition").map((issue) => issue.entityId)),
    [issues],
  );
  const nodes = useMemo(() => draft.nodes.map((node) => ({
    ...node,
    className: [node.className, invalidNodeIds.has(node.id) ? "workflow-flow-node-invalid" : ""].filter(Boolean).join(" "),
    connectable: !readOnly && node.type !== "teamSwimlane",
    deletable: false,
    draggable: !readOnly,
  } as WorkflowNode)), [draft.nodes, invalidNodeIds, readOnly]);
  const edges = useMemo(() => draft.edges.map((edge) => ({
    ...edge,
    className: [edge.className, invalidEdgeIds.has(edge.id) ? "workflow-flow-edge-invalid" : ""].filter(Boolean).join(" "),
    deletable: false,
    label: transitionLabel(edge),
  })), [draft.edges, invalidEdgeIds]);

  useEffect(() => {
    let secondFrame = 0;
    const firstFrame = window.requestAnimationFrame(() => {
      secondFrame = window.requestAnimationFrame(() => {
        void fitView({ padding: 0.12, maxZoom: 1.05, duration: 220 });
      });
    });
    return () => {
      window.cancelAnimationFrame(firstFrame);
      if (secondFrame) window.cancelAnimationFrame(secondFrame);
    };
  }, [fitView, fitViewKey]);

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    if (readOnly) {
      return;
    }
    const kind = event.dataTransfer.getData(workflowPaletteMime);
    if (!isWorkflowNodeKind(kind)) {
      return;
    }
    addNode(kind, { position: screenToFlowPosition({ x: event.clientX, y: event.clientY }) });
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (readOnly || (event.key !== "Delete" && event.key !== "Backspace") || isTextEntry(event.target)) {
      return;
    }
    event.preventDefault();
    deleteSelection();
  }

  function isValidConnection(connection: Connection | WorkflowTransition) {
    const source = draft.nodes.find((node) => node.id === connection.source);
    const target = draft.nodes.find((node) => node.id === connection.target);
    return Boolean(
      source
      && target
      && source.id !== target.id
      && source.type !== "teamSwimlane"
      && target.type !== "teamSwimlane"
      && source.type !== "completedEnd"
      && source.type !== "rejectedEnd"
      && target.type !== "start",
    );
  }

  return (
    <main
      className={`workflow-canvas${readOnly ? " workflow-canvas-readonly" : ""}`}
      onDragOver={(event) => {
        if (!readOnly) {
          event.preventDefault();
          event.dataTransfer.dropEffect = "copy";
        }
      }}
      onDrop={handleDrop}
      onKeyDown={handleKeyDown}
    >
      <ReactFlow<WorkflowNode, WorkflowTransition>
        colorMode="system"
        defaultEdgeOptions={{
          type: "smoothstep",
          markerEnd: { type: MarkerType.ArrowClosed },
          style: { strokeWidth: 1.6 },
        }}
        deleteKeyCode={null}
        edges={edges}
        edgesFocusable
        elementsSelectable
        fitView
        fitViewOptions={{ padding: 0.12, maxZoom: 1.05 }}
        isValidConnection={isValidConnection}
        maxZoom={1.7}
        minZoom={0.35}
        nodeTypes={workflowNodeTypes}
        nodes={nodes}
        nodesConnectable={!readOnly}
        nodesDraggable={!readOnly}
        nodesFocusable
        onConnect={readOnly ? undefined : connect}
        onEdgesChange={applyEdgeChanges}
        onNodesChange={applyNodeChanges}
        onSelectionChange={({ nodes: selectedNodes, edges: selectedEdges }) => {
          syncSelection(selectedNodes[0]?.id ?? null, selectedEdges[0]?.id ?? null);
        }}
        panOnScroll
        proOptions={{ hideAttribution: true }}
        selectionOnDrag={!readOnly}
      >
        <Background color="var(--workflow-canvas-dot)" gap={20} size={1.2} variant={BackgroundVariant.Dots} />
        <Controls position="bottom-left" showInteractive={false} />
        <MiniMap
          ariaLabel="Akış mini haritası"
          maskColor="var(--workflow-minimap-mask)"
          nodeColor={(node) => minimapNodeColor(node.type)}
          pannable
          position="bottom-right"
          zoomable
        />
      </ReactFlow>
    </main>
  );
}

function transitionLabel(edge: WorkflowTransition) {
  if (edge.data?.action) {
    return workflowActionLabels[edge.data.action];
  }
  if (edge.data?.isDefault) {
    return "Varsayılan";
  }
  if (edge.data?.condition?.fieldKey) {
    return edge.data.condition.fieldKey;
  }
  return undefined;
}

function isWorkflowNodeKind(value: string): value is WorkflowNodeKind {
  return ["start", "userTask", "exclusiveGateway", "completedEnd", "rejectedEnd", "teamSwimlane"].includes(value);
}

function isTextEntry(target: EventTarget) {
  return target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement;
}

function minimapNodeColor(type: string | undefined) {
  switch (type) {
    case "start":
      return "#2c8c82";
    case "userTask":
      return "#0d6efd";
    case "exclusiveGateway":
      return "#d97706";
    case "completedEnd":
      return "#238636";
    case "rejectedEnd":
      return "#b42318";
    default:
      return "#8795a8";
  }
}
