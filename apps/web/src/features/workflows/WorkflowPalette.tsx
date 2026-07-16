import {
  CircleCheck,
  CircleX,
  ClipboardCheck,
  GitFork,
  GripVertical,
  Play,
  Rows3,
  type LucideIcon,
} from "lucide-react";
import type { DragEvent } from "react";
import type { WorkflowNodeKind } from "@/features/workflows/contracts";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";

export const workflowPaletteMime = "application/x-techyouth-workflow-node";

type WorkflowPaletteProps = {
  readOnly: boolean;
};

type PaletteItem = {
  kind: WorkflowNodeKind;
  label: string;
  icon: LucideIcon;
};

const paletteGroups: Array<{ label: string; items: PaletteItem[] }> = [{
  label: "Akış",
  items: [
    { kind: "start", label: "Başlangıç", icon: Play },
    { kind: "userTask", label: "Kullanıcı görevi", icon: ClipboardCheck },
    { kind: "exclusiveGateway", label: "Karar", icon: GitFork },
  ],
}, {
  label: "Sonuç",
  items: [
    { kind: "completedEnd", label: "Tamamlandı", icon: CircleCheck },
    { kind: "rejectedEnd", label: "Reddedildi", icon: CircleX },
  ],
}, {
  label: "Yerleşim",
  items: [
    { kind: "teamSwimlane", label: "Takım kulvarı", icon: Rows3 },
  ],
}];

export function WorkflowPalette({ readOnly }: WorkflowPaletteProps) {
  const addNode = useWorkflowDraftStore((state) => state.addNode);
  const hasStart = useWorkflowDraftStore((state) => state.draft.nodes.some((node) => node.type === "start"));

  function startDrag(event: DragEvent<HTMLButtonElement>, kind: WorkflowNodeKind) {
    event.dataTransfer.setData(workflowPaletteMime, kind);
    event.dataTransfer.effectAllowed = "copy";
  }

  return (
    <aside className="workflow-palette" aria-label="Düğüm paleti">
      <div className="workflow-panel-heading">
        <span>Palet</span>
        <strong>Düğümler</strong>
      </div>
      <div className="workflow-palette-groups">
        {paletteGroups.map((group) => (
          <section className="workflow-palette-group" key={group.label}>
            <h3>{group.label}</h3>
            <div className="workflow-palette-items">
              {group.items.map((item) => {
                const disabled = readOnly || (item.kind === "start" && hasStart);
                return (
                  <button
                    className="workflow-palette-item"
                    disabled={disabled}
                    draggable={!disabled}
                    key={item.kind}
                    onClick={() => addNode(item.kind)}
                    onDragStart={(event) => startDrag(event, item.kind)}
                    title={item.kind === "start" && hasStart ? "Başlangıç düğümü zaten var" : item.label}
                    type="button"
                  >
                    <span className={`workflow-palette-icon workflow-palette-icon-${item.kind}`}>
                      <item.icon size={17} aria-hidden="true" />
                    </span>
                    <span>{item.label}</span>
                    <GripVertical className="workflow-palette-grip" size={15} aria-hidden="true" />
                  </button>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </aside>
  );
}
