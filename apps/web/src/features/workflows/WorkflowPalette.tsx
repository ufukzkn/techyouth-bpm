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
import { getWorkflowNodeLabels } from "@/features/workflows/workflowLabels";
import { workflowText } from "@/features/workflows/workflowI18n";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";
import type { Language } from "@/lib/types";

export const workflowPaletteMime = "application/x-techyouth-workflow-node";

type WorkflowPaletteProps = {
  language: Language;
  readOnly: boolean;
};

type PaletteItem = {
  kind: WorkflowNodeKind;
  label: string;
  icon: LucideIcon;
};

export function WorkflowPalette({ language, readOnly }: WorkflowPaletteProps) {
  const addNode = useWorkflowDraftStore((state) => state.addNode);
  const hasStart = useWorkflowDraftStore((state) => state.draft.nodes.some((node) => node.type === "start"));
  const labels = getWorkflowNodeLabels(language);
  const text = (tr: string, en: string) => workflowText(language, tr, en);
  const paletteGroups: Array<{ label: string; items: PaletteItem[] }> = [{
    label: text("Akış", "Flow"),
    items: [
      { kind: "start", label: labels.start, icon: Play },
      { kind: "userTask", label: labels.userTask, icon: ClipboardCheck },
      { kind: "exclusiveGateway", label: labels.exclusiveGateway, icon: GitFork },
    ],
  }, {
    label: text("Sonuç", "Result"),
    items: [
      { kind: "completedEnd", label: text("Tamamlandı", "Completed"), icon: CircleCheck },
      { kind: "rejectedEnd", label: text("Reddedildi", "Rejected"), icon: CircleX },
    ],
  }, {
    label: text("Yerleşim", "Layout"),
    items: [
      { kind: "teamSwimlane", label: labels.teamSwimlane, icon: Rows3 },
    ],
  }];

  function startDrag(event: DragEvent<HTMLButtonElement>, kind: WorkflowNodeKind) {
    event.dataTransfer.setData(workflowPaletteMime, kind);
    event.dataTransfer.effectAllowed = "copy";
  }

  return (
    <aside className="workflow-palette" aria-label={text("Düğüm paleti", "Node palette")}>
      <div className="workflow-panel-heading">
        <span>{text("Palet", "Palette")}</span>
        <strong>{text("Düğümler", "Nodes")}</strong>
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
                    title={item.kind === "start" && hasStart
                      ? text("Başlangıç düğümü zaten var", "A start node already exists")
                      : item.label}
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
