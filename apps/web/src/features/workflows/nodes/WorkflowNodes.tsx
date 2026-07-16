import {
  Check,
  CircleCheck,
  CircleX,
  ClipboardCheck,
  Flag,
  GitFork,
  Play,
  Rows3,
  UserRoundCheck,
} from "lucide-react";
import { Handle, Position, type NodeProps, type NodeTypes } from "@xyflow/react";
import type {
  CompletedEndNode,
  ExclusiveGatewayNode,
  RejectedEndNode,
  StartNode,
  TeamSwimlaneNode,
  UserTaskNode,
  WorkflowAssignment,
} from "@/features/workflows/contracts";
import { workflowPriorityLabels } from "@/features/workflows/workflowLabels";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";

export function StartNodeView({ data, isConnectable }: NodeProps<StartNode>) {
  return (
    <div className="workflow-node-card workflow-node-card-start">
      <NodeHeader icon={<Play size={17} />} kicker="Başlangıç" title={data.label} />
      <NodeMeta icon={<Flag size={14} />} text={data.formBinding?.formName || "Başlangıç formu yok"} />
      <Handle className="workflow-handle" id="out" isConnectable={isConnectable} position={Position.Right} type="source" />
    </div>
  );
}

export function UserTaskNodeView({ data, isConnectable }: NodeProps<UserTaskNode>) {
  return (
    <div className="workflow-node-card workflow-node-card-task">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <NodeHeader icon={<ClipboardCheck size={17} />} kicker="Kullanıcı görevi" title={data.label} />
      <div className="workflow-node-task-meta">
        <NodeMeta icon={<UserRoundCheck size={14} />} text={assignmentSummary(data.assignment)} />
        <span className={`workflow-priority-dot workflow-priority-dot-${data.priority.toLowerCase()}`} aria-hidden="true" />
        <span>{workflowPriorityLabels[data.priority]}</span>
      </div>
      <div className="workflow-node-action-count">
        <Check size={13} aria-hidden="true" />
        <span>{data.actions.length} işlem</span>
      </div>
      <Handle className="workflow-handle" id="out" isConnectable={isConnectable} position={Position.Right} type="source" />
    </div>
  );
}

export function ExclusiveGatewayNodeView({ id, data, isConnectable }: NodeProps<ExclusiveGatewayNode>) {
  const branchCount = useWorkflowDraftStore((state) => state.draft.edges.filter((edge) => edge.source === id).length);
  return (
    <div className="workflow-gateway-node">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <div className="workflow-gateway-diamond" aria-hidden="true">
        <GitFork size={22} />
      </div>
      <div className="workflow-gateway-copy">
        <span>Karar</span>
        <strong>{data.label}</strong>
        <small>{branchCount} çıkış</small>
      </div>
      <Handle className="workflow-handle" id="out" isConnectable={isConnectable} position={Position.Right} type="source" />
    </div>
  );
}

export function CompletedEndNodeView({ data, isConnectable }: NodeProps<CompletedEndNode>) {
  return (
    <div className="workflow-node-card workflow-node-card-completed">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <NodeHeader icon={<CircleCheck size={17} />} kicker="Sonuç" title={data.label} />
    </div>
  );
}

export function RejectedEndNodeView({ data, isConnectable }: NodeProps<RejectedEndNode>) {
  return (
    <div className="workflow-node-card workflow-node-card-rejected">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <NodeHeader icon={<CircleX size={17} />} kicker="Sonuç" title={data.label} />
    </div>
  );
}

export function TeamSwimlaneNodeView({ data }: NodeProps<TeamSwimlaneNode>) {
  return (
    <div className="workflow-swimlane-node">
      <div className="workflow-swimlane-heading">
        <Rows3 size={17} aria-hidden="true" />
        <span>
          <strong>{data.label}</strong>
          <small>{data.teamName || "Takım seçilmedi"}</small>
        </span>
      </div>
    </div>
  );
}

export const workflowNodeTypes = {
  start: StartNodeView,
  userTask: UserTaskNodeView,
  exclusiveGateway: ExclusiveGatewayNodeView,
  completedEnd: CompletedEndNodeView,
  rejectedEnd: RejectedEndNodeView,
  teamSwimlane: TeamSwimlaneNodeView,
} satisfies NodeTypes;

function NodeHeader({ icon, kicker, title }: { icon: React.ReactNode; kicker: string; title: string }) {
  return (
    <div className="workflow-node-heading">
      <span className="workflow-node-icon" aria-hidden="true">{icon}</span>
      <span>
        <small>{kicker}</small>
        <strong>{title || "Adsız düğüm"}</strong>
      </span>
    </div>
  );
}

function NodeMeta({ icon, text }: { icon: React.ReactNode; text: string }) {
  return (
    <div className="workflow-node-meta">
      {icon}
      <span>{text}</span>
    </div>
  );
}

function assignmentSummary(assignment: WorkflowAssignment) {
  switch (assignment.type) {
    case "processStarter":
      return "Süreci başlatan";
    case "person":
      return assignment.personName || "Kullanıcı seçilmedi";
    case "team":
      return assignment.teamName || "Takım seçilmedi";
    case "communityRole":
      return assignment.communityRoleName || "Rol seçilmedi";
    case "teamAndRole":
      return [assignment.teamName, assignment.communityRoleName].filter(Boolean).join(" · ") || "Takım ve rol seçilmedi";
  }
}
