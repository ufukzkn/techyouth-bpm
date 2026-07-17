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
import { useSessionStore } from "@/features/session/sessionStore";
import { getWorkflowPriorityLabels } from "@/features/workflows/workflowLabels";
import { workflowText } from "@/features/workflows/workflowI18n";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";
import type { Language } from "@/lib/types";

export function StartNodeView({ data, isConnectable }: NodeProps<StartNode>) {
  const language = useSessionStore((state) => state.language);
  return (
    <div className="workflow-node-card workflow-node-card-start">
      <NodeHeader icon={<Play size={17} />} kicker={workflowText(language, "Başlangıç", "Start")} language={language} title={data.label} />
      <NodeMeta icon={<Flag size={14} />} text={data.formBinding?.formName || workflowText(language, "Başlangıç formu yok", "No start form")} />
      <Handle className="workflow-handle" id="out" isConnectable={isConnectable} position={Position.Right} type="source" />
    </div>
  );
}

export function UserTaskNodeView({ data, isConnectable }: NodeProps<UserTaskNode>) {
  const language = useSessionStore((state) => state.language);
  return (
    <div className="workflow-node-card workflow-node-card-task">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <NodeHeader icon={<ClipboardCheck size={17} />} kicker={workflowText(language, "Kullanıcı görevi", "User task")} language={language} title={data.label} />
      <div className="workflow-node-task-meta">
        <NodeMeta icon={<UserRoundCheck size={14} />} text={assignmentSummary(data.assignment, language)} />
        <span className={`workflow-priority-dot workflow-priority-dot-${data.priority.toLowerCase()}`} aria-hidden="true" />
        <span>{getWorkflowPriorityLabels(language)[data.priority]}</span>
      </div>
      <div className="workflow-node-action-count">
        <Check size={13} aria-hidden="true" />
        <span>{workflowText(language, `${data.actions.length} işlem`, `${data.actions.length} actions`)}</span>
      </div>
      <Handle className="workflow-handle" id="out" isConnectable={isConnectable} position={Position.Right} type="source" />
    </div>
  );
}

export function ExclusiveGatewayNodeView({ id, data, isConnectable }: NodeProps<ExclusiveGatewayNode>) {
  const branchCount = useWorkflowDraftStore((state) => state.draft.edges.filter((edge) => edge.source === id).length);
  const language = useSessionStore((state) => state.language);
  return (
    <div className="workflow-gateway-node">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <div className="workflow-gateway-diamond" aria-hidden="true">
        <GitFork size={22} />
      </div>
      <div className="workflow-gateway-copy">
        <span>{workflowText(language, "Karar", "Decision")}</span>
        <strong>{data.label}</strong>
        <small>{workflowText(language, `${branchCount} çıkış`, `${branchCount} branches`)}</small>
      </div>
      <Handle className="workflow-handle" id="out" isConnectable={isConnectable} position={Position.Right} type="source" />
    </div>
  );
}

export function CompletedEndNodeView({ data, isConnectable }: NodeProps<CompletedEndNode>) {
  const language = useSessionStore((state) => state.language);
  return (
    <div className="workflow-node-card workflow-node-card-completed">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <NodeHeader icon={<CircleCheck size={17} />} kicker={workflowText(language, "Sonuç", "Result")} language={language} title={data.label} />
    </div>
  );
}

export function RejectedEndNodeView({ data, isConnectable }: NodeProps<RejectedEndNode>) {
  const language = useSessionStore((state) => state.language);
  return (
    <div className="workflow-node-card workflow-node-card-rejected">
      <Handle className="workflow-handle" id="in" isConnectable={isConnectable} position={Position.Left} type="target" />
      <NodeHeader icon={<CircleX size={17} />} kicker={workflowText(language, "Sonuç", "Result")} language={language} title={data.label} />
    </div>
  );
}

export function TeamSwimlaneNodeView({ data }: NodeProps<TeamSwimlaneNode>) {
  const language = useSessionStore((state) => state.language);
  return (
    <div className="workflow-swimlane-node">
      <div className="workflow-swimlane-heading">
        <Rows3 size={17} aria-hidden="true" />
        <span>
          <strong>{data.label}</strong>
          <small>{data.teamName || workflowText(language, "Takım seçilmedi", "No team selected")}</small>
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

function NodeHeader({
  icon,
  kicker,
  language,
  title,
}: {
  icon: React.ReactNode;
  kicker: string;
  language: Language;
  title: string;
}) {
  return (
    <div className="workflow-node-heading">
      <span className="workflow-node-icon" aria-hidden="true">{icon}</span>
      <span>
        <small>{kicker}</small>
        <strong>{title || workflowText(language, "Adsız düğüm", "Untitled node")}</strong>
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

function assignmentSummary(assignment: WorkflowAssignment, language: Language) {
  switch (assignment.type) {
    case "processStarter":
      return workflowText(language, "Süreci başlatan", "Process starter");
    case "person":
      return assignment.personName || workflowText(language, "Kullanıcı seçilmedi", "No user selected");
    case "team":
      return assignment.teamName || workflowText(language, "Takım seçilmedi", "No team selected");
    case "communityRole":
      return assignment.communityRoleName || workflowText(language, "Rol seçilmedi", "No role selected");
    case "teamAndRole":
      return [assignment.teamName, assignment.communityRoleName].filter(Boolean).join(" · ")
        || workflowText(language, "Takım ve rol seçilmedi", "No team or role selected");
  }
}
